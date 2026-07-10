using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TacticalGame.AI;
using TacticalGame.AI.Utility;
using TacticalGame.Grid;
using TacticalGame.Messages;
using Xunit;

namespace TacticalGame.Tests
{
    public class MessageEngineTests
    {
        // ── Helpers ───────────────────────────────────────────────────

        private static MessageRegistry LoadMessages()
            => MessageRegistry.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Resources"));

        private static AIDefRegistry LoadAIDefs()
            => AIDefRegistry.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Resources"));

        private static CampaignContext Meta(params string[] factions)
        {
            var meta = new CampaignContext();
            foreach (var f in factions) meta.Factions.Add(f);
            return meta;
        }

        // Scripted facts — tests the engine machinery without a real fight,
        // and doubles as the extensibility demo (custom IFactSource).
        private sealed class StubSource : IFactSource
        {
            private readonly Queue<List<Fact>> _batches = new();
            public void Push(params Fact[] facts) => _batches.Enqueue(new List<Fact>(facts));
            public List<Fact> Extract(BattleState battle, BattleMemory memory)
                => _batches.Count > 0 ? _batches.Dequeue() : new List<Fact>();
        }

        private static MessageRule Rule(string when, string[] say, object? conditions = null,
            int priority = 0, int cooldown = 0, string once = "none", string[]? tags = null)
        {
            var rule = new MessageRule
            {
                When = when,
                Priority = priority,
                Cooldown = cooldown,
                Once = once,
            };
            rule.Say.AddRange(say);
            if (tags != null) rule.Tags.AddRange(tags);
            if (conditions != null)
                foreach (var prop in JObject.FromObject(conditions).Properties())
                    rule.If[prop.Name] = prop.Value;
            return rule;
        }

        private static MessageRegistry CodeRegistry(params MessageRule[] rules)
        {
            var pool = new MessagePool { Pool = "test_pool" };
            pool.Rules.AddRange(rules);
            var registry = new MessageRegistry();
            registry.Register(pool);
            return registry;
        }

        private static (BattleState battle, Unit attacker, Unit defender) MakeDuel(int attack = 80)
        {
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker", attack: attack);
            var defender = TestHelpers.MakeUnit("Defender");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(defender, new HexCoord(1, 0));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { defender });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.AdvanceTurn();

            return (battle, attacker, defender);
        }

        // ── Resource loading + validation ─────────────────────────────

        [Fact]
        public void Resources_LoadAllPools()
        {
            var registry = LoadMessages();
            Assert.Equal(4, registry.Pools.Count); // generic, orc, goblin, bridge_callbacks
            foreach (var pool in registry.Pools)
                Assert.NotEmpty(pool.Rules);
        }

        [Fact]
        public void Registry_RejectsUnknownCondition()
        {
            var pool = new MessagePool { Pool = "broken" };
            pool.Rules.Add(Rule("Killed", new[] { "text" }, new { tpyoCondition = 1 }));

            var registry = new MessageRegistry();
            Assert.Throws<InvalidDataException>(() => registry.Register(pool));
        }

        [Fact]
        public void Registry_RejectsRuleWithNothingToSay()
        {
            var pool = new MessagePool { Pool = "mute" };
            pool.Rules.Add(new MessageRule { When = "Killed" });

            var registry = new MessageRegistry();
            Assert.Throws<InvalidDataException>(() => registry.Register(pool));
        }

        // ── Pool gating: meta gameplay selects the subset ─────────────

        [Fact]
        public void PoolGating_OrcLines_OnlyWhenOrcsPresent()
        {
            var (battle, attacker, _) = MakeDuel();
            var stub = new StubSource();
            var disobeyed = new Fact(FactTypes.Disobeyed, battle.TurnNumber, actor: attacker,
                data: new Dictionary<string, string>
                { ["commanderWanted"] = "Cautious", ["unitChose"] = "Berserker" });

            // Orcs in this fight → orc pool active
            var withOrcs = new MessageEngine(LoadMessages(), Meta("Orcs"));
            withOrcs.SetVoice(attacker, "Orc");
            withOrcs.AddFactSource(stub);
            withOrcs.BeginBattle(battle);
            stub.Push(disobeyed);
            withOrcs.ObserveTurn(battle);
            Assert.Single(withOrcs.DrainLines());

            // No orcs → pool inactive, same fact stays silent
            var withoutOrcs = new MessageEngine(LoadMessages(), Meta("Undead"));
            withoutOrcs.SetVoice(attacker, "Orc");
            withoutOrcs.AddFactSource(stub);
            withoutOrcs.BeginBattle(battle);
            stub.Push(disobeyed);
            withoutOrcs.ObserveTurn(battle);
            Assert.Empty(withoutOrcs.DrainLines());
        }

        [Fact]
        public void PoolGating_CampaignCallback_NeedsChapterAndFlag()
        {
            var (battle, attacker, defender) = MakeDuel();
            var allyDied = new Fact(FactTypes.AllyDied, battle.TurnNumber,
                actor: attacker, target: defender,
                data: new Dictionary<string, string> { ["ally"] = "Defender", ["killer"] = "X" });

            // Chapter 3 + flag → callback line fires
            var late = Meta();
            late.Chapter = 3;
            late.Flags.Add("bridge_massacre");
            var engine = new MessageEngine(LoadMessages(), late);
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);
            stub.Push(allyDied);
            engine.ObserveTurn(battle);
            var lines = engine.DrainLines();
            Assert.Single(lines);
            Assert.Contains("bridge", lines[0].Text, StringComparison.Ordinal);

            // Chapter 2, no flag → pool gated off, nothing to say
            var early = Meta();
            early.Chapter = 2;
            var silent = new MessageEngine(LoadMessages(), early);
            var stub2 = new StubSource();
            silent.AddFactSource(stub2);
            silent.BeginBattle(battle);
            stub2.Push(allyDied);
            silent.ObserveTurn(battle);
            Assert.Empty(silent.DrainLines());
        }

        [Fact]
        public void OnceCampaign_LineNeverRepeats_AcrossBattles()
        {
            var (battle, attacker, defender) = MakeDuel();
            var meta = Meta();
            meta.Chapter = 3;
            meta.Flags.Add("bridge_massacre");

            Fact AllyDied() => new(FactTypes.AllyDied, battle.TurnNumber,
                actor: attacker, target: defender,
                data: new Dictionary<string, string> { ["ally"] = "D", ["killer"] = "X" });

            var engine = new MessageEngine(LoadMessages(), meta);
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);
            stub.Push(AllyDied());
            engine.ObserveTurn(battle);
            Assert.Single(engine.DrainLines()); // battle 1: spoken

            engine.BeginBattle(battle);         // battle 2, same campaign
            stub.Push(AllyDied());
            engine.ObserveTurn(battle);
            Assert.Empty(engine.DrainLines());  // burned for the whole campaign

            // Campaign memory survives save/load
            var restored = CampaignMemory.FromJson(engine.Campaign.ToJson());
            var engine2 = new MessageEngine(LoadMessages(), meta, restored);
            var stub2 = new StubSource();
            engine2.AddFactSource(stub2);
            engine2.BeginBattle(battle);
            stub2.Push(AllyDied());
            engine2.ObserveTurn(battle);
            Assert.Empty(engine2.DrainLines());
        }

        // ── Grudges and conversations (engine's own memory) ───────────

        [Fact]
        public void Grudge_FromAllyDeath_FuelsRevengeLine()
        {
            var battle = TestHelpers.MakeBattle();
            var avenger = TestHelpers.MakeUnit("Avenger");
            var brother = TestHelpers.MakeUnit("Brother");
            var killer = TestHelpers.MakeUnit("Killer");
            battle.PlaceUnit(avenger, HexCoord.Zero);
            battle.PlaceUnit(brother, new HexCoord(0, 1));
            battle.PlaceUnit(killer, new HexCoord(1, 0));
            battle.RegisterTeam(0, new List<Unit> { avenger, brother });
            battle.RegisterTeam(1, new List<Unit> { killer });
            battle.AdvanceTurn();

            var engine = new MessageEngine(LoadMessages(), Meta());
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);

            // Killer kills Brother → Avenger silently remembers
            stub.Push(new Fact(FactTypes.Killed, battle.TurnNumber, actor: killer, target: brother,
                data: new Dictionary<string, string> { ["victim"] = "Brother", ["killer"] = "Killer" }));
            engine.ObserveTurn(battle);
            engine.DrainLines();
            Assert.True(engine.Memory.HasGrudge(avenger, killer));

            // Avenger kills Killer → the revenge line, not a generic boast
            battle.AdvanceTurn();
            stub.Push(new Fact(FactTypes.Killed, battle.TurnNumber, actor: avenger, target: killer,
                data: new Dictionary<string, string> { ["victim"] = "Killer", ["killer"] = "Avenger" }));
            engine.ObserveTurn(battle);

            var lines = engine.DrainLines();
            Assert.Single(lines);
            Assert.Same(avenger, lines[0].Speaker);
            Assert.Contains("revenge", lines[0].Tags);
        }

        [Fact]
        public void Grudge_FromHeavyHit_NotFromScratch()
        {
            var (battle, attacker, defender) = MakeDuel();
            var engine = new MessageEngine(CodeRegistry(), Meta());
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);

            // 10 damage on 100 max HP → shrugged off
            stub.Push(new Fact(FactTypes.Hurt, battle.TurnNumber,
                actor: attacker, target: defender, amount: 10));
            engine.ObserveTurn(battle);
            Assert.False(engine.Memory.HasGrudge(defender, attacker));

            // 30 damage → that's personal now
            battle.AdvanceTurn();
            stub.Push(new Fact(FactTypes.Hurt, battle.TurnNumber,
                actor: attacker, target: defender, amount: 30));
            engine.ObserveTurn(battle);
            Assert.True(engine.Memory.HasGrudge(defender, attacker));
        }

        [Fact]
        public void Conversation_TauntGetsAnswered_NextTurn()
        {
            var (battle, attacker, defender) = MakeDuel();
            var registry = CodeRegistry(
                Rule("Killed", new[] { "You were nothing!" }, tags: new[] { "taunt" }),
                Rule("Said", new[] { "Keep talking, {actor}." },
                    new { speakerIs = "enemyOfActor", tag = "taunt" }, tags: new[] { "reply" }));

            var engine = new MessageEngine(registry, Meta());
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);

            // Attacker kills someone → taunts
            stub.Push(new Fact(FactTypes.Killed, battle.TurnNumber, actor: attacker, target: defender));
            engine.ObserveTurn(battle);
            var taunt = Assert.Single(engine.DrainLines());
            Assert.Same(attacker, taunt.Speaker);

            // Next turn: the enemy heard it (everyone hears everyone) and answers
            battle.AdvanceTurn();
            engine.ObserveTurn(battle);
            var reply = Assert.Single(engine.DrainLines());
            Assert.Same(defender, reply.Speaker);
            Assert.Equal("Keep talking, Attacker.", reply.Text);
        }

        // ── Authoring mechanics: substitution, variants ───────────────

        [Fact]
        public void Substitution_FillsVariables()
        {
            var (battle, attacker, defender) = MakeDuel();
            var registry = CodeRegistry(
                Rule("Hurt", new[] { "{unit} hit {victim} for {amount}!" }));

            var engine = new MessageEngine(registry, Meta());
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);

            stub.Push(new Fact(FactTypes.Hurt, battle.TurnNumber,
                actor: attacker, target: defender, amount: 42,
                data: new Dictionary<string, string> { ["victim"] = "Defender" }));
            engine.ObserveTurn(battle);

            Assert.Equal("Attacker hit Defender for 42!", engine.DrainLines()[0].Text);
        }

        [Fact]
        public void Variants_DontRepeat_UntilAllHeard()
        {
            var (battle, attacker, defender) = MakeDuel();
            var registry = CodeRegistry(
                Rule("Hurt", new[] { "one", "two", "three" }));

            var engine = new MessageEngine(registry, Meta());
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);

            var heard = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                stub.Push(new Fact(FactTypes.Hurt, battle.TurnNumber, actor: attacker, target: defender));
                engine.ObserveTurn(battle);
                heard.Add(engine.DrainLines()[0].Text);
                battle.AdvanceTurn();
            }

            Assert.Equal(3, new HashSet<string>(heard).Count); // all distinct
        }

        // ── Anti-spam: cooldown, once, budget, one line per speaker ───

        [Fact]
        public void Cooldown_SilencesRepeats()
        {
            var (battle, attacker, defender) = MakeDuel();
            var registry = CodeRegistry(
                Rule("Hurt", new[] { "ow" }, cooldown: 3));

            var engine = new MessageEngine(registry, Meta());
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);

            stub.Push(new Fact(FactTypes.Hurt, battle.TurnNumber, actor: attacker, target: defender));
            engine.ObserveTurn(battle);
            Assert.Single(engine.DrainLines());

            battle.AdvanceTurn(); // only 1 turn later — still cooling down
            stub.Push(new Fact(FactTypes.Hurt, battle.TurnNumber, actor: attacker, target: defender));
            engine.ObserveTurn(battle);
            Assert.Empty(engine.DrainLines());
        }

        [Fact]
        public void OnceBattle_FiresExactlyOnce()
        {
            var (battle, attacker, defender) = MakeDuel();
            var registry = CodeRegistry(
                Rule("Hurt", new[] { "never again" }, once: "battle"));

            var engine = new MessageEngine(registry, Meta());
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);

            for (int i = 0; i < 3; i++)
            {
                stub.Push(new Fact(FactTypes.Hurt, battle.TurnNumber, actor: attacker, target: defender));
                engine.ObserveTurn(battle);
                battle.AdvanceTurn();
            }

            Assert.Single(engine.DrainLines());
        }

        [Fact]
        public void Budget_CapsLinesPerTurn()
        {
            var battle = TestHelpers.MakeBattle();
            var units = new List<Unit>();
            for (int i = 0; i < 4; i++)
            {
                var u = TestHelpers.MakeUnit($"U{i}");
                battle.PlaceUnit(u, new HexCoord(i - 2, 0));
                units.Add(u);
            }
            battle.RegisterTeam(0, new List<Unit> { units[0], units[1] });
            battle.RegisterTeam(1, new List<Unit> { units[2], units[3] });
            battle.AdvanceTurn();

            // 4 distinct rules all firing the same turn — budget must cap at 3
            var registry = CodeRegistry(
                Rule("Hurt", new[] { "a" }, priority: 4),
                Rule("Moved", new[] { "b" }, priority: 3),
                Rule("SkillUsed", new[] { "c" }, priority: 2),
                Rule("Outnumbered", new[] { "d" }, priority: 1));

            var engine = new MessageEngine(registry, Meta());
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);

            stub.Push(
                new Fact(FactTypes.Hurt, 1, actor: units[0]),
                new Fact(FactTypes.Moved, 1, actor: units[1]),
                new Fact(FactTypes.SkillUsed, 1, actor: units[2]),
                new Fact(FactTypes.Outnumbered, 1, actor: units[3]));
            engine.ObserveTurn(battle);

            var lines = engine.DrainLines();
            Assert.Equal(3, lines.Count);
            Assert.Equal("a", lines[0].Text); // highest priority survived the cut
        }

        [Fact]
        public void Speaker_SaysOneLinePerTurn()
        {
            var (battle, attacker, defender) = MakeDuel();
            var registry = CodeRegistry(
                Rule("Hurt", new[] { "high" }, priority: 10),
                Rule("Killed", new[] { "low" }, priority: 1));

            var engine = new MessageEngine(registry, Meta());
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);

            // Both facts point at the same speaker — only the higher-priority line lands
            stub.Push(
                new Fact(FactTypes.Hurt, battle.TurnNumber, actor: attacker),
                new Fact(FactTypes.Killed, battle.TurnNumber, actor: attacker, target: defender));
            engine.ObserveTurn(battle);

            var line = Assert.Single(engine.DrainLines());
            Assert.Equal("high", line.Text);
        }

        // ── Universality: custom facts and conditions ─────────────────

        [Fact]
        public void CustomFactType_AndCondition_PlugIn()
        {
            MessageConditions.Register("always", (value, ctx) => value.Value<bool>());

            var (battle, attacker, _) = MakeDuel();
            var registry = CodeRegistry(
                Rule("EarthShake", new[] { "The ground trembles..." },
                    new { always = true, speakerIs = "any" }));

            var engine = new MessageEngine(registry, Meta());
            var stub = new StubSource();
            engine.AddFactSource(stub);
            engine.BeginBattle(battle);

            stub.Push(new Fact("EarthShake", battle.TurnNumber));
            engine.ObserveTurn(battle);

            Assert.Single(engine.DrainLines());
        }

        // ── AI state integration ──────────────────────────────────────

        [Fact]
        public void Disobedience_ObservedFromPlanner_ProducesLine()
        {
            var (battle, attacker, _) = MakeDuel(attack: 80);
            var planner = new UtilityAIPlanner(LoadAIDefs(), LoadAIDefs().Commander("GoblinBoss"));

            var engine = new MessageEngine(LoadMessages(), Meta("Goblins"),
                planners: new[] { planner });
            engine.SetVoice(attacker, "Goblin");
            engine.BeginBattle(battle);

            // Strong healthy unit under a coward commander → disobedience fires
            planner.Plan(attacker, battle);
            Assert.NotEmpty(planner.DisobedienceSnapshot());

            engine.ObserveTurn(battle);
            var lines = engine.DrainLines();

            Assert.Contains(lines, l => l.Speaker == attacker && l.Tags.Contains("disobedience"));
        }

        // ── Real battles end-to-end ───────────────────────────────────

        [Fact]
        public void LastAlive_RealBattle_SpeaksGenericLine()
        {
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker", attack: 80);
            var victimA = TestHelpers.MakeUnit("VictimA", maxHP: 30, maxArmor: 0);
            var victimB = TestHelpers.MakeUnit("VictimB", maxHP: 30, maxArmor: 0);
            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(victimA, new HexCoord(1, 0));
            battle.PlaceUnit(victimB, new HexCoord(1, -1));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { victimA, victimB });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));

            var manager = new BattleManager(battle, useThreads: false);
            var engine = new MessageEngine(LoadMessages(), Meta(), seed: 7);
            engine.BeginBattle(battle);

            var all = new List<MessageLine>();
            int turns = 0;
            while (!manager.IsBattleOver() && turns++ < 30)
            {
                manager.StepTurn();
                engine.ObserveTurn(battle);
                all.AddRange(engine.DrainLines());
            }

            // First victim died → the survivor noticed being the last one
            Assert.Contains(all, l =>
                l.RuleId.StartsWith("generic_battle:LastAlive", StringComparison.Ordinal));
        }

        [Fact]
        public void Determinism_SameSeed_SameLines()
        {
            List<string> RunBattle(int seed)
            {
                var battle = TestHelpers.MakeBattle();
                var attacker = TestHelpers.MakeUnit("Attacker", attack: 80);
                var victimA = TestHelpers.MakeUnit("VictimA", maxHP: 30, maxArmor: 0);
                var victimB = TestHelpers.MakeUnit("VictimB", maxHP: 30, maxArmor: 0);
                battle.PlaceUnit(attacker, HexCoord.Zero);
                battle.PlaceUnit(victimA, new HexCoord(1, 0));
                battle.PlaceUnit(victimB, new HexCoord(1, -1));
                battle.RegisterTeam(0, new List<Unit> { attacker });
                battle.RegisterTeam(1, new List<Unit> { victimA, victimB });
                battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));

                var manager = new BattleManager(battle, useThreads: false);
                var engine = new MessageEngine(LoadMessages(), Meta(), seed: seed);
                engine.BeginBattle(battle);

                var texts = new List<string>();
                int turns = 0;
                while (!manager.IsBattleOver() && turns++ < 30)
                {
                    manager.StepTurn();
                    engine.ObserveTurn(battle);
                    foreach (var line in engine.DrainLines()) texts.Add(line.ToString());
                }
                return texts;
            }

            Assert.Equal(RunBattle(seed: 42), RunBattle(seed: 42));
        }

        [Fact]
        public void ThreadedBattle_WithObserver_RunsClean()
        {
            var registry = LoadAIDefs();
            var battle = TestHelpers.MakeBattle(gridRadius: 5);
            var teamA = new List<Unit>();
            var teamB = new List<Unit>();
            for (int i = 0; i < 3; i++)
            {
                var a = TestHelpers.MakeUnit($"Orc{i}", attack: 60);
                var b = TestHelpers.MakeUnit($"Gob{i}", attack: 60);
                battle.PlaceUnit(a, new HexCoord(-3, i));
                battle.PlaceUnit(b, new HexCoord(3, i - 3));
                teamA.Add(a);
                teamB.Add(b);
                battle.Equip(a, new Equipment(TestHelpers.SampleWeapons.Axe));
                battle.Equip(b, new Equipment(TestHelpers.SampleWeapons.Axe));
            }
            battle.RegisterTeam(0, teamA);
            battle.RegisterTeam(1, teamB);

            var orc = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            var goblin = new UtilityAIPlanner(registry, registry.Commander("GoblinBoss"));
            var manager = new BattleManager(battle,
                (unit, b) => (unit.TeamIndex == 0 ? orc : goblin).Plan(unit, b),
                useThreads: true);

            var engine = new MessageEngine(LoadMessages(), Meta("Orcs", "Goblins"),
                seed: 1, planners: new[] { orc, goblin });
            foreach (var u in teamA) engine.SetVoice(u, "Orc");
            foreach (var u in teamB) engine.SetVoice(u, "Goblin");
            engine.BeginBattle(battle);

            var all = new List<MessageLine>();
            int turns = 0;
            while (!manager.IsBattleOver() && turns++ < 100)
            {
                manager.StepTurn();
                engine.ObserveTurn(battle); // observer runs against a parallel-planned battle
                all.AddRange(engine.DrainLines());
            }

            Assert.True(manager.IsBattleOver() || turns >= 100);
            Assert.NotEmpty(all); // a full fight between talkative factions is never silent
            foreach (var line in all)
                Assert.False(string.IsNullOrEmpty(line.Text));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TacticalGame.AI;
using TacticalGame.AI.Utility;
using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    /// <summary>
    /// Audit tests for the three core guarantees:
    ///   1. Determinism — same setup, same run, every time (threaded or not)
    ///   2. Reversibility — any turn can be undone back to an exact prior state
    ///   3. Save/Load — a loaded battle behaves identically to the original
    ///
    /// Tests named KnownBug_* document currently broken behavior and are
    /// EXPECTED TO FAIL until the underlying issue is fixed. See the audit
    /// report for details. Do not "fix" the tests — fix the code.
    /// </summary>
    public class DeterminismReversibilityTests
    {
        // ── Helpers ───────────────────────────────────────────────────────

        /// Full-state fingerprint: turn number + every unit's mutable state
        /// + occupancy consistency. Two states with equal snapshots are
        /// gameplay-identical.
        private static string Snapshot(BattleState b)
        {
            var sb = new StringBuilder();
            sb.Append("turn=").Append(b.TurnNumber).Append('\n');
            foreach (var u in b.Units)
            {
                bool inCell = b.Grid.Cells.TryGetValue(u.Position, out var cell)
                              && System.Linq.Enumerable.Contains(cell.Occupants, u);
                sb.Append(u.Name)
                  .Append(" pos=").Append(u.Position)
                  .Append(" team=").Append(u.TeamIndex)
                  .Append(" hp=").Append(u.Stats.CurrentHP)
                  .Append(" armor=").Append(u.Stats.CurrentArmor)
                  .Append(" fat=").Append(u.Stats.CurrentFatigue)
                  .Append(" mor=").Append(u.Stats.Morale)
                  .Append(" alive=").Append(u.IsAlive)
                  .Append(" inCell=").Append(inCell)
                  .Append('\n');
            }
            return sb.ToString();
        }

        // CompoundCommand.ToString() throws NRE on recovery commands
        // (ResolveRecovery passes null! for weapon/skill) — see report.
        private static string Fingerprint(IBattleCommand cmd)
            => cmd is CompoundCommand c && c.Skill == null
                ? c.Unit + ": recovery"
                : cmd.ToString()!;

        private static string Fingerprint(IEnumerable<IBattleCommand> cmds)
        {
            var parts = new List<string>();
            foreach (var cmd in cmds) parts.Add(Fingerprint(cmd));
            return string.Join(" ;; ", parts);
        }

        /// Runs the battle to completion (safety-capped), returning one
        /// log line per turn built from command fingerprints — a behavioral
        /// fingerprint of the whole battle.
        private static List<string> RunToEnd(BattleManager m, int maxTurns = 60)
        {
            var log = new List<string>();
            for (int i = 0; i < maxTurns && !m.IsBattleOver(); i++)
                log.Add(Fingerprint(m.StepTurn()));
            return log;
        }

        private static AIDefRegistry LoadRegistry()
            => AIDefRegistry.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Resources"));

        /// 2v2 scenario with mixed weapons and spacing so movement,
        /// targeting and multi-unit planning all participate.
        private static BattleState MakeSkirmish()
        {
            var battle = TestHelpers.MakeBattle(gridRadius: 4);

            var a1 = TestHelpers.MakeUnit("A1", attack: 15);
            var a2 = TestHelpers.MakeUnit("A2", attack: 10);
            var b1 = TestHelpers.MakeUnit("B1", attack: 12);
            var b2 = TestHelpers.MakeUnit("B2", attack: 12, maxHP: 80);

            battle.PlaceUnit(a1, new HexCoord(-3, 0));
            battle.PlaceUnit(a2, new HexCoord(-3, 1));
            battle.PlaceUnit(b1, new HexCoord(3, 0));
            battle.PlaceUnit(b2, new HexCoord(3, -1));
            battle.RegisterTeam(0, new List<Unit> { a1, a2 });
            battle.RegisterTeam(1, new List<Unit> { b1, b2 });

            battle.Equip(a1, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.Equip(a2, new Equipment(TestHelpers.SampleWeapons.Sword));
            battle.Equip(b1, new Equipment(TestHelpers.SampleWeapons.Sword));
            battle.Equip(b2, new Equipment(TestHelpers.SampleWeapons.Axe));

            return battle;
        }

        private static BattleManager MakeUtilityManager(BattleState battle, bool useThreads)
        {
            var registry = LoadRegistry();
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            return new BattleManager(battle, planner.Plan, useThreads);
        }

        // ── 1. Determinism ────────────────────────────────────────────────

        [Fact]
        public void Determinism_DefaultPlanner_TwoRuns_IdenticalBattle()
        {
            var m1 = new BattleManager(MakeSkirmish(), useThreads: false);
            var m2 = new BattleManager(MakeSkirmish(), useThreads: false);

            var log1 = RunToEnd(m1);
            var log2 = RunToEnd(m2);

            Assert.Equal(log1, log2);
            Assert.Equal(Snapshot(m1.Battle), Snapshot(m2.Battle));
        }

        [Fact]
        public void Determinism_DefaultPlanner_Threaded_MatchesSequential()
        {
            var seq = new BattleManager(MakeSkirmish(), useThreads: false);
            var par = new BattleManager(MakeSkirmish(), useThreads: true);

            var logSeq = RunToEnd(seq);
            var logPar = RunToEnd(par);

            Assert.Equal(logSeq, logPar);
            Assert.Equal(Snapshot(seq.Battle), Snapshot(par.Battle));
        }

        [Fact]
        public void Determinism_UtilityPlanner_TwoRuns_IdenticalBattle()
        {
            var m1 = MakeUtilityManager(MakeSkirmish(), useThreads: false);
            var m2 = MakeUtilityManager(MakeSkirmish(), useThreads: false);

            var log1 = RunToEnd(m1);
            var log2 = RunToEnd(m2);

            Assert.Equal(log1, log2);
            Assert.Equal(Snapshot(m1.Battle), Snapshot(m2.Battle));
        }

        [Fact]
        public void Determinism_UtilityPlanner_Threaded_MatchesSequential()
        {
            var seq = MakeUtilityManager(MakeSkirmish(), useThreads: false);
            var par = MakeUtilityManager(MakeSkirmish(), useThreads: true);

            var logSeq = RunToEnd(seq);
            var logPar = RunToEnd(par);

            Assert.Equal(logSeq, logPar);
            Assert.Equal(Snapshot(seq.Battle), Snapshot(par.Battle));
        }

        // ── 2. Reversibility ──────────────────────────────────────────────

        [Fact]
        public void Undo_SingleTurn_RestoresExactSnapshot()
        {
            var manager = new BattleManager(MakeSkirmish(), useThreads: false);
            var before = Snapshot(manager.Battle);

            manager.StepTurn();
            manager.UndoLastTurn();

            Assert.Equal(before, Snapshot(manager.Battle));
        }

        [Fact]
        public void Undo_ThreeTurns_RestoresExactSnapshot()
        {
            var manager = new BattleManager(MakeSkirmish(), useThreads: false);
            var before = Snapshot(manager.Battle);

            manager.StepTurn();
            manager.StepTurn();
            manager.StepTurn();
            manager.UndoLastTurn();
            manager.UndoLastTurn();
            manager.UndoLastTurn();

            Assert.Equal(before, Snapshot(manager.Battle));
        }

        [Fact]
        public void Undo_ThenReplay_ProducesIdenticalTurn_DefaultPlanner()
        {
            var manager = new BattleManager(MakeSkirmish(), useThreads: false);
            manager.StepTurn();

            var original = Fingerprint(manager.StepTurn());
            var stateAfter = Snapshot(manager.Battle);

            manager.UndoLastTurn();
            var replayed = Fingerprint(manager.StepTurn());

            Assert.Equal(original, replayed);
            Assert.Equal(stateAfter, Snapshot(manager.Battle));
        }

        // KNOWN BUG (undo-overkill): DamageEffect.Apply stores the *intended*
        // HP damage from SplitDamage, not the *actual* clamped delta returned
        // by ChangeHP. When damage exceeds remaining HP (any killing blow),
        // Reverse restores more HP than was removed — undo inflates HP.
        [Fact]
        public void KnownBug_Undo_OverkillDamage_RestoresExactHP()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            battle.ChangeArmor(defender, -defender.Stats.MaxArmor); // armor 0
            battle.ChangeHP(defender, -(defender.Stats.MaxHP - 5)); // hp 5

            var effect = new DamageEffect(attacker, defender, 60) { IsEssential = true };
            effect.Apply(battle);
            Assert.Equal(0, defender.Stats.CurrentHP); // dead

            effect.Reverse(battle);
            Assert.Equal(5, defender.Stats.CurrentHP); // FAILS today: restored to 60
        }

        [Fact]
        public void Undo_FullBattle_RestoresInitialSnapshot()
        {
            var manager = new BattleManager(MakeSkirmish(), useThreads: false);
            var initial = Snapshot(manager.Battle);

            RunToEnd(manager);
            while (manager.CanUndo)
                manager.UndoLastTurn();

            Assert.Equal(initial, Snapshot(manager.Battle));
        }

        // KNOWN BUG (undo-overkill, integration): undoing the turn with the
        // killing blow revives the victim at the wrong HP when the blow
        // overshot remaining HP (97 HP vs 15-damage hits → last hit lands
        // on 7 HP, undo restores 15). Note: unwinding ALL turns can mask
        // this — over-restoration clamps at MaxHP, so a unit that started
        // at full HP ends up "correct" by accident. Partial undo exposes it.
        [Fact]
        public void KnownBug_Undo_KillingBlowTurn_RestoresPreTurnSnapshot()
        {
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker");
            var victim = TestHelpers.MakeUnit("Victim", maxHP: 97, maxArmor: 0);
            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(victim, new HexCoord(1, 0));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { victim });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));

            var manager = new BattleManager(battle, useThreads: false);

            var beforeLastTurn = "";
            for (int i = 0; i < 60 && !manager.IsBattleOver(); i++)
            {
                beforeLastTurn = Snapshot(battle);
                manager.StepTurn();
            }
            Assert.False(victim.IsAlive); // the kill actually happened

            manager.UndoLastTurn();

            Assert.Equal(beforeLastTurn, Snapshot(battle));
        }

        // UtilityAIPlanner keeps per-unit memory (strategy, goal, commander
        // phase turn stamp) OUTSIDE BattleState. Undo rewinds the battle but
        // not that memory, so replaying rewound turns can diverge from the
        // original timeline. This asserts the desired property: undo N turns,
        // replay N turns → identical commands.
        [Fact]
        public void Undo_ThenReplay_ProducesIdenticalTurns_UtilityPlanner()
        {
            var battle = MakeSkirmish();
            var manager = MakeUtilityManager(battle, useThreads: false);

            var originalLog = new List<string>();
            for (int i = 0; i < 4 && !manager.IsBattleOver(); i++)
                originalLog.Add(Fingerprint(manager.StepTurn()));

            while (manager.CanUndo)
                manager.UndoLastTurn();

            var replayLog = new List<string>();
            for (int i = 0; i < originalLog.Count; i++)
                replayLog.Add(Fingerprint(manager.StepTurn()));

            Assert.Equal(originalLog, replayLog);
        }

        // ── 3. Save/Load ──────────────────────────────────────────────────

        private static BattleManager SaveAndLoad(BattleState battle, PlanAction? planner = null)
        {
            var stream = new MemoryStream();
            BattleSave.Save(battle, stream);
            stream.Position = 0;
            return new BattleManager(BattleSave.Load(stream), planner, useThreads: false);
        }

        [Fact]
        public void SaveLoad_MidBattle_SnapshotIdentical()
        {
            var manager = new BattleManager(MakeSkirmish(), useThreads: false);
            manager.StepTurn();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager.Battle);

            Assert.Equal(Snapshot(manager.Battle), Snapshot(loaded.Battle));
        }

        [Fact]
        public void SaveLoad_Continuation_MatchesOriginal_DefaultPlanner()
        {
            var manager = new BattleManager(MakeSkirmish(), useThreads: false);
            manager.StepTurn();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager.Battle);

            var originalLog = RunToEnd(manager);
            var loadedLog = RunToEnd(loaded);

            Assert.Equal(originalLog, loadedLog);
            Assert.Equal(Snapshot(manager.Battle), Snapshot(loaded.Battle));
        }

        [Fact]
        public void SaveLoad_UndoAfterLoad_MatchesUndoWithoutSave()
        {
            var manager = new BattleManager(MakeSkirmish(), useThreads: false);
            manager.StepTurn();
            manager.StepTurn();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager.Battle);

            manager.UndoLastTurn();
            loaded.UndoLastTurn();

            Assert.Equal(Snapshot(manager.Battle), Snapshot(loaded.Battle));
        }

        [Fact]
        public void SaveLoad_UndoToTurnZero_AfterLoad()
        {
            var manager = new BattleManager(MakeSkirmish(), useThreads: false);
            var initial = Snapshot(manager.Battle);
            manager.StepTurn();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager.Battle);
            while (loaded.CanUndo)
                loaded.UndoLastTurn();

            Assert.Equal(initial, Snapshot(loaded.Battle));
        }

        // UtilityAIPlanner state (strategies, goals, hysteresis) is not part
        // of BattleSave. A fresh planner on a loaded battle starts cold, so
        // continuation can diverge from the original session. This asserts
        // the desired property; failure means AI state must be serialized
        // (or accepted as a documented limitation).
        [Fact]
        public void SaveLoad_Continuation_MatchesOriginal_UtilityPlanner()
        {
            var battle = MakeSkirmish();
            var manager = MakeUtilityManager(battle, useThreads: false);
            manager.StepTurn();
            manager.StepTurn();

            var stream = new MemoryStream();
            BattleSave.Save(manager.Battle, stream);
            stream.Position = 0;
            var loaded = MakeUtilityManager(BattleSave.Load(stream), useThreads: false);

            var originalLog = RunToEnd(manager);
            var loadedLog = RunToEnd(loaded);

            Assert.Equal(originalLog, loadedLog);
        }
    }
}

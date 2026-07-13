using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TacticalGame.AI;
using TacticalGame.AI.Layers;
using TacticalGame.AI.Utility;
using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class AIBehaviorTests
    {
        private static AIDefRegistry LoadRegistry()
            => AIDefRegistry.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Resources"));

        // ── Resource loading ──────────────────────────────────────────

        [Fact]
        public void Resources_LoadFromDisk()
        {
            var registry = LoadRegistry();

            Assert.Equal(5, registry.Goals.Count);        // KillUnit, HelpAlly, HoldPosition, MoveToPosition, UseSkill
            Assert.Equal(6, registry.Strategies.Count);   // Tank, DPS, Berserker, Cautious, Protector, Retreating
            Assert.Equal(3, registry.Commanders.Count);   // OrcWarchief, GoblinBoss, KnightCaptain
            Assert.Equal(2, registry.ActionWeightSets.Count);

            Assert.Equal(50f, registry.Goal(GoalTypes.KillUnit).Modifier("killTargetBonus"));
            Assert.Equal(0.7f, registry.Strategy("Berserker").Modifier("aggressionBias"));
            Assert.Equal(0.6f, registry.Commander("OrcWarchief").AggressionBias);
            Assert.Equal(45f, registry.ActionWeights("RecklessActions")!.Get("moveStep"));
        }

        // ── Commander layer: dual-score strategy assignment ───────────

        [Fact]
        public void OrcCommander_AssignsAggressiveStrategy_ToStrongUnit()
        {
            var registry = LoadRegistry();
            var (battle, attacker, _) = MakeDuelWithStats(attack: 80);
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));

            planner.Plan(attacker, battle);

            var strategy = planner.StateOf(attacker).Strategy;
            Assert.NotNull(strategy);
            Assert.True(strategy!.Name == "Berserker" || strategy.Name == "DPS",
                $"expected aggressive strategy, got {strategy.Name}");
        }

        [Fact]
        public void GoblinCommander_SameUnit_AvoidsBerserker()
        {
            var registry = LoadRegistry();
            var (battle, attacker, _) = MakeDuelWithStats(attack: 80);
            var planner = new UtilityAIPlanner(registry, registry.Commander("GoblinBoss"));

            planner.Plan(attacker, battle);

            Assert.NotEqual("Berserker", planner.StateOf(attacker).Strategy!.Name);
        }

        [Fact]
        public void Disobedience_Fires_WhenUnitDesireOverridesCommander()
        {
            var registry = LoadRegistry();
            // Strong healthy fighter under a cowardly commander: goblin wants
            // Cautious, unit's own desire drags the result to an attack strategy.
            var (battle, attacker, _) = MakeDuelWithStats(attack: 80);
            var planner = new UtilityAIPlanner(registry, registry.Commander("GoblinBoss"));

            planner.Plan(attacker, battle);

            var events = planner.DisobedienceSnapshot();
            Assert.Contains(events, e => e.Unit == attacker && e.CommanderWanted == "Cautious");
        }

        [Fact]
        public void Obedience_NoEvent_WhenCommanderAndDesireAgree()
        {
            var registry = LoadRegistry();
            // Badly wounded weak unit under a cowardly commander: both agree on caution.
            var (battle, attacker, _) = MakeDuelWithStats(attack: 10);
            battle.ChangeHP(attacker, -90);
            battle.ChangeArmor(attacker, -50);

            var planner = new UtilityAIPlanner(registry, registry.Commander("GoblinBoss"));
            planner.Plan(attacker, battle);

            Assert.DoesNotContain(planner.DisobedienceSnapshot(), e => e.Unit == attacker);
        }

        // ── Strategy layer: goals, persistence ────────────────────────

        [Fact]
        public void DpsStrategy_TargetsWoundedEnemy()
        {
            var registry = LoadRegistry();
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker", attack: 80);
            var healthy = TestHelpers.MakeUnit("Healthy");
            var wounded = TestHelpers.MakeUnit("Wounded");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(healthy, new HexCoord(1, 0));
            battle.PlaceUnit(wounded, new HexCoord(1, -1));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { healthy, wounded });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.ChangeHP(wounded, -70);

            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            var action = planner.Plan(attacker, battle);

            Assert.Same(wounded, planner.StateOf(attacker).Goal!.TargetUnit);
            Assert.NotNull(action);
            Assert.IsType<SkillAction>(action);
            Assert.Equal(wounded.Position, action!.Target);
        }

        [Fact]
        public void Goal_Persists_WithinMinTurns()
        {
            var registry = LoadRegistry();
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker", attack: 80);
            var enemyA = TestHelpers.MakeUnit("EnemyA");
            var enemyB = TestHelpers.MakeUnit("EnemyB");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(enemyA, new HexCoord(1, 0));
            battle.PlaceUnit(enemyB, new HexCoord(1, -1));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { enemyA, enemyB });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.AdvanceTurn();

            battle.ChangeHP(enemyA, -40); // A is the better kill now
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            planner.Plan(attacker, battle);
            Assert.Same(enemyA, planner.StateOf(attacker).Goal!.TargetUnit);

            // B becomes the juicier target, but goal is younger than MinTurns
            battle.ChangeHP(enemyB, -60);
            battle.AdvanceTurn();
            planner.Plan(attacker, battle);

            Assert.Same(enemyA, planner.StateOf(attacker).Goal!.TargetUnit);
        }

        [Fact]
        public void Goal_Switches_WhenTargetDies()
        {
            var registry = LoadRegistry();
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker", attack: 80);
            var enemyA = TestHelpers.MakeUnit("EnemyA");
            var enemyB = TestHelpers.MakeUnit("EnemyB");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(enemyA, new HexCoord(1, 0));
            battle.PlaceUnit(enemyB, new HexCoord(1, -1));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { enemyA, enemyB });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.AdvanceTurn();

            battle.ChangeHP(enemyA, -40);
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            planner.Plan(attacker, battle);
            Assert.Same(enemyA, planner.StateOf(attacker).Goal!.TargetUnit);

            battle.ChangeHP(enemyA, -100); // dead
            battle.AdvanceTurn();
            planner.Plan(attacker, battle);

            Assert.Same(enemyB, planner.StateOf(attacker).Goal!.TargetUnit);
        }

        // ── Goal layer: context biases visible in traces ──────────────

        [Fact]
        public void Trace_CarriesStrategyAndGoalLabels()
        {
            var registry = LoadRegistry();
            var (battle, attacker, defender) = MakeDuelWithStats(attack: 80);
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"))
            {
                Log = new TacticalGame.AI.Debug.DecisionLog()
            };

            planner.Plan(attacker, battle);

            var context = planner.Log.Snapshot()[0].Context!;
            Assert.NotNull(context.StrategyName);
            Assert.Contains("KillUnit", context.GoalLabel);
            Assert.True(context.AggressionBias > 0.5f); // orc 0.6 + strategy bias
        }

        // ── Utility layer: per-unit action weights ────────────────────

        [Fact]
        public void PerUnitActionWeights_ChangeScoring()
        {
            var registry = LoadRegistry();
            var (battle, attacker, _) = MakeDuelWithStats(attack: 80);
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"))
            {
                Log = new TacticalGame.AI.Debug.DecisionLog()
            };
            planner.SetUnitActionWeights(attacker, registry.ActionWeights("RecklessActions")!);

            planner.Plan(attacker, battle);

            var trace = planner.Log.Snapshot()[0];
            foreach (var breakdown in trace.Candidates)
                foreach (var term in breakdown.Terms)
                    if (term.Name == "approach")
                        Assert.Equal(45f, term.Weight); // moveStep from reckless_actions.json
        }

        // ── Multithreading ────────────────────────────────────────────

        [Fact]
        public void ParallelPlanning_AssignsConsistentStrategies()
        {
            var registry = LoadRegistry();
            var battle = TestHelpers.MakeBattle(gridRadius: 5);
            var teamA = new List<Unit>();
            var teamB = new List<Unit>();

            for (int i = 0; i < 4; i++)
            {
                var a = TestHelpers.MakeUnit($"A{i}", attack: 20 + i * 20);
                var b = TestHelpers.MakeUnit($"B{i}", attack: 20 + i * 20);
                battle.PlaceUnit(a, new HexCoord(-3, i));
                battle.PlaceUnit(b, new HexCoord(3, i - 3));
                teamA.Add(a);
                teamB.Add(b);
                battle.Equip(a, new Equipment(TestHelpers.SampleWeapons.Axe));
                battle.Equip(b, new Equipment(TestHelpers.SampleWeapons.Axe));
            }
            battle.RegisterTeam(0, teamA);
            battle.RegisterTeam(1, teamB);
            battle.AdvanceTurn();

            var planner = new UtilityAIPlanner(registry, registry.Commander("KnightCaptain"));
            var all = new List<Unit>();
            all.AddRange(teamA);
            all.AddRange(teamB);

            // Plan all units in parallel — commander phase must run exactly once
            var actions = new AIAction?[all.Count];
            Parallel.For(0, all.Count, i => actions[i] = planner.Plan(all[i], battle));

            foreach (var unit in all)
                Assert.NotNull(planner.StateOf(unit).Strategy);
            Assert.All(actions, a => Assert.NotNull(a));

            // Replanning the same turn must not reassign (phase ran once)
            var before = planner.StateOf(teamA[0]).Strategy;
            planner.Plan(teamA[0], battle);
            Assert.Same(before, planner.StateOf(teamA[0]).Strategy);
        }

        [Fact]
        public void FullBattle_ThreadedBattleManager_WithUtilityPlanner_Completes()
        {
            var registry = LoadRegistry();
            var battle = TestHelpers.MakeBattle(gridRadius: 5);
            var teamA = new List<Unit>();
            var teamB = new List<Unit>();

            for (int i = 0; i < 3; i++)
            {
                var a = TestHelpers.MakeUnit($"A{i}", attack: 60);
                var b = TestHelpers.MakeUnit($"B{i}", attack: 60);
                battle.PlaceUnit(a, new HexCoord(-3, i));
                battle.PlaceUnit(b, new HexCoord(3, i - 3));
                teamA.Add(a);
                teamB.Add(b);
                battle.Equip(a, new Equipment(TestHelpers.SampleWeapons.Axe));
                battle.Equip(b, new Equipment(TestHelpers.SampleWeapons.Axe));
            }
            battle.RegisterTeam(0, teamA);
            battle.RegisterTeam(1, teamB);

            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            var manager = new BattleManager(battle, planner.Plan, useThreads: true);

            int turns = 0;
            while (!manager.IsBattleOver() && turns++ < 100)
                manager.StepTurn();

            Assert.True(manager.IsBattleOver() || turns >= 100);
            Assert.True(battle.TurnNumber > 1);
        }

        // ── Director: scripted fights ─────────────────────────────────

        [Fact]
        public void ScriptedFight_LastSurvivorsFlee()
        {
            var registry = LoadRegistry();
            var (battle, attacker, defender) = MakeDuelWithStats(attack: 80);

            // Attacker's team has 1 unit ≤ threshold 3 → must flee despite axe in hand
            var director = ScriptedFights.LastSurvivorsFlee(new[] { attacker }, threshold: 3);
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"), director);

            var action = planner.Plan(attacker, battle);

            Assert.IsType<MoveAction>(action);
            Assert.True(action!.Target.DistanceTo(defender.Position) >
                        attacker.Position.DistanceTo(defender.Position));
        }

        [Fact]
        public void ScriptedFight_NoRetreat_ForcesAttackWhenHurt()
        {
            var registry = LoadRegistry();
            var (battle, attacker, defender) = MakeDuelWithStats(attack: 80);
            battle.ChangeHP(attacker, -90); // 10% — cautious AI would flee

            var director = ScriptedFights.NoRetreat(new[] { attacker });
            var planner = new UtilityAIPlanner(registry, registry.Commander("GoblinBoss"), director);

            var action = planner.Plan(attacker, battle);

            Assert.IsType<SkillAction>(action);
            Assert.Equal(defender.Position, action!.Target);
        }

        [Fact]
        public void ScriptedFight_Vengeance_FiresWhenWatchedAllyDies()
        {
            var registry = LoadRegistry();
            var battle = TestHelpers.MakeBattle();
            var avenger = TestHelpers.MakeUnit("Avenger", attack: 80);
            var brother = TestHelpers.MakeUnit("Brother");
            var killer = TestHelpers.MakeUnit("Killer");

            battle.PlaceUnit(avenger, HexCoord.Zero);
            battle.PlaceUnit(brother, new HexCoord(0, 1));
            battle.PlaceUnit(killer, new HexCoord(1, 0));
            battle.RegisterTeam(0, new List<Unit> { avenger, brother });
            battle.RegisterTeam(1, new List<Unit> { killer });
            battle.Equip(avenger, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.AdvanceTurn();

            var director = ScriptedFights.Vengeance(avenger, watched: brother);
            var planner = new UtilityAIPlanner(registry, registry.Commander("GoblinBoss"), director);

            // Brother alive → no override, normal planning
            var calm = planner.Plan(avenger, battle);
            Assert.True(calm!.Score < float.MaxValue);

            battle.ChangeHP(brother, -100);
            battle.AdvanceTurn();

            var enraged = planner.Plan(avenger, battle);
            Assert.IsType<SkillAction>(enraged);
            Assert.Equal(float.MaxValue, enraged!.Score);
            Assert.Equal(killer.Position, enraged.Target);
        }

        [Fact]
        public void ScriptedFight_Assassination_HuntsTargetUntilDead()
        {
            var registry = LoadRegistry();
            var battle = TestHelpers.MakeBattle();
            var assassin = TestHelpers.MakeUnit("Assassin", attack: 80);
            var decoy = TestHelpers.MakeUnit("Decoy");
            var mark = TestHelpers.MakeUnit("Mark");

            battle.PlaceUnit(assassin, HexCoord.Zero);
            battle.PlaceUnit(decoy, new HexCoord(1, 0));      // adjacent, easy kill
            battle.PlaceUnit(mark, new HexCoord(0, 3));       // far away, path unblocked
            battle.RegisterTeam(0, new List<Unit> { assassin });
            battle.RegisterTeam(1, new List<Unit> { decoy, mark });
            battle.Equip(assassin, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.ChangeHP(decoy, -70); // decoy is the "obvious" target
            battle.AdvanceTurn();

            var director = ScriptedFights.Assassination(assassin, mark);
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"), director);

            // Ignores wounded adjacent decoy, moves toward the mark
            var hunt = planner.Plan(assassin, battle);
            Assert.IsType<MoveAction>(hunt);
            Assert.True(hunt!.Target.DistanceTo(mark.Position) <
                        assassin.Position.DistanceTo(mark.Position));

            // Mark dies → trigger stops, normal AI resumes (kills the decoy)
            battle.ChangeHP(mark, -100);
            battle.AdvanceTurn();
            var after = planner.Plan(assassin, battle);
            Assert.Equal(decoy.Position, after!.Target);
        }

        // ── Orders: MoveToPosition / UseSkill ─────────────────────────

        [Fact]
        public void OrderMoveTo_OverridesAttack_UntilArrival()
        {
            var registry = LoadRegistry();
            var (battle, attacker, defender) = MakeDuelWithStats(attack: 80);
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));

            // Order: fall back away from the adjacent enemy
            var rally = new HexCoord(-2, 0);
            planner.OrderMoveTo(attacker, rally, battle);

            var action = planner.Plan(attacker, battle);

            Assert.True(planner.StateOf(attacker).Goal!.IsOrder); // survived strategy assignment
            Assert.IsType<MoveAction>(action);
            Assert.True(action!.Target.DistanceTo(rally) < attacker.Position.DistanceTo(rally),
                "move must close in on the ordered position");

            // Walk the unit to the rally point, goal completes, normal AI resumes
            var cmd = action.CreateCommand(battle);
            cmd.Execute(battle);
            battle.AdvanceTurn();
            var second = planner.Plan(attacker, battle);
            cmd = second!.CreateCommand(battle);
            cmd.Execute(battle);
            Assert.Equal(rally, attacker.Position);

            battle.AdvanceTurn();
            planner.Plan(attacker, battle);

            var goal = planner.StateOf(attacker).Goal!;
            Assert.NotEqual(GoalTypes.MoveToPosition, goal.Def.Type); // order done, back to own goals
        }

        [Fact]
        public void OrderUseSkill_ForcesSkillOnOrderedTarget()
        {
            var registry = LoadRegistry();
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker", attack: 80);
            var healthy = TestHelpers.MakeUnit("Healthy");
            var wounded = TestHelpers.MakeUnit("Wounded");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(healthy, new HexCoord(1, 0));
            battle.PlaceUnit(wounded, new HexCoord(1, -1));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { healthy, wounded });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.ChangeHP(wounded, -70); // wounded is the "natural" target
            battle.AdvanceTurn();

            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            var chop = TestHelpers.SampleWeapons.Axe.GrantedSkills[0];
            planner.OrderUseSkill(attacker, chop, healthy, battle);

            var action = planner.Plan(attacker, battle);

            Assert.IsType<SkillAction>(action);
            Assert.Equal(healthy.Position, action!.Target); // order beats the easy kill
            Assert.Same(chop, ((SkillAction)action).Skill);
        }

        [Fact]
        public void OrderUseSkill_ExpiresAfterLifetime()
        {
            var registry = LoadRegistry();
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker", attack: 80);
            var healthy = TestHelpers.MakeUnit("Healthy");
            var wounded = TestHelpers.MakeUnit("Wounded");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(healthy, new HexCoord(1, 0));
            battle.PlaceUnit(wounded, new HexCoord(1, -1));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { healthy, wounded });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.ChangeHP(wounded, -70);
            battle.AdvanceTurn();

            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            var chop = TestHelpers.SampleWeapons.Axe.GrantedSkills[0];
            planner.OrderUseSkill(attacker, chop, healthy, battle);
            planner.Plan(attacker, battle);
            Assert.Equal(GoalTypes.UseSkill, planner.StateOf(attacker).Goal!.Def.Type);

            // expireTurns = 3 → after 3 turns the order lapses
            for (int i = 0; i < 3; i++) battle.AdvanceTurn();
            planner.Plan(attacker, battle);

            var goal = planner.StateOf(attacker).Goal!;
            Assert.Equal(GoalTypes.KillUnit, goal.Def.Type);
            Assert.Same(wounded, goal.TargetUnit); // back to its own judgement
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static (BattleState battle, Unit attacker, Unit defender) MakeDuelWithStats(int attack)
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
    }
}

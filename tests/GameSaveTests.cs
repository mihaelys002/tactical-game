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
    /// GameSave = BattleState + AI planner memory in one document.
    /// Verifies the planner state round-trips with full unit identity and
    /// that a loaded session continues exactly like the original.
    /// </summary>
    public class GameSaveTests
    {
        // ── Helpers ───────────────────────────────────────────────────────

        private static AIDefRegistry LoadRegistry()
            => AIDefRegistry.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Resources"));

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

        private static string Fingerprint(IEnumerable<IBattleCommand> cmds)
        {
            var parts = new List<string>();
            foreach (var cmd in cmds)
                parts.Add(cmd is CompoundCommand c && c.Skill == null
                    ? c.Unit + ": recovery" : cmd.ToString()!);
            return string.Join(" ;; ", parts);
        }

        private static List<string> RunToEnd(BattleManager m, int maxTurns = 60)
        {
            var log = new List<string>();
            for (int i = 0; i < maxTurns && !m.IsBattleOver(); i++)
                log.Add(Fingerprint(m.StepTurn()));
            return log;
        }

        private static string Snapshot(BattleState b)
        {
            var sb = new StringBuilder();
            sb.Append("turn=").Append(b.TurnNumber).Append('\n');
            foreach (var u in b.Units)
                sb.Append(u.Name)
                  .Append(" pos=").Append(u.Position)
                  .Append(" hp=").Append(u.Stats.CurrentHP)
                  .Append(" armor=").Append(u.Stats.CurrentArmor)
                  .Append(" fat=").Append(u.Stats.CurrentFatigue)
                  .Append(" mor=").Append(u.Stats.Morale)
                  .Append('\n');
            return sb.ToString();
        }

        /// Battle + planner after N turns of play, plus the save stream.
        private static (BattleState battle, UtilityAIPlanner planner, AIDefRegistry registry, MemoryStream stream)
            PlayAndSave(int turns, string commander = "OrcWarchief")
        {
            var registry = LoadRegistry();
            var battle = MakeSkirmish();
            var planner = new UtilityAIPlanner(registry, registry.Commander(commander));
            var manager = new BattleManager(battle, planner.Plan, useThreads: false);

            for (int i = 0; i < turns; i++)
                manager.StepTurn();

            var stream = new MemoryStream();
            GameSave.Save(battle, new[] { planner }, registry, stream);
            stream.Position = 0;
            return (battle, planner, registry, stream);
        }

        // ── Round-trip: battle part unchanged ─────────────────────────────

        [Fact]
        public void Load_BattleSnapshot_MatchesOriginal()
        {
            var (battle, _, registry, stream) = PlayAndSave(2);

            var (loaded, _) = GameSave.Load(stream, registry);

            Assert.Equal(Snapshot(battle), Snapshot(loaded));
        }

        [Fact]
        public void Load_UndoStillWorks()
        {
            var (_, _, registry, stream) = PlayAndSave(2);

            var (loaded, _) = GameSave.Load(stream, registry);
            var manager = new BattleManager(loaded, useThreads: false);

            Assert.True(manager.CanUndo);
            Assert.True(manager.UndoLastTurn());
            Assert.Equal(1, loaded.TurnNumber);
        }

        // ── Round-trip: planner memory ────────────────────────────────────

        [Fact]
        public void Load_CommanderName_RoundTrips()
        {
            var (_, _, registry, stream) = PlayAndSave(1, commander: "GoblinBoss");

            var (_, planners) = GameSave.Load(stream, registry);

            Assert.Single(planners);
            Assert.Equal("GoblinBoss", planners[0].CommanderName);
        }

        [Fact]
        public void Load_StrategyPreserved_AsRegistryInstance()
        {
            var (battle, planner, registry, stream) = PlayAndSave(2);

            var (loaded, planners) = GameSave.Load(stream, registry);

            for (int i = 0; i < battle.Units.Count; i++)
            {
                var original = planner.PeekState(battle.Units[i]);
                var restored = planners[0].PeekState(loaded.Units[i]);
                Assert.NotNull(original?.Strategy);
                Assert.NotNull(restored);
                Assert.Same(registry.Strategy(original!.Strategy!.Name), restored!.Strategy);
            }
        }

        [Fact]
        public void Load_GoalPreserved_WithFields()
        {
            var (battle, planner, registry, stream) = PlayAndSave(2);

            var (loaded, planners) = GameSave.Load(stream, registry);

            bool sawGoal = false;
            for (int i = 0; i < battle.Units.Count; i++)
            {
                var original = planner.PeekState(battle.Units[i])?.Goal;
                var restored = planners[0].PeekState(loaded.Units[i])?.Goal;
                if (original == null) { Assert.Null(restored); continue; }

                sawGoal = true;
                Assert.NotNull(restored);
                Assert.Same(registry.Goal(original.Def.Type), restored!.Def);
                Assert.Equal(original.StartTurn, restored.StartTurn);
                Assert.Equal(original.Score, restored.Score);
                Assert.Equal(original.IsOrder, restored.IsOrder);
                Assert.Equal(original.TargetHex, restored.TargetHex);
            }
            Assert.True(sawGoal, "scenario should have produced at least one goal");
        }

        [Fact]
        public void Load_GoalTargets_AreLoadedBattleUnits()
        {
            var (battle, planner, registry, stream) = PlayAndSave(2);

            var (loaded, planners) = GameSave.Load(stream, registry);

            bool sawTarget = false;
            for (int i = 0; i < battle.Units.Count; i++)
            {
                var original = planner.PeekState(battle.Units[i])?.Goal?.TargetUnit;
                if (original == null) continue;

                sawTarget = true;
                var restored = planners[0].PeekState(loaded.Units[i])!.Goal!.TargetUnit;
                int targetIndex = -1;
                for (int j = 0; j < battle.Units.Count; j++)
                    if (battle.Units[j] == original) { targetIndex = j; break; }
                Assert.Same(loaded.Units[targetIndex], restored);
            }
            Assert.True(sawTarget, "scenario should have produced at least one targeted goal");
        }

        [Fact]
        public void Load_Order_Survives()
        {
            var registry = LoadRegistry();
            var battle = MakeSkirmish();
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            var manager = new BattleManager(battle, planner.Plan, useThreads: false);

            manager.StepTurn();
            var destination = new HexCoord(0, 3);
            planner.OrderMoveTo(battle.Units[0], destination, battle);

            var stream = new MemoryStream();
            GameSave.Save(battle, new[] { planner }, registry, stream);
            stream.Position = 0;
            var (loaded, planners) = GameSave.Load(stream, registry);

            var goal = planners[0].PeekState(loaded.Units[0])?.Goal;
            Assert.NotNull(goal);
            Assert.True(goal!.IsOrder);
            Assert.Equal(destination, goal.TargetHex);
        }

        // ── Behavior: loaded session == original session ──────────────────

        [Fact]
        public void Continuation_MatchesOriginal_WithPlannerMemory()
        {
            var (battle, planner, registry, stream) = PlayAndSave(2);
            var originalManager = new BattleManager(battle, planner.Plan, useThreads: false);

            var (loadedBattle, planners) = GameSave.Load(stream, registry);
            var loadedManager = new BattleManager(loadedBattle, planners[0].Plan, useThreads: false);

            var originalLog = RunToEnd(originalManager);
            var loadedLog = RunToEnd(loadedManager);

            Assert.Equal(originalLog, loadedLog);
            Assert.Equal(Snapshot(battle), Snapshot(loadedBattle));
        }

        [Fact]
        public void Save_SameState_ProducesIdenticalBytes()
        {
            var registry = LoadRegistry();
            var battle = MakeSkirmish();
            var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            var manager = new BattleManager(battle, planner.Plan, useThreads: false);
            manager.StepTurn();

            var s1 = new MemoryStream();
            var s2 = new MemoryStream();
            GameSave.Save(battle, new[] { planner }, registry, s1);
            GameSave.Save(battle, new[] { planner }, registry, s2);

            Assert.Equal(
                Encoding.UTF8.GetString(s1.ToArray()),
                Encoding.UTF8.GetString(s2.ToArray()));
        }

        [Fact]
        public void MultiplePlanners_RoundTrip()
        {
            var registry = LoadRegistry();
            var battle = MakeSkirmish();
            var orc = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"));
            var goblin = new UtilityAIPlanner(registry, registry.Commander("GoblinBoss"));

            // Team-split planning: each planner only ever sees its own team.
            PlanAction split = (unit, b) => unit.TeamIndex == 0 ? orc.Plan(unit, b) : goblin.Plan(unit, b);
            var manager = new BattleManager(battle, split, useThreads: false);
            manager.StepTurn();

            var stream = new MemoryStream();
            GameSave.Save(battle, new[] { orc, goblin }, registry, stream);
            stream.Position = 0;
            var (_, planners) = GameSave.Load(stream, registry);

            Assert.Equal(2, planners.Count);
            Assert.Equal("OrcWarchief", planners[0].CommanderName);
            Assert.Equal("GoblinBoss", planners[1].CommanderName);
        }
    }
}

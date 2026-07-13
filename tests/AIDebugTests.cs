using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TacticalGame.AI;
using TacticalGame.AI.Debug;
using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class AIDebugTests
    {
        // ── ScoreBreakdown correctness ────────────────────────────────

        [Fact]
        public void Trace_TermContributions_SumToActionScore()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);
            var context = new ScoringContext
            {
                AggressionBias = 0.5f,
                AssignedTarget = defender,
                KillTargetBonus = 50f
            };

            var trace = new DecisionTrace(attacker, turnNumber: 1);
            AIBrain.DecideAction(attacker, bb, context, trace);

            Assert.NotEmpty(trace.Candidates);
            foreach (var breakdown in trace.Candidates)
            {
                float sum = 0f;
                foreach (var term in breakdown.Terms)
                    sum += term.Contribution;

                Assert.True(Math.Abs(sum - breakdown.Total) < 0.01f,
                    $"{breakdown.ActionLabel}: terms sum {sum} != score {breakdown.Total}");
            }
        }

        [Fact]
        public void Trace_MoveTerms_SumToActionScore_WhenRetreating()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);
            var context = new ScoringContext { SurvivalBias = 200f };

            var trace = new DecisionTrace(attacker, 1);
            AIBrain.DecideAction(attacker, bb, context, trace);

            bool sawMove = false;
            foreach (var breakdown in trace.Candidates)
            {
                if (breakdown.Action is not MoveAction) continue;
                sawMove = true;

                float sum = 0f;
                bool sawSurvival = false;
                foreach (var term in breakdown.Terms)
                {
                    sum += term.Contribution;
                    if (term.Name == "survivalBias") sawSurvival = true;
                }

                Assert.True(sawSurvival);
                Assert.True(Math.Abs(sum - breakdown.Total) < 0.01f);
            }
            Assert.True(sawMove);
        }

        [Fact]
        public void Trace_MarksChosenAction()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);

            var trace = new DecisionTrace(attacker, 1);
            var action = AIBrain.DecideAction(attacker, bb, new ScoringContext(), trace);

            Assert.NotNull(action);
            Assert.Same(action, trace.Chosen);
            Assert.NotNull(trace.FindBreakdown(action!));
        }

        [Fact]
        public void Trace_KillTargetBonusTerm_OnlyOnAssignedTarget()
        {
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker");
            var enemy1 = TestHelpers.MakeUnit("Enemy1");
            var enemy2 = TestHelpers.MakeUnit("Enemy2");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(enemy1, new HexCoord(1, 0));
            battle.PlaceUnit(enemy2, new HexCoord(1, -1));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { enemy1, enemy2 });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));

            var bb = new AIBlackboard(battle, attacker);
            var context = new ScoringContext { AssignedTarget = enemy2, KillTargetBonus = 200f };

            var trace = new DecisionTrace(attacker, 1);
            AIBrain.DecideAction(attacker, bb, context, trace);

            foreach (var breakdown in trace.Candidates)
            {
                if (breakdown.Action is not SkillAction) continue;

                bool hasBonus = false;
                foreach (var term in breakdown.Terms)
                    if (term.Name == "killTargetBonus") hasBonus = true;

                Assert.Equal(breakdown.Action.Target == enemy2.Position, hasBonus);
            }
        }

        // ── DecisionLog ───────────────────────────────────────────────

        [Fact]
        public void DecisionLog_ThreadSafe_UnderParallelAdd()
        {
            var log = new DecisionLog();
            var unit = TestHelpers.MakeUnit();

            Parallel.For(0, 200, i => log.Add(new DecisionTrace(unit, i)));

            Assert.Equal(200, log.Snapshot().Count);
        }

        [Fact]
        public void AIPlanner_WithLog_RecordsTrace()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var planner = new AIPlanner { Log = new DecisionLog() };

            var action = planner.Plan(attacker, battle);

            var traces = planner.Log.Snapshot();
            Assert.NotNull(action);
            Assert.Single(traces);
            Assert.Same(attacker, traces[0].Unit);
            Assert.Same(action, traces[0].Chosen);
        }

        // ── Formatter ─────────────────────────────────────────────────

        [Fact]
        public void Format_ShowsChosenMarkerAndTerms()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var planner = new AIPlanner { Log = new DecisionLog() };
            planner.Plan(attacker, battle);

            var text = DecisionFormatter.Format(planner.Log.Snapshot()[0]);

            Assert.Contains("Attacker", text);
            Assert.Contains(">", text);          // chosen marker
            Assert.Contains("basePower", text);
        }

        [Fact]
        public void Diff_FlagsBiggestGapTerm()
        {
            var unit = TestHelpers.MakeUnit();

            var chosenAction = AIAction.Move(unit, new HexCoord(1, 0), 122f);
            var chosen = new ScoreBreakdown(chosenAction, "Slash → Orc", new List<ScoreTerm>
            {
                new("enemyHpMissing", 0.7f, 100f, 70f),
                new("aggressionBias", 0.5f, 104f, 52f),
            });

            var wantedAction = AIAction.Move(unit, new HexCoord(-1, 0), 41f);
            var wanted = new ScoreBreakdown(wantedAction, "Retreat West", new List<ScoreTerm>
            {
                new("retreatDistanceGain", 2f, 30f, 60f),
                new("survivalBias", 1f, -19f, -19f),
            });

            var text = DecisionFormatter.Diff(chosen, wanted);

            Assert.Contains("biggest gap", text);
            // enemyHpMissing: 70 vs 0 = gap 70 — the largest chosen-over-wanted term
            var lines = text.Split('\n');
            foreach (var line in lines)
                if (line.Contains("biggest gap"))
                    Assert.Contains("enemyHpMissing", line);
        }

        // ── DecisionHistory (Tier 3 replay) ───────────────────────────

        [Fact]
        public void DecisionHistory_StoresAndFindsPerTurn()
        {
            var history = new DecisionHistory();
            var unitA = TestHelpers.MakeUnit("A");
            var unitB = TestHelpers.MakeUnit("B");

            history.Store(1, new List<DecisionTrace> { new(unitA, 1) });
            history.Store(2, new List<DecisionTrace> { new(unitA, 2), new(unitB, 2) });

            Assert.Equal(2, history.LatestTurn);
            Assert.Single(history.GetTurn(1)!);
            Assert.Equal(2, history.GetTurn(2)!.Count);
            Assert.Same(unitB, history.Find(2, unitB)!.Unit);
            Assert.Null(history.Find(1, unitB));
            Assert.Null(history.GetTurn(99));
        }

        [Fact]
        public void DecisionHistory_KeepsPastTurns_ForReplayAfterUndo()
        {
            var history = new DecisionHistory();
            var unit = TestHelpers.MakeUnit();

            for (int turn = 1; turn <= 5; turn++)
                history.Store(turn, new List<DecisionTrace> { new(unit, turn) });

            // Undo rewinds battle state, but archived decisions stay browsable
            Assert.NotNull(history.GetTurn(3));
            Assert.Equal(3, history.GetTurn(3)![0].TurnNumber);
            Assert.Equal(5, history.LatestTurn);
        }

        [Fact]
        public void AIPlanner_AttachesContextToTrace()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var planner = new AIPlanner { Log = new DecisionLog() };

            planner.Plan(attacker, battle);

            var trace = planner.Log.Snapshot()[0];
            Assert.NotNull(trace.Context);
            Assert.Equal(UnitRole.Attacker, trace.Context!.Role);
        }

        // ── Scenario: tuning harness style ────────────────────────────

        [Fact]
        public void Scenario_FinishesLowHpEnemy()
        {
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker");
            var healthy = TestHelpers.MakeUnit("Healthy");
            var wounded = TestHelpers.MakeUnit("Wounded");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(healthy, new HexCoord(1, 0));
            battle.PlaceUnit(wounded, new HexCoord(1, -1));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { healthy, wounded });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.ChangeHP(wounded, -90); // 10% HP

            var bb = new AIBlackboard(battle, attacker);
            var action = AIBrain.DecideAction(attacker, bb, new ScoringContext());

            Assert.NotNull(action);
            Assert.IsType<SkillAction>(action);
            Assert.Equal(wounded.Position, action.Target);
        }
    }
}

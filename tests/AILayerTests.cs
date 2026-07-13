using System.Collections.Generic;
using TacticalGame.AI;
using TacticalGame.AI.Layers;
using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class AILayerTests
    {
        // ── ScoringContext biases ──────────────────────────────────────

        [Fact]
        public void AggressionBias_IncreasesAttackScore()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);

            var neutral = AIBrain.DecideAction(attacker, bb, new ScoringContext());
            var aggressive = AIBrain.DecideAction(attacker, bb,
                new ScoringContext { AggressionBias = 1f });

            Assert.NotNull(neutral);
            Assert.NotNull(aggressive);
            Assert.IsType<SkillAction>(neutral);
            Assert.IsType<SkillAction>(aggressive);
            Assert.True(aggressive.Score > neutral.Score);
        }

        [Fact]
        public void KillTargetBonus_FavorsAssignedTarget()
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
            var context = new ScoringContext
            {
                AssignedTarget = enemy2,
                KillTargetBonus = 200f
            };

            var action = AIBrain.DecideAction(attacker, bb, context);

            Assert.NotNull(action);
            Assert.Equal(enemy2.Position, action.Target);
        }

        [Fact]
        public void SurvivalBias_PrefersMovingAway()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);

            var context = new ScoringContext { SurvivalBias = 200f };
            var action = AIBrain.DecideAction(attacker, bb, context);

            Assert.NotNull(action);
            Assert.IsType<MoveAction>(action);
            // Should move away from defender
            Assert.True(action.Target.DistanceTo(defender.Position) >
                         attacker.Position.DistanceTo(defender.Position));
        }

        // ── DefaultCommanderLayer ─────────────────────────────────────

        [Fact]
        public void DefaultCommander_WritesFieldsToContext()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);

            var commander = new DefaultCommanderLayer
            {
                AggressionBias = 0.5f,
                PriorityTarget = defender,
                ForceRetreatUnit = attacker
            };

            var context = new ScoringContext();
            commander.Evaluate(bb, context);

            Assert.Equal(0.5f, context.AggressionBias);
            Assert.Equal(defender, context.PriorityTarget);
            Assert.Equal(attacker, context.ForceRetreatUnit);
        }

        // ── DefaultStrategyLayer ──────────────────────────────────────

        [Fact]
        public void DefaultStrategy_AssignsRetreating_WhenLowHP()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            battle.ChangeHP(attacker, -85); // 15% HP

            var bb = new AIBlackboard(battle, attacker);
            var context = new ScoringContext();

            new DefaultStrategyLayer().Evaluate(attacker, bb, context);

            Assert.Equal(UnitRole.Retreating, context.Role);
            Assert.True(context.ShouldRetreat);
        }

        [Fact]
        public void DefaultStrategy_AssignsAttacker_WhenHealthy()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);
            var context = new ScoringContext();

            new DefaultStrategyLayer().Evaluate(attacker, bb, context);

            Assert.Equal(UnitRole.Attacker, context.Role);
            Assert.False(context.ShouldRetreat);
        }

        [Fact]
        public void DefaultStrategy_ForceRetreat_OverridesHealth()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);

            var context = new ScoringContext { ForceRetreatUnit = attacker };
            new DefaultStrategyLayer().Evaluate(attacker, bb, context);

            Assert.Equal(UnitRole.Retreating, context.Role);
            Assert.True(context.ShouldRetreat);
        }

        [Fact]
        public void DefaultStrategy_PropagatesPriorityTarget()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);

            var context = new ScoringContext { PriorityTarget = defender };
            new DefaultStrategyLayer().Evaluate(attacker, bb, context);

            Assert.Equal(defender, context.AssignedTarget);
        }

        // ── DefaultGoalLayer ──────────────────────────────────────────

        [Fact]
        public void DefaultGoal_SetsKillBonus_WhenTargetAssigned()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);

            var context = new ScoringContext { AssignedTarget = defender };
            new DefaultGoalLayer().Evaluate(attacker, bb, context);

            Assert.True(context.KillTargetBonus > 0);
        }

        [Fact]
        public void DefaultGoal_SetsSurvivalBias_WhenRetreating()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);

            var context = new ScoringContext { Role = UnitRole.Retreating };
            new DefaultGoalLayer().Evaluate(attacker, bb, context);

            Assert.True(context.SurvivalBias > 0);
        }

        // ── AIPlanner end-to-end ──────────────────────────────────────

        [Fact]
        public void AIPlanner_ProducesAction_WithDefaultLayers()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var planner = new AIPlanner();

            var action = planner.Plan(attacker, battle);

            Assert.NotNull(action);
        }

        [Fact]
        public void AIPlanner_AggressiveCommander_StillAttacks()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var planner = new AIPlanner(
                commander: new DefaultCommanderLayer { AggressionBias = 1f });

            var action = planner.Plan(attacker, battle);

            Assert.NotNull(action);
            Assert.IsType<SkillAction>(action);
        }

        [Fact]
        public void AIPlanner_ForceRetreat_MakesUnitFlee()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var planner = new AIPlanner(
                commander: new DefaultCommanderLayer { ForceRetreatUnit = attacker });

            var action = planner.Plan(attacker, battle);

            Assert.NotNull(action);
            Assert.IsType<MoveAction>(action);
            Assert.True(action.Target.DistanceTo(defender.Position) >
                         attacker.Position.DistanceTo(defender.Position));
        }

        // ── Director override ─────────────────────────────────────────

        [Fact]
        public void Director_OverridesChain()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();

            var director = new ScriptedDirector();
            var forcedTarget = new HexCoord(-1, 0);
            director.AddTrigger(new AlwaysMoveTrigger(attacker, forcedTarget));

            var planner = new AIPlanner(director: director);
            var action = planner.Plan(attacker, battle);

            Assert.NotNull(action);
            Assert.IsType<MoveAction>(action);
            Assert.Equal(forcedTarget, action.Target);
        }

        [Fact]
        public void Director_FiresOnlyOnce()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();

            var director = new ScriptedDirector();
            director.AddTrigger(new AlwaysMoveTrigger(attacker, new HexCoord(-1, 0)));

            var planner = new AIPlanner(director: director);

            var first = planner.Plan(attacker, battle);
            Assert.IsType<MoveAction>(first);

            var second = planner.Plan(attacker, battle);
            // After trigger fired, should fall through to normal scoring
            Assert.IsType<SkillAction>(second);
        }

        [Fact]
        public void Director_DoesNotAffectOtherUnits()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            battle.Equip(defender, new Equipment(TestHelpers.SampleWeapons.Axe));

            var director = new ScriptedDirector();
            director.AddTrigger(new AlwaysMoveTrigger(attacker, new HexCoord(-1, 0)));

            var planner = new AIPlanner(director: director);

            // Defender should not be affected by attacker's trigger
            var defenderAction = planner.Plan(defender, battle);
            Assert.IsType<SkillAction>(defenderAction);
        }

        // ── TeamPlanner routing ───────────────────────────────────────

        [Fact]
        public void TeamPlanner_RoutesToCorrectPlanner()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();

            var aggressivePlanner = new AIPlanner(
                commander: new DefaultCommanderLayer { AggressionBias = 1f });
            var retreatPlanner = new AIPlanner(
                commander: new DefaultCommanderLayer { ForceRetreatUnit = defender });

            var teamPlanner = new TeamPlanner(aggressivePlanner);
            teamPlanner.SetTeamPlanner(1, retreatPlanner);

            // Team 0 (attacker) gets aggressive planner
            var attackerAction = teamPlanner.Plan(attacker, battle);
            Assert.NotNull(attackerAction);
            Assert.IsType<SkillAction>(attackerAction);

            // Team 1 (defender) gets retreat planner — needs a weapon to have alternatives
            battle.Equip(defender, new Equipment(TestHelpers.SampleWeapons.Axe));
            var defenderAction = teamPlanner.Plan(defender, battle);
            Assert.NotNull(defenderAction);
            Assert.IsType<MoveAction>(defenderAction);
        }

        // ── Test helpers ──────────────────────────────────────────────

        private class AlwaysMoveTrigger : DirectorTrigger
        {
            private readonly HexCoord _target;

            public AlwaysMoveTrigger(Unit unit, HexCoord target) : base(unit)
            {
                _target = target;
            }

            public override bool ShouldFire(AIBlackboard blackboard) => true;

            public override AIAction CreateAction(AIBlackboard blackboard)
            {
                return AIAction.Move(Unit, _target, float.MaxValue);
            }
        }
    }
}

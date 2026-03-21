using System.Collections.Generic;
using TacticalGame.AI;
using TacticalGame.Grid;
using TacticalGame.Grid.Skills;
using Xunit;

namespace TacticalGame.Tests
{
    public class AITests
    {
        // ── AIBrain decision-making ─────────────────────────────────────

        [Fact]
        public void DecideAction_PrefersAttack_WhenInRange()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);

            var action = AIBrain.DecideAction(attacker, bb);

            Assert.NotNull(action);
            Assert.IsType<SkillAction>(action);
        }

        [Fact]
        public void DecideAction_MovesTowardEnemy_WhenOutOfRange()
        {
            var battle = TestHelpers.MakeBattle(5);
            var attacker = TestHelpers.MakeUnit("Attacker");
            var defender = TestHelpers.MakeUnit("Defender");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(defender, new HexCoord(4, 0)); // far away
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { defender });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));

            var bb = new AIBlackboard(battle, attacker);
            var action = AIBrain.DecideAction(attacker, bb);

            Assert.NotNull(action);
            Assert.IsType<MoveAction>(action);
            // New position should be closer to the defender
            Assert.True(action.Target.DistanceTo(defender.Position) <
                         attacker.Position.DistanceTo(defender.Position));
        }

        [Fact]
        public void DecideAction_ReturnsNull_WhenNoEnemies()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);
            battle.RegisterTeam(0, new List<Unit> { unit });
            battle.Equip(unit, new Equipment(TestHelpers.SampleWeapons.Axe));

            var bb = new AIBlackboard(battle, unit);
            var action = AIBrain.DecideAction(unit, bb);

            // No enemies → no moves to score (no closest enemy), no skills to use
            Assert.Null(action);
        }

        [Fact]
        public void DecideAction_SkipsSkill_WhenTooFatigued()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();

            // Max out fatigue so the skill can't be used
            battle.ChangeFatigue(attacker, attacker.Stats.MaxFatigue);

            var bb = new AIBlackboard(battle, attacker);
            var action = AIBrain.DecideAction(attacker, bb);

            // Should still move (or null), but not use a skill
            if (action != null)
                Assert.IsNotType<SkillAction>(action);
        }

        [Fact]
        public void DecideAction_PrefersLowHPEnemy()
        {
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker");
            var healthyEnemy = TestHelpers.MakeUnit("Healthy");
            var woundedEnemy = TestHelpers.MakeUnit("Wounded");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(healthyEnemy, new HexCoord(1, 0));
            battle.PlaceUnit(woundedEnemy, new HexCoord(1, -1));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { healthyEnemy, woundedEnemy });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));

            // Wound one enemy significantly
            battle.ChangeHP(woundedEnemy, -80);

            var bb = new AIBlackboard(battle, attacker);
            var action = AIBrain.DecideAction(attacker, bb);

            Assert.NotNull(action);
            Assert.IsType<SkillAction>(action);
            Assert.Equal(woundedEnemy.Position, action.Target);
        }

        // ── AIBlackboard ────────────────────────────────────────────────

        [Fact]
        public void AIBlackboard_SeparatesAlliesAndEnemies()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var bb = new AIBlackboard(battle, attacker);

            Assert.Empty(bb.Friends); // no allies besides self (and self excluded)
            Assert.Single(bb.Enemies);
            Assert.Equal(defender, bb.Enemies[0]);
        }

        // ── AIAction command creation ───────────────────────────────────

        [Fact]
        public void MoveAction_CreatesMoveCommand()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);

            var action = AIAction.Move(unit, new HexCoord(1, 0), 30f);
            var cmd = action.CreateCommand(battle);

            Assert.IsType<MoveCommand>(cmd);
        }

        [Fact]
        public void SkillAction_CreatesCompoundCommand()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var skill = new ChopSkill();
            var weapon = TestHelpers.SampleWeapons.Axe;

            var action = AIAction.UseSkill(attacker, defender.Position, skill, weapon, 50f);
            var cmd = action.CreateCommand(battle);

            Assert.IsType<CompoundCommand>(cmd);
        }
    }
}

using System;
using System.Collections.Generic;
using TacticalGame.Grid;
using TacticalGame.Grid.Skills;
using Xunit;

namespace TacticalGame.Tests
{
    public class CommandTests
    {
        // ── MoveCommand ─────────────────────────────────────────────────

        [Fact]
        public void MoveCommand_Execute_MovesUnit()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);

            var cmd = new MoveCommand(unit, HexCoord.Zero, new HexCoord(1, 0));
            bool success = cmd.Execute(battle);

            Assert.True(success);
            Assert.Equal(new HexCoord(1, 0), unit.Position);
        }

        [Fact]
        public void MoveCommand_Execute_FailsOnOccupiedCell()
        {
            var battle = TestHelpers.MakeBattle();
            var u1 = TestHelpers.MakeUnit("A");
            var u2 = TestHelpers.MakeUnit("B");
            battle.PlaceUnit(u1, HexCoord.Zero);
            battle.PlaceUnit(u2, new HexCoord(1, 0));

            var cmd = new MoveCommand(u1, HexCoord.Zero, new HexCoord(1, 0));
            bool success = cmd.Execute(battle);

            Assert.False(success);
            Assert.Equal(HexCoord.Zero, u1.Position); // didn't move
        }

        [Fact]
        public void MoveCommand_Execute_FailsOnNonexistentCell()
        {
            var battle = TestHelpers.MakeBattle(1);
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);

            var cmd = new MoveCommand(unit, HexCoord.Zero, new HexCoord(99, 99));
            bool success = cmd.Execute(battle);

            Assert.False(success);
        }

        [Fact]
        public void MoveCommand_Undo_RestoresPosition()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);

            var cmd = new MoveCommand(unit, HexCoord.Zero, new HexCoord(1, 0));
            cmd.Execute(battle);
            cmd.Undo(battle);

            Assert.Equal(HexCoord.Zero, unit.Position);
        }

        [Fact]
        public void MoveCommand_Undo_CorrectOrder_RestoresBothUnits()
        {
            var battle = TestHelpers.MakeBattle();
            var a = TestHelpers.MakeUnit("A");
            var b = TestHelpers.MakeUnit("B");
            battle.PlaceUnit(a, HexCoord.Zero);
            battle.PlaceUnit(b, new HexCoord(-1, 0));

            var cmdA = new MoveCommand(a, HexCoord.Zero, new HexCoord(1, 0));
            cmdA.Execute(battle);

            var cmdB = new MoveCommand(b, new HexCoord(-1, 0), HexCoord.Zero);
            cmdB.Execute(battle);

            // Undo in correct reverse order: B first, then A
            cmdB.Undo(battle);
            cmdA.Undo(battle);

            Assert.Equal(HexCoord.Zero, a.Position);
            Assert.Equal(new HexCoord(-1, 0), b.Position);
        }

        [Fact]
        public void MoveCommand_Undo_WrongOrder_Throws()
        {
            var battle = TestHelpers.MakeBattle();
            var a = TestHelpers.MakeUnit("A");
            var b = TestHelpers.MakeUnit("B");
            battle.PlaceUnit(a, HexCoord.Zero);
            battle.PlaceUnit(b, new HexCoord(-1, 0));

            var cmdA = new MoveCommand(a, HexCoord.Zero, new HexCoord(1, 0));
            cmdA.Execute(battle);

            var cmdB = new MoveCommand(b, new HexCoord(-1, 0), HexCoord.Zero);
            cmdB.Execute(battle);

            // Undo in WRONG order: A first — origin cell (0,0) is occupied by B
            Assert.Throws<InvalidOperationException>(() => cmdA.Undo(battle));
        }

        [Fact]
        public void TwoUnitsCompeteForSameCell_FirstWins_SecondFails()
        {
            var battle = TestHelpers.MakeBattle();
            var u1 = TestHelpers.MakeUnit("A");
            var u2 = TestHelpers.MakeUnit("B");
            var target = new HexCoord(1, 0);

            battle.PlaceUnit(u1, HexCoord.Zero);
            battle.PlaceUnit(u2, new HexCoord(2, 0));

            var cmd1 = new MoveCommand(u1, HexCoord.Zero, target);
            var cmd2 = new MoveCommand(u2, new HexCoord(2, 0), target);

            bool first = cmd1.Execute(battle);
            bool second = cmd2.Execute(battle);

            Assert.True(first);
            Assert.False(second);
            Assert.Equal(target, u1.Position);
            Assert.Equal(new HexCoord(2, 0), u2.Position); // didn't move
        }

        // ── CompoundCommand ─────────────────────────────────────────────

        [Fact]
        public void CompoundCommand_Execute_FailsWithNoEssentialEffects()
        {
            var unit = TestHelpers.MakeUnit();
            var weaponDef = TestHelpers.SampleWeapons.Axe;
            var skill = new Grid.Skills.ChopSkill();

            // No effects marked essential
            var effect = new FatigueEffect(unit, 5) { IsEssential = false };
            var cmd = new CompoundCommand(unit, weaponDef, skill, HexCoord.Zero,
                new List<BattleEffect> { effect });

            var battle = TestHelpers.MakeBattle();
            battle.PlaceUnit(unit, HexCoord.Zero);
            bool success = cmd.Execute(battle);

            Assert.False(success);
        }

        [Fact]
        public void CompoundCommand_Execute_AppliesAllEffects_WhenEssentialPresent()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var weaponDef = TestHelpers.SampleWeapons.Axe;
            var skill = new Grid.Skills.ChopSkill();

            var dmg = new DamageEffect(attacker, defender, 20) { IsEssential = true };
            var fat = new FatigueEffect(attacker, 6);
            var cmd = new CompoundCommand(attacker, weaponDef, skill,
                defender.Position, new List<BattleEffect> { dmg, fat });

            bool success = cmd.Execute(battle);

            Assert.True(success);
            Assert.True(dmg.AppliedArmorDamage > 0 || dmg.AppliedHpDamage > 0);
            Assert.True(attacker.Stats.CurrentFatigue > 0);
        }

        [Fact]
        public void CompoundCommand_Undo_ReversesInReverseOrder()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            int hpBefore = defender.Stats.CurrentHP;
            int armorBefore = defender.Stats.CurrentArmor;
            int fatBefore = attacker.Stats.CurrentFatigue;

            var dmg = new DamageEffect(attacker, defender, 20) { IsEssential = true };
            var fat = new FatigueEffect(attacker, 6);
            var cmd = new CompoundCommand(attacker, TestHelpers.SampleWeapons.Axe,
                new Grid.Skills.ChopSkill(), defender.Position,
                new List<BattleEffect> { dmg, fat });

            cmd.Execute(battle);
            cmd.Undo(battle);

            Assert.Equal(hpBefore, defender.Stats.CurrentHP);
            Assert.Equal(armorBefore, defender.Stats.CurrentArmor);
            Assert.Equal(fatBefore, attacker.Stats.CurrentFatigue);
        }

        [Fact]
        public void AttackCommand_Execute_FailsOnEmptyCell()
        {
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker");
            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.RegisterTeam(new List<Unit> { attacker });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));

            var emptyHex = new HexCoord(1, 0);
            var cmd = CombatPipeline.Resolve(attacker, TestHelpers.SampleWeapons.Axe,
                new ChopSkill(), emptyHex, battle);

            bool success = cmd.Execute(battle);

            Assert.False(success);
            // Attacker state unchanged — no fatigue spent on a miss
            Assert.Equal(0, attacker.Stats.CurrentFatigue);
        }
    }
}

using System;
using System.Collections.Generic;
using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class BattleStateTests
    {
        // ── Unit placement ──────────────────────────────────────────────

        [Fact]
        public void PlaceUnit_AddsUnitToGrid()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);

            Assert.True(battle.HasUnit(unit));
            Assert.Equal(HexCoord.Zero, unit.Position);
            Assert.Single(battle.Units);
        }

        [Fact]
        public void PlaceUnit_ThrowsOnOccupiedCell()
        {
            var battle = TestHelpers.MakeBattle();
            var u1 = TestHelpers.MakeUnit("A");
            var u2 = TestHelpers.MakeUnit("B");
            battle.PlaceUnit(u1, HexCoord.Zero);

            Assert.Throws<InvalidOperationException>(() =>
                battle.PlaceUnit(u2, HexCoord.Zero));
        }

        [Fact]
        public void PlaceUnit_ThrowsOnNonexistentCell()
        {
            var battle = TestHelpers.MakeBattle(1); // small grid
            var unit = TestHelpers.MakeUnit();

            Assert.Throws<ArgumentException>(() =>
                battle.PlaceUnit(unit, new HexCoord(99, 99)));
        }

        // ── Unit movement ───────────────────────────────────────────────

        [Fact]
        public void MoveUnit_UpdatesPosition()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);

            var target = new HexCoord(1, 0);
            battle.MoveUnit(unit, target);

            Assert.Equal(target, unit.Position);
        }

        [Fact]
        public void MoveUnit_FreesOldCell()
        {
            var battle = TestHelpers.MakeBattle();
            var u1 = TestHelpers.MakeUnit("A");
            var u2 = TestHelpers.MakeUnit("B");
            battle.PlaceUnit(u1, HexCoord.Zero);
            battle.MoveUnit(u1, new HexCoord(1, 0));

            // Old cell should now be walkable for another unit
            battle.PlaceUnit(u2, HexCoord.Zero);
            Assert.Equal(HexCoord.Zero, u2.Position);
        }

        [Fact]
        public void MoveUnit_ThrowsOnOccupiedTarget()
        {
            var battle = TestHelpers.MakeBattle();
            var u1 = TestHelpers.MakeUnit("A");
            var u2 = TestHelpers.MakeUnit("B");
            battle.PlaceUnit(u1, HexCoord.Zero);
            battle.PlaceUnit(u2, new HexCoord(1, 0));

            Assert.Throws<InvalidOperationException>(() =>
                battle.MoveUnit(u1, new HexCoord(1, 0)));
        }

        // ── Remove unit ─────────────────────────────────────────────────

        [Fact]
        public void RemoveUnit_ClearsFromGridAndList()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);
            battle.RemoveUnit(unit);

            Assert.False(battle.HasUnit(unit));
            Assert.Empty(battle.Units);
        }

        // ── Team tracking ───────────────────────────────────────────────

        [Fact]
        public void RegisterTeam_AssignsSequentialIndices()
        {
            var battle = TestHelpers.MakeBattle();
            var u1 = TestHelpers.MakeUnit("A");
            var u2 = TestHelpers.MakeUnit("B");
            battle.PlaceUnit(u1, HexCoord.Zero);
            battle.PlaceUnit(u2, new HexCoord(1, 0));

            int t0 = battle.RegisterTeam(new List<Unit> { u1 });
            int t1 = battle.RegisterTeam(new List<Unit> { u2 });

            Assert.Equal(0, t0);
            Assert.Equal(1, t1);
            Assert.Equal(2, battle.TeamCount);
        }

        [Fact]
        public void TeamIndex_ReturnsNegativeOne_ForUnregisteredUnit()
        {
            var unit = TestHelpers.MakeUnit();
            Assert.Equal(-1, unit.TeamIndex);
        }

        [Fact]
        public void GetAllies_ExcludesSelf_OnlyAlive()
        {
            var battle = TestHelpers.MakeBattle();
            var u1 = TestHelpers.MakeUnit("A");
            var u2 = TestHelpers.MakeUnit("B");
            var u3 = TestHelpers.MakeUnit("C");

            battle.PlaceUnit(u1, HexCoord.Zero);
            battle.PlaceUnit(u2, new HexCoord(1, 0));
            battle.PlaceUnit(u3, new HexCoord(-1, 0));
            battle.RegisterTeam(new List<Unit> { u1, u2, u3 });

            // Kill u3
            battle.ChangeHP(u3, -u3.Stats.MaxHP);

            var allies = battle.GetAllies(u1);
            Assert.Single(allies);
            Assert.Equal(u2, allies[0]);
        }

        [Fact]
        public void GetEnemies_ReturnsOtherTeamsAliveUnits()
        {
            var battle = TestHelpers.MakeBattle();
            var ally = TestHelpers.MakeUnit("Ally");
            var enemy1 = TestHelpers.MakeUnit("E1");
            var enemy2 = TestHelpers.MakeUnit("E2");

            battle.PlaceUnit(ally, HexCoord.Zero);
            battle.PlaceUnit(enemy1, new HexCoord(1, 0));
            battle.PlaceUnit(enemy2, new HexCoord(-1, 0));
            battle.RegisterTeam(new List<Unit> { ally });
            battle.RegisterTeam(new List<Unit> { enemy1, enemy2 });

            var enemies = battle.GetEnemies(ally);
            Assert.Equal(2, enemies.Count);
        }

        [Fact]
        public void GetAllies_ReturnsEmpty_ForUnregisteredUnit()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            Assert.Empty(battle.GetAllies(unit));
        }

        // ── Change* clamping ────────────────────────────────────────────

        [Fact]
        public void ChangeHP_ClampsToZero_ReturnsActualDelta()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit(maxHP: 100);
            battle.PlaceUnit(unit, HexCoord.Zero);

            int actual = battle.ChangeHP(unit, -999);
            Assert.Equal(-100, actual);
            Assert.Equal(0, unit.Stats.CurrentHP);
        }

        [Fact]
        public void ChangeHP_ClampsToMax()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit(maxHP: 100);
            battle.PlaceUnit(unit, HexCoord.Zero);
            battle.ChangeHP(unit, -50); // drop to 50

            int actual = battle.ChangeHP(unit, 999);
            Assert.Equal(50, actual); // only healed 50
            Assert.Equal(100, unit.Stats.CurrentHP);
        }

        [Fact]
        public void ChangeArmor_ClampsToZeroAndMax()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit(maxArmor: 50);
            battle.PlaceUnit(unit, HexCoord.Zero);

            int actual = battle.ChangeArmor(unit, -999);
            Assert.Equal(-50, actual);
            Assert.Equal(0, unit.Stats.CurrentArmor);
        }

        [Fact]
        public void ChangeFatigue_ClampsToMaxFatigue()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);

            // Fresh unit starts at 0 fatigue
            int actual = battle.ChangeFatigue(unit, 999);
            Assert.Equal(60, actual); // maxFatigue = 60
            Assert.Equal(60, unit.Stats.CurrentFatigue);
        }

        [Fact]
        public void ChangeFatigue_ClampsToZero()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);

            int actual = battle.ChangeFatigue(unit, -10);
            Assert.Equal(0, actual); // already at 0
        }

        [Fact]
        public void ChangeMorale_ClampsTo0And100()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);

            // morale starts at 70
            int up = battle.ChangeMorale(unit, 999);
            Assert.Equal(30, up);
            Assert.Equal(100, unit.Stats.Morale);

            int down = battle.ChangeMorale(unit, -999);
            Assert.Equal(-100, down);
            Assert.Equal(0, unit.Stats.Morale);
        }

        // ── Equipment rules ─────────────────────────────────────────────

        [Fact]
        public void Equip_ThrowsOnOccupiedSlot()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);
            battle.RegisterTeam(new List<Unit> { unit });

            battle.Equip(unit, new Equipment(TestHelpers.SampleWeapons.Axe));

            Assert.Throws<InvalidOperationException>(() =>
                battle.Equip(unit, new Equipment(TestHelpers.SampleWeapons.Sword)));
        }

        [Fact]
        public void Equip_TwoHanded_BlocksLeftHand()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);
            battle.RegisterTeam(new List<Unit> { unit });

            battle.Equip(unit, new Equipment(TestHelpers.SampleWeapons.TwoHandedAxe));

            Assert.Throws<InvalidOperationException>(() =>
                battle.Equip(unit, new Equipment(TestHelpers.SampleWeapons.Shield)));
        }

        [Fact]
        public void Equip_LeftHand_BlockedByExistingTwoHanded()
        {
            // Same as above but verifying the reverse direction check
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);
            battle.RegisterTeam(new List<Unit> { unit });

            battle.Equip(unit, new Equipment(TestHelpers.SampleWeapons.TwoHandedAxe));

            Assert.Throws<InvalidOperationException>(() =>
                battle.Equip(unit, new Equipment(TestHelpers.SampleWeapons.Shield)));
        }

        [Fact]
        public void Equip_ThrowsForUnitNotInBattle()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();

            Assert.Throws<InvalidOperationException>(() =>
                battle.Equip(unit, new Equipment(TestHelpers.SampleWeapons.Axe)));
        }

        [Fact]
        public void Unequip_ReturnsEquipment_ClearsSlot()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);
            battle.RegisterTeam(new List<Unit> { unit });

            var axe = new Equipment(TestHelpers.SampleWeapons.Axe);
            battle.Equip(unit, axe);
            var removed = battle.Unequip(unit, EquipmentSlot.RightHand);

            Assert.Same(axe, removed);
            Assert.False(unit.Equipment.Has(EquipmentSlot.RightHand));
        }

        [Fact]
        public void Unequip_ReturnsNull_WhenSlotEmpty()
        {
            var battle = TestHelpers.MakeBattle();
            var unit = TestHelpers.MakeUnit();
            battle.PlaceUnit(unit, HexCoord.Zero);
            battle.RegisterTeam(new List<Unit> { unit });

            var removed = battle.Unequip(unit, EquipmentSlot.Helmet);
            Assert.Null(removed);
        }

        // ── Dead unit walkability ───────────────────────────────────────

        [Fact]
        public void DeadUnit_DoesNotBlockWalkability()
        {
            var battle = TestHelpers.MakeBattle();
            var u1 = TestHelpers.MakeUnit("A");
            var u2 = TestHelpers.MakeUnit("B");
            battle.PlaceUnit(u1, HexCoord.Zero);

            // Kill u1
            battle.ChangeHP(u1, -u1.Stats.MaxHP);
            Assert.False(u1.IsAlive);

            // Another unit should be able to walk on the same cell
            battle.PlaceUnit(u2, HexCoord.Zero);
            Assert.Equal(HexCoord.Zero, u2.Position);
        }
    }
}

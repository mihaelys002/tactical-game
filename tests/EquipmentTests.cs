using System;
using System.Linq;
using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class EquipmentTests
    {
        // ── Effective stats ─────────────────────────────────────────────

        [Fact]
        public void EffectiveAttack_IncludesEquipmentBonus()
        {
            var (battle, attacker, _) = TestHelpers.MakeDuel();
            // Axe gives +10 attack, base attack is 10
            Assert.Equal(20, attacker.EffectiveAttack);
        }

        [Fact]
        public void EffectiveDefense_IncludesEquipmentBonus()
        {
            var (battle, attacker, _) = TestHelpers.MakeDuel(
                attackerWeapon: TestHelpers.SampleWeapons.Sword);
            // Sword gives +3 defense, base defense is 10
            Assert.Equal(13, attacker.EffectiveDefense);
        }

        [Fact]
        public void EffectiveStats_StackFromMultipleSlots()
        {
            var (battle, attacker, _) = TestHelpers.MakeDuel(
                attackerWeapon: TestHelpers.SampleWeapons.Sword);
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Shield));

            // Sword defense +3, Shield defense +15
            Assert.Equal(28, attacker.EffectiveDefense);
        }

        [Fact]
        public void EffectiveStats_NoEquipment_EqualsBaseStats()
        {
            var unit = TestHelpers.MakeUnit(attack: 15, defense: 12);
            Assert.Equal(15, unit.EffectiveAttack);
            Assert.Equal(12, unit.EffectiveDefense);
        }

        // ── StatBonus addition ──────────────────────────────────────────

        [Fact]
        public void StatBonus_Addition_CombinesAllFields()
        {
            var a = new StatBonus(attack: 5, defense: 3, maxArmor: 10);
            var b = new StatBonus(attack: 2, defense: 7, morale: 5);
            var sum = a + b;

            Assert.Equal(7, sum.Attack);
            Assert.Equal(10, sum.Defense);
            Assert.Equal(10, sum.MaxArmor);
            Assert.Equal(5, sum.Morale);
        }

        // ── GrantedSkills ───────────────────────────────────────────────

        [Fact]
        public void AllGrantedSkills_CollectsFromAllEquipment()
        {
            var (battle, attacker, _) = TestHelpers.MakeDuel(
                attackerWeapon: TestHelpers.SampleWeapons.Sword);
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Shield));

            var skills = attacker.AllGrantedSkills().ToList();
            Assert.Equal(2, skills.Count); // Slash + ShieldWall
        }

        [Fact]
        public void AllGrantedSkills_EmptyForNoEquipment()
        {
            var unit = TestHelpers.MakeUnit();
            Assert.Empty(unit.AllGrantedSkills());
        }

        [Fact]
        public void EquipmentWithNoSkills_GrantsNone()
        {
            var (battle, attacker, _) = TestHelpers.MakeDuel();
            battle.Unequip(attacker, EquipmentSlot.RightHand);
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Helmet));

            var skills = attacker.AllGrantedSkills().ToList();
            Assert.Empty(skills);
        }

        // ── Equipment durability ────────────────────────────────────────

        [Fact]
        public void DefaultDurability_Is100()
        {
            var eq = new Equipment(TestHelpers.SampleWeapons.Axe);
            Assert.Equal(100, eq.CurrentDurability);
        }

        [Fact]
        public void CustomDurability_IsRespected()
        {
            var eq = new Equipment(TestHelpers.SampleWeapons.Axe, 75);
            Assert.Equal(75, eq.CurrentDurability);
        }
    }
}

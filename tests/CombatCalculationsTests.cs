using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class CombatCalculationsTests
    {
        // ── RawDamage ───────────────────────────────────────────────────

        [Fact]
        public void RawDamage_DefaultScale_AddsAttackAndWeaponBonus()
        {
            // scale = 1.0 → attack + weaponBonus
            Assert.Equal(20, CombatCalculations.RawDamage(10, 10));
        }

        [Fact]
        public void RawDamage_WithScale_ScalesOnlyWeaponBonus()
        {
            // 10 + (int)(10 * 1.2) = 10 + 12 = 22
            Assert.Equal(22, CombatCalculations.RawDamage(10, 10, 1.2f));
        }

        [Fact]
        public void RawDamage_ZeroWeapon_ReturnsAttackOnly()
        {
            Assert.Equal(15, CombatCalculations.RawDamage(15, 0));
        }

        // ── ReduceByDefense ─────────────────────────────────────────────

        [Fact]
        public void ReduceByDefense_SubtractsHalfDefenseByDefault()
        {
            // 20 - 10/2 = 15
            Assert.Equal(15, CombatCalculations.ReduceByDefense(20, 10));
        }

        [Fact]
        public void ReduceByDefense_NeverDropsBelowOne()
        {
            // raw=1, defense=100 → max(1, 1-50) = 1
            Assert.Equal(1, CombatCalculations.ReduceByDefense(1, 100));
        }

        [Fact]
        public void ReduceByDefense_CustomDivisor()
        {
            // 20 - 9/3 = 20 - 3 = 17
            Assert.Equal(17, CombatCalculations.ReduceByDefense(20, 9, 3));
        }

        [Theory]
        [InlineData(5, 3, 5 / 3)]   // 1 (truncates, not rounds)
        [InlineData(7, 3, 7 / 3)]   // 2
        [InlineData(10, 3, 10 / 3)] // 3
        public void ReduceByDefense_IntegerDivisionTruncates(int defense, int divisor, int expectedReduction)
        {
            int raw = 100;
            Assert.Equal(raw - expectedReduction, CombatCalculations.ReduceByDefense(raw, defense, divisor));
        }

        // ── SplitDamage ─────────────────────────────────────────────────

        [Fact]
        public void SplitDamage_ArmorAbsorbsFirst()
        {
            // 30 damage, 20 armor → 20 to armor, 10 to HP
            var (armorDmg, hpDmg) = CombatCalculations.SplitDamage(30, 20);
            Assert.Equal(20, armorDmg);
            Assert.Equal(10, hpDmg);
        }

        [Fact]
        public void SplitDamage_ArmorAbsorbsAll_WhenSufficient()
        {
            var (armorDmg, hpDmg) = CombatCalculations.SplitDamage(10, 50);
            Assert.Equal(10, armorDmg);
            Assert.Equal(0, hpDmg);
        }

        [Fact]
        public void SplitDamage_NoArmor_AllToHP()
        {
            var (armorDmg, hpDmg) = CombatCalculations.SplitDamage(25, 0);
            Assert.Equal(0, armorDmg);
            Assert.Equal(25, hpDmg);
        }

        [Fact]
        public void SplitDamage_NegativeAmount_ClampedToZero()
        {
            var (armorDmg, hpDmg) = CombatCalculations.SplitDamage(-5, 10);
            Assert.Equal(0, armorDmg);
            Assert.Equal(0, hpDmg);
        }
    }
}

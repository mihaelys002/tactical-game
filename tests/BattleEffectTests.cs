using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class BattleEffectTests
    {
        // ── DamageEffect Apply/Reverse symmetry ─────────────────────────

        [Fact]
        public void DamageEffect_Apply_SplitsAcrossArmorThenHP()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var effect = new DamageEffect(attacker, defender, 70);

            effect.Apply(battle);

            // 50 armor absorbs first, 20 goes to HP
            Assert.Equal(50, effect.AppliedArmorDamage);
            Assert.Equal(20, effect.AppliedHpDamage);
            Assert.Equal(0, defender.Stats.CurrentArmor);
            Assert.Equal(80, defender.Stats.CurrentHP);
        }

        [Fact]
        public void DamageEffect_Reverse_RestoresExactState()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            int hpBefore = defender.Stats.CurrentHP;
            int armorBefore = defender.Stats.CurrentArmor;

            var effect = new DamageEffect(attacker, defender, 30);
            effect.Apply(battle);
            effect.Reverse(battle);

            Assert.Equal(hpBefore, defender.Stats.CurrentHP);
            Assert.Equal(armorBefore, defender.Stats.CurrentArmor);
        }

        [Fact]
        public void DamageEffect_CanKillUnit()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var effect = new DamageEffect(attacker, defender, 999);
            effect.Apply(battle);

            Assert.False(defender.IsAlive);
            Assert.Equal(0, defender.Stats.CurrentHP);
        }

        [Fact]
        public void DamageEffect_Reverse_RevivesKilledUnit()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var effect = new DamageEffect(attacker, defender, 999);
            effect.Apply(battle);
            Assert.False(defender.IsAlive);

            effect.Reverse(battle);
            Assert.True(defender.IsAlive);
        }

        // ── HealEffect Apply/Reverse symmetry ──────────────────────────

        [Fact]
        public void HealEffect_ClampsToMaxHP()
        {
            var (battle, _, defender) = TestHelpers.MakeDuel();
            battle.ChangeHP(defender, -30); // drop to 70

            var effect = new HealEffect(defender, 999);
            effect.Apply(battle);

            Assert.Equal(30, effect.AppliedHeal);
            Assert.Equal(100, defender.Stats.CurrentHP);
        }

        [Fact]
        public void HealEffect_Reverse_RemovesHealedAmount()
        {
            var (battle, _, defender) = TestHelpers.MakeDuel();
            battle.ChangeHP(defender, -30);

            var effect = new HealEffect(defender, 20);
            effect.Apply(battle);
            Assert.Equal(90, defender.Stats.CurrentHP);

            effect.Reverse(battle);
            Assert.Equal(70, defender.Stats.CurrentHP);
        }

        // ── FatigueEffect Apply/Reverse symmetry ────────────────────────

        [Fact]
        public void FatigueEffect_AppliesAndReverses()
        {
            var (battle, attacker, _) = TestHelpers.MakeDuel();
            int before = attacker.Stats.CurrentFatigue;

            var effect = new FatigueEffect(attacker, 10);
            effect.Apply(battle);
            Assert.Equal(before + 10, attacker.Stats.CurrentFatigue);

            effect.Reverse(battle);
            Assert.Equal(before, attacker.Stats.CurrentFatigue);
        }

        [Fact]
        public void FatigueEffect_ClampsToMax()
        {
            var (battle, attacker, _) = TestHelpers.MakeDuel();
            var effect = new FatigueEffect(attacker, 999);
            effect.Apply(battle);

            Assert.Equal(attacker.Stats.MaxFatigue, attacker.Stats.CurrentFatigue);
            // AppliedFatigue should be the actual amount, not the requested amount
            Assert.Equal(attacker.Stats.MaxFatigue, effect.AppliedFatigue);
        }

        // ── MoraleEffect Apply/Reverse symmetry ────────────────────────

        [Fact]
        public void MoraleEffect_AppliesAndReverses()
        {
            var (battle, attacker, _) = TestHelpers.MakeDuel();
            int before = attacker.Stats.Morale;

            var effect = new MoraleEffect(attacker, -20);
            effect.Apply(battle);
            Assert.Equal(before - 20, attacker.Stats.Morale);

            effect.Reverse(battle);
            Assert.Equal(before, attacker.Stats.Morale);
        }
    }
}

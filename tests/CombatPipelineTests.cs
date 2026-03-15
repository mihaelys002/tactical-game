using System.Collections.Generic;
using System.Linq;
using TacticalGame.Grid;
using TacticalGame.Grid.Skills;
using Xunit;

namespace TacticalGame.Tests
{
    public class CombatPipelineTests
    {
        // ── End-to-end skill resolution ─────────────────────────────────

        [Fact]
        public void Resolve_ChopSkill_ProducesDamageAndFatigue()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var weapon = TestHelpers.SampleWeapons.Axe;
            var skill = new ChopSkill();

            var cmd = CombatPipeline.Resolve(attacker, weapon, skill,
                defender.Position, battle);

            // Should have damage effect(s) + fatigue effect
            Assert.True(cmd.Effects.Count >= 2);
            Assert.Contains(cmd.Effects, e => e is DamageEffect);
            Assert.Contains(cmd.Effects, e => e is FatigueEffect);
        }

        [Fact]
        public void Resolve_ChopSkill_DamageIsPositive()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            var weapon = TestHelpers.SampleWeapons.Axe;
            var skill = new ChopSkill();

            var cmd = CombatPipeline.Resolve(attacker, weapon, skill,
                defender.Position, battle);

            var dmg = cmd.Effects.OfType<DamageEffect>().First();
            Assert.True(dmg.Amount > 0);
        }

        [Fact]
        public void Resolve_SlashSkill_DamageIsPositive()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel(
                attackerWeapon: TestHelpers.SampleWeapons.Sword);
            var weapon = TestHelpers.SampleWeapons.Sword;
            var skill = new SlashSkill();

            var cmd = CombatPipeline.Resolve(attacker, weapon, skill,
                defender.Position, battle);

            var dmg = cmd.Effects.OfType<DamageEffect>().First();
            Assert.True(dmg.Amount > 0);
        }

        [Fact]
        public void Resolve_ChopDealsMoreThanSlash_SameAttacker()
        {
            // Chop has 1.2x weapon scale vs Slash's 1.0x, but Slash has lower defense divisor (3 vs 2)
            // With Axe (attack bonus 10), attacker attack 10, defender defense 10:
            // Chop: raw = 10 + (int)(10*1.2) = 22, reduce = max(1, 22 - 10/2) = 17
            // Slash with Sword: raw = 10 + 7 = 17, reduce = max(1, 17 - 10/3) = 14
            var (battle1, atk1, def1) = TestHelpers.MakeDuel();
            var chopCmd = CombatPipeline.Resolve(atk1, TestHelpers.SampleWeapons.Axe,
                new ChopSkill(), def1.Position, battle1);
            int chopDamage = chopCmd.Effects.OfType<DamageEffect>().First().Amount;

            var (battle2, atk2, def2) = TestHelpers.MakeDuel(
                attackerWeapon: TestHelpers.SampleWeapons.Sword);
            var slashCmd = CombatPipeline.Resolve(atk2, TestHelpers.SampleWeapons.Sword,
                new SlashSkill(), def2.Position, battle2);
            int slashDamage = slashCmd.Effects.OfType<DamageEffect>().First().Amount;

            Assert.True(chopDamage > slashDamage,
                $"Chop ({chopDamage}) should deal more than Slash ({slashDamage})");
        }

        // ── Full execute + undo cycle ───────────────────────────────────

        [Fact]
        public void Pipeline_Execute_ThenUndo_RestoresFullState()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            int defHP = defender.Stats.CurrentHP;
            int defArmor = defender.Stats.CurrentArmor;
            int atkFat = attacker.Stats.CurrentFatigue;

            var cmd = CombatPipeline.Resolve(attacker, TestHelpers.SampleWeapons.Axe,
                new ChopSkill(), defender.Position, battle);
            cmd.Execute(battle);

            // State changed
            Assert.True(defender.Stats.CurrentHP < defHP || defender.Stats.CurrentArmor < defArmor);
            Assert.True(attacker.Stats.CurrentFatigue > atkFat);

            cmd.Undo(battle);

            Assert.Equal(defHP, defender.Stats.CurrentHP);
            Assert.Equal(defArmor, defender.Stats.CurrentArmor);
            Assert.Equal(atkFat, attacker.Stats.CurrentFatigue);
        }

        // ── Trait modification ──────────────────────────────────────────

        [Fact]
        public void Pipeline_AttackerTrait_CanModifyDamage()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            attacker.Traits.Add(new DoubleDamageTrait());

            var cmd = CombatPipeline.Resolve(attacker, TestHelpers.SampleWeapons.Axe,
                new ChopSkill(), defender.Position, battle);

            var dmg = cmd.Effects.OfType<DamageEffect>().First();
            // Original damage with Axe (attack 10 + weapon 10*1.2=12=22, reduce by 10/2=5 → 17)
            // Doubled → 34
            Assert.Equal(34, dmg.Amount);
        }

        [Fact]
        public void Pipeline_DefenderTrait_CanReduceDamage()
        {
            var (battle, attacker, defender) = TestHelpers.MakeDuel();
            defender.Traits.Add(new HalveDamageTrait());

            var cmd = CombatPipeline.Resolve(attacker, TestHelpers.SampleWeapons.Axe,
                new ChopSkill(), defender.Position, battle);

            var dmg = cmd.Effects.OfType<DamageEffect>().First();
            // 17 / 2 = 8 (integer division)
            Assert.Equal(8, dmg.Amount);
        }

        // ── ShieldWall (self-cast) ──────────────────────────────────────

        [Fact]
        public void ShieldWall_ProducesOnlyFatigue_NoTargets()
        {
            var (battle, attacker, _) = TestHelpers.MakeDuel();
            var skill = new ShieldWallSkill();
            var weapon = TestHelpers.SampleWeapons.Shield;
            battle.Equip(attacker, new Equipment(weapon));

            var cmd = CombatPipeline.Resolve(attacker, weapon, skill,
                attacker.Position, battle);

            Assert.All(cmd.Effects, e => Assert.IsType<FatigueEffect>(e));
            Assert.Single(cmd.Effects);
        }

        // ── No targets hit → no essential effects ───────────────────────

        [Fact]
        public void ChopAtEmptyHex_ProducesNoEssentialEffects()
        {
            var battle = TestHelpers.MakeBattle();
            var attacker = TestHelpers.MakeUnit("Attacker");
            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.RegisterTeam(new List<Unit> { attacker });
            battle.Equip(attacker, new Equipment(TestHelpers.SampleWeapons.Axe));

            var cmd = CombatPipeline.Resolve(attacker, TestHelpers.SampleWeapons.Axe,
                new ChopSkill(), new HexCoord(1, 0), battle);

            // Only fatigue effect, which is not marked essential by ChopSkill
            bool hasEssential = cmd.Effects.Any(e => e.IsEssential);
            Assert.False(hasEssential);

            // So Execute should fail
            Assert.False(cmd.Execute(battle));
        }

        // ── Test trait implementations ──────────────────────────────────

        private sealed class DoubleDamageTrait : ITrait
        {
            public string Id => "double_damage";
            public void ModifyEffects(PrototypeCommand cmd, Unit owner)
            {
                foreach (var effect in cmd.Effects)
                    if (effect is DamageEffect dmg && dmg.Source == owner)
                        dmg.Amount *= 2;
            }
        }

        private sealed class HalveDamageTrait : ITrait
        {
            public string Id => "halve_damage";
            public void ModifyEffects(PrototypeCommand cmd, Unit owner)
            {
                foreach (var effect in cmd.Effects)
                    if (effect is DamageEffect dmg && dmg.Target == owner)
                        dmg.Amount /= 2;
            }
        }
    }
}

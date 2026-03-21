namespace TacticalGame.Grid.Skills
{
    /// <summary>
    /// Heavy axe strike. Scales strongly off weapon attack bonus.
    /// </summary>
    public sealed class ChopSkill : SkillDef
    {
        private const float WeaponScale = 1.2f;
        private const int DefenseDivisor = 2;

        public ChopSkill() : base("chop", "Chop", fatigueCost: 6, range: 1) { }

        public override int EstimatePower(Unit user, EquipmentDef weapon)
        {
            return CombatCalculations.RawDamage(user.Stats.Attack, weapon.Bonus.Attack, WeaponScale);
        }

        public override PrototypeCommand CreateCommand(Unit user, EquipmentDef weapon,
            HexCoord targetHex, BattleState battle)
        {
            var hitTargets = CombatCalculations.ResolveTargets(user, HitPattern, targetHex, battle);
            var cmd = new PrototypeCommand(CommandType.Attack, user, weapon, this, targetHex, hitTargets);

            foreach (var target in hitTargets)
            {
                int raw = CombatCalculations.RawDamage(user.Stats.Attack, weapon.Bonus.Attack, WeaponScale);
                int damage = CombatCalculations.ReduceByDefense(raw, target.EffectiveDefense, DefenseDivisor);
                cmd.Effects.Add(new DamageEffect(user, target, damage) { IsEssential = true });
            }

            cmd.Effects.Add(new FatigueEffect(user, FatigueCost));
            return cmd;
        }
    }
}

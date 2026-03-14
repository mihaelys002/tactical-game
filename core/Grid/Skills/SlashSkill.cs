namespace TacticalGame.Grid.Skills
{
    /// <summary>
    /// Quick sword strike. Balanced between unit skill and weapon.
    /// </summary>
    public sealed class SlashSkill : SkillDef
    {
        private const int DefenseDivisor = 3;

        public SlashSkill() : base("slash", "Slash", fatigueCost: 4, range: 1) { }

        public override int EstimatePower(Unit user, EquipmentDef weapon)
        {
            return CombatCalculations.RawDamage(user.Stats.Attack, weapon.Bonus.Attack);
        }

        public override PrototypeCommand CreateCommand(Unit user, EquipmentDef weapon,
            HexCoord targetHex, BattleState battle)
        {
            var hitTargets = CombatCalculations.ResolveTargets(user, HitPattern, targetHex, battle);
            var cmd = new PrototypeCommand(user, weapon, this, targetHex, hitTargets);

            foreach (var target in hitTargets)
            {
                int raw = CombatCalculations.RawDamage(user.Stats.Attack, weapon.Bonus.Attack);
                int damage = CombatCalculations.ReduceByDefense(raw, target.EffectiveDefense, DefenseDivisor);
                cmd.Effects.Add(new DamageEffect(user, target, damage) { IsEssential = true });
            }

            cmd.Effects.Add(new FatigueEffect(user, FatigueCost));
            return cmd;
        }
    }
}

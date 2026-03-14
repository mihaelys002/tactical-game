namespace TacticalGame.Grid
{
    public static class CombatPipeline
    {
        public static CompoundCommand Resolve(
            Unit user, EquipmentDef weapon, SkillDef skill,
            HexCoord targetHex, BattleState battle)
        {
            // Step 1: Skill creates prototype command with effects
            var cmd = skill.CreateCommand(user, weapon, targetHex, battle);

            // Step 2: Attacker traits modify
            foreach (var trait in user.Traits)
                trait.ModifyEffects(cmd, user);

            // Step 3: Each target's traits modify
            foreach (var target in cmd.HitTargets)
                foreach (var trait in target.Traits)
                    trait.ModifyEffects(cmd, target);

            // Step 4: Finalize
            return cmd.Finalize();
        }
    }
}

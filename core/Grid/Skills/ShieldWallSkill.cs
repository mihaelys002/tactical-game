using System.Collections.Generic;

namespace TacticalGame.Grid.Skills
{
    /// <summary>
    /// Defensive stance. No damage — future: applies a defense buff.
    /// </summary>
    public sealed class ShieldWallSkill : SkillDef
    {
        public ShieldWallSkill() : base("shield_wall", "Shield Wall", fatigueCost: 8, range: 0) { }

        public override bool HasValidUse(Unit user, BattleState battle)
        {
            var enemies = battle.GetEnemies(user);
            foreach (var enemy in enemies)
                if (user.Position.DistanceTo(enemy.Position) <= 1)
                    return true;
            return false;
        }

        public override int EstimatePower(Unit user, EquipmentDef weapon)
        {
            return 0;
        }

        public override PrototypeCommand CreateCommand(Unit user, EquipmentDef weapon,
            HexCoord targetHex, BattleState battle)
        {
            var cmd = new PrototypeCommand(CommandType.Attack, user, weapon, this, targetHex, new List<Unit>());
            cmd.Effects.Add(new FatigueEffect(user, FatigueCost) { IsEssential = true });
            return cmd;
        }
    }
}

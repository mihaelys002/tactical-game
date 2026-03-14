using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public abstract class SkillDef
    {
        public string Id { get; }
        public string Name { get; }
        public int FatigueCost { get; }
        public int Range { get; }
        public HitPattern HitPattern { get; }

        protected SkillDef(string id, string name, int fatigueCost, int range,
            HitPattern? hitPattern = null)
        {
            Id = id;
            Name = name;
            FatigueCost = fatigueCost;
            Range = range;
            HitPattern = hitPattern ?? HitPattern.SingleTarget;
        }

        public virtual bool HasValidUse(Unit user, BattleState battle)
        {
            var enemies = battle.GetEnemies(user);
            foreach (var enemy in enemies)
                if (user.Position.DistanceTo(enemy.Position) <= Range)
                    return true;
            return false;
        }

        public abstract PrototypeCommand CreateCommand(Unit user, EquipmentDef weapon,
            HexCoord targetHex, BattleState battle);

        /// <summary>
        /// Cheap estimate for AI scoring. No allocations, no pipeline.
        /// Returns approximate raw damage this skill would deal with the given weapon.
        /// </summary>
        public abstract int EstimatePower(Unit user, EquipmentDef weapon);

        public override string ToString() => $"SkillDef({Id})";
    }
}

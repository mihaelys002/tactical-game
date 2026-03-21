using System;
using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public static class CombatCalculations
    {
        public const int DefaultFatigueRecovery = 15;

        public static List<Unit> ResolveTargets(Unit user, HitPattern pattern,
            HexCoord targetHex, BattleState battle)
        {
            var hitHexes = pattern.Resolve(user.Position, targetHex);
            var targets = new List<Unit>();
            foreach (var hex in hitHexes)
            {
                if (!battle.Grid.Cells.TryGetValue(hex, out var cell)) continue;
                foreach (var occupant in cell.Occupants)
                {
                    if (occupant.IsAlive)
                        targets.Add(occupant);
                }
            }
            return targets;
        }

        public static int RawDamage(int attack, int weaponBonus, float scale = 1f)
        {
            return attack + (int)(weaponBonus * scale);
        }

        public static int ReduceByDefense(int raw, int defense, int divisor = 2)
        {
            return Math.Max(1, raw - defense / divisor);
        }

        public static (int armorDamage, int hpDamage) SplitDamage(int amount, int currentArmor)
        {
            int clamped = Math.Max(0, amount);
            int armorDamage = Math.Min(currentArmor, clamped);
            int hpDamage = clamped - armorDamage;
            return (armorDamage, hpDamage);
        }
    }
}

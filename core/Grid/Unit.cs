using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public class Unit
    {
        public string Name { get; set; } = "";
        public HexCoord Position { get; internal set; }
        public UnitStats Stats { get; }
        public EquipmentSlots Equipment { get; }
        public List<ITrait> Traits { get; } = new();
        public bool IsAlive => Stats.CurrentHP > 0;

        public int EffectiveAttack => Stats.Attack + Equipment.TotalBonus().Attack;
        public int EffectiveDefense => Stats.Defense + Equipment.TotalBonus().Defense;
        public int EffectiveResolve => Stats.Resolve + Equipment.TotalBonus().Resolve;

        public Unit(UnitStats stats)
        {
            Stats = stats;
            Equipment = new EquipmentSlots();
        }

        internal Unit(UnitStats stats, EquipmentSlots equipment)
        {
            Stats = stats;
            Equipment = equipment;
        }

        public override string ToString() => string.IsNullOrEmpty(Name) ? "Unit" : Name;
    }
}

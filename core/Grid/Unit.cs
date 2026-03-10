using System.IO;

namespace TacticalGame.Grid
{
    public class Unit
    {
        public HexCoord Position { get; internal set; }
        public UnitStats Stats { get; }
        public EquipmentSlots Equipment { get; }
        public bool IsAlive => Stats.CurrentHP > 0;

        public int EffectiveAttack => Stats.Attack + Equipment.TotalBonus().Attack;
        public int EffectiveDefense => Stats.Defense + Equipment.TotalBonus().Defense;
        public int EffectiveResolve => Stats.Resolve + Equipment.TotalBonus().Resolve;

        public Unit(UnitStats stats)
        {
            Stats = stats;
            Equipment = new EquipmentSlots();
        }

        public void WriteTo(BinaryWriter writer)
        {
            Position.WriteTo(writer);
            Stats.WriteTo(writer);
            Equipment.WriteTo(writer);
        }

        public static Unit ReadFrom(BinaryReader reader, EquipmentRegistry registry)
        {
            var position = HexCoord.ReadFrom(reader);
            var stats = UnitStats.ReadFrom(reader);
            var equipment = EquipmentSlots.ReadFrom(reader, registry);
            var unit = new Unit(stats, equipment);
            unit.Position = position;
            return unit;
        }

        internal Unit(UnitStats stats, EquipmentSlots equipment)
        {
            Stats = stats;
            Equipment = equipment;
        }
    }
}

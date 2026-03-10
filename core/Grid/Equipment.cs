using System.IO;

namespace TacticalGame.Grid
{
    public class Equipment
    {
        public EquipmentDef Def { get; }
        public int CurrentDurability { get; internal set; }

        public Equipment(EquipmentDef def, int durability)
        {
            Def = def;
            CurrentDurability = durability;
        }

        public Equipment(EquipmentDef def)
            : this(def, 100)
        {
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Def.Id);
            writer.Write(CurrentDurability);
        }

        public static Equipment ReadFrom(BinaryReader reader, EquipmentRegistry registry)
        {
            string id = reader.ReadString();
            int durability = reader.ReadInt32();
            var def = registry.Get(id);
            return new Equipment(def, durability);
        }

        public override string ToString() => $"{Def.Name} ({CurrentDurability}%)";
    }
}

using Newtonsoft.Json;

namespace TacticalGame.Grid
{
    [JsonObject(MemberSerialization.Fields)]
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

        public override string ToString() => $"{Def.Name} ({CurrentDurability}%)";
    }
}

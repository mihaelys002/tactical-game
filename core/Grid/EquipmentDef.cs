using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public class EquipmentDef
    {
        public string Id { get; }
        public string Name { get; }
        public EquipmentSlot Slot { get; }
        public int Weight { get; }
        public StatBonus Bonus { get; }
        public bool IsTwoHanded { get; }
        public List<SkillDef> GrantedSkills { get; }

        public EquipmentDef(string id, string name, EquipmentSlot slot,
            int weight, StatBonus bonus, IReadOnlyList<SkillDef> grantedSkills,
            bool isTwoHanded = false)
        {
            Id = id;
            Name = name;
            Slot = slot;
            Weight = weight;
            Bonus = bonus;
            IsTwoHanded = isTwoHanded;
            GrantedSkills = new List<SkillDef>(grantedSkills);
        }

        public override string ToString() => $"EquipmentDef({Id})";
    }
}

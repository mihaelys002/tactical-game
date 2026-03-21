using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TacticalGame.Grid
{
    [JsonObject(MemberSerialization.Fields)]
    public class Unit
    {
        public string Name { get; set; } = "";
        public HexCoord Position { get; internal set; }
        public int TeamIndex { get; internal set; } = -1;
        public UnitStats Stats { get; }
        public Dictionary<EquipmentSlot, Equipment> Equipment { get; } = new();
        public List<ITrait> Traits { get; } = new();
        public bool IsAlive => Stats.CurrentHP > 0;

        public int EffectiveAttack => Stats.Attack + TotalBonus().Attack;
        public int EffectiveDefense => Stats.Defense + TotalBonus().Defense;
        public int EffectiveResolve => Stats.Resolve + TotalBonus().Resolve;

        public StatBonus TotalBonus()
        {
            var total = default(StatBonus);
            foreach (var eq in Equipment.Values)
                total = total + eq.Def.Bonus;
            return total;
        }

        public IEnumerable<SkillDef> AllGrantedSkills()
        {
            return Equipment.Values.SelectMany(eq => eq.Def.GrantedSkills);
        }

        public Unit(UnitStats stats)
        {
            Stats = stats;
        }

        public override string ToString() => string.IsNullOrEmpty(Name) ? "Unit" : Name;
    }
}

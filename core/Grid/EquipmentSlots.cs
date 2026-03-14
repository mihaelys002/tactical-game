using System.Collections.Generic;
using System.Linq;

namespace TacticalGame.Grid
{
    public class EquipmentSlots
    {
        private readonly Dictionary<EquipmentSlot, Equipment> _slots = new();

        public Equipment? Get(EquipmentSlot slot)
        {
            _slots.TryGetValue(slot, out var eq);
            return eq;
        }

        public bool Has(EquipmentSlot slot) => _slots.ContainsKey(slot);

        public IEnumerable<Equipment> All => _slots.Values;

        public bool IsEmpty => _slots.Count == 0;

        internal void Set(EquipmentSlot slot, Equipment equipment)
        {
            _slots[slot] = equipment;
        }

        internal Equipment? Remove(EquipmentSlot slot)
        {
            if (!_slots.Remove(slot, out var removed))
                return null;
            return removed;
        }

        public StatBonus TotalBonus()
        {
            var total = default(StatBonus);
            foreach (var eq in _slots.Values)
                total = total + eq.Def.Bonus;
            return total;
        }

        public IEnumerable<SkillDef> AllGrantedSkills()
        {
            return _slots.Values.SelectMany(eq => eq.Def.GrantedSkills);
        }

    }
}

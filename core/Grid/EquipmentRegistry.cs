using System;
using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public class EquipmentRegistry
    {
        private readonly Dictionary<string, EquipmentDef> _defs = new();

        public void Register(EquipmentDef def)
        {
            _defs[def.Id] = def;
        }

        public EquipmentDef Get(string id)
        {
            if (_defs.TryGetValue(id, out var def))
                return def;

            throw new ArgumentException($"Unknown equipment: {id}");
        }

        public bool TryGet(string id, out EquipmentDef? def)
        {
            return _defs.TryGetValue(id, out def);
        }
    }
}

using System;
using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public static class DefRegistry
    {
        private static readonly Dictionary<string, EquipmentDef> _equipment = new();
        private static readonly Dictionary<string, SkillDef> _skills = new();

        public static void Register(EquipmentDef def)
        {
            _equipment[def.Id] = def;
            foreach (var skill in def.GrantedSkills)
                Register(skill);
        }

        public static void Register(SkillDef def)
        {
            _skills[def.Id] = def;
        }

        public static EquipmentDef GetEquipment(string id)
        {
            if (_equipment.TryGetValue(id, out var def))
                return def;
            throw new KeyNotFoundException($"EquipmentDef '{id}' not found in registry.");
        }

        public static SkillDef GetSkill(string id)
        {
            if (_skills.TryGetValue(id, out var def))
                return def;
            throw new KeyNotFoundException($"SkillDef '{id}' not found in registry.");
        }

        public static void Clear()
        {
            _equipment.Clear();
            _skills.Clear();
        }
    }
}

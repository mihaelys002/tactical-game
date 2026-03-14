using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public class PrototypeCommand
    {
        public Unit Unit { get; }
        public EquipmentDef Weapon { get; }
        public SkillDef Skill { get; }
        public HexCoord TargetHex { get; }
        public IReadOnlyList<Unit> HitTargets { get; }
        public List<BattleEffect> Effects { get; } = new();

        public PrototypeCommand(Unit unit, EquipmentDef weapon, SkillDef skill,
            HexCoord targetHex, IReadOnlyList<Unit> hitTargets)
        {
            Unit = unit;
            Weapon = weapon;
            Skill = skill;
            TargetHex = targetHex;
            HitTargets = hitTargets;
        }

        public CompoundCommand Finalize()
        {
            return new CompoundCommand(Unit, Weapon, Skill, TargetHex, Effects.AsReadOnly());
        }
    }
}

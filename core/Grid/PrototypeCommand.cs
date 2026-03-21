using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public enum CommandType { Attack, RoundRecovery }

    public class PrototypeCommand
    {
        public CommandType Type { get; }
        public Unit Unit { get; }
        public EquipmentDef Weapon { get; }
        public SkillDef Skill { get; }
        public HexCoord TargetHex { get; }
        public IReadOnlyList<Unit> HitTargets { get; }
        public List<BattleEffect> Effects { get; } = new();

        public PrototypeCommand(CommandType type, Unit unit, EquipmentDef weapon, SkillDef skill,
            HexCoord targetHex, IReadOnlyList<Unit> hitTargets)
        {
            Type = type;
            Unit = unit;
            Weapon = weapon;
            Skill = skill;
            TargetHex = targetHex;
            HitTargets = hitTargets;
        }

        public CompoundCommand Finalize()
        {
            return new CompoundCommand(Unit, Weapon, Skill, TargetHex, new List<BattleEffect>(Effects));
        }
    }
}

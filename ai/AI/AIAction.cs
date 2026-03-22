using TacticalGame.Grid;

namespace TacticalGame.AI
{
    public abstract class AIAction
    {
        public Unit Unit { get; }
        public HexCoord Target { get; }
        public float Score { get; }

        protected AIAction(Unit unit, HexCoord target, float score)
        {
            Unit = unit;
            Target = target;
            Score = score;
        }

        public abstract IBattleCommand CreateCommand(BattleState battle);

        public static MoveAction Move(Unit unit, HexCoord target, float score)
            => new(unit, target, score);

        public static SkillAction UseSkill(Unit unit, HexCoord targetHex, SkillDef skill, EquipmentDef weapon, float score)
            => new(unit, targetHex, skill, weapon, score);
    }

    public class MoveAction : AIAction
    {
        public MoveAction(Unit unit, HexCoord target, float score) : base(unit, target, score) { }

        public override IBattleCommand CreateCommand(BattleState battle)
        {
            return new MoveCommand(Unit, Unit.Position, Target);
        }
    }

    public class SkillAction : AIAction
    {
        public SkillDef Skill { get; }
        public EquipmentDef Weapon { get; }

        public SkillAction(Unit unit, HexCoord targetHex, SkillDef skill, EquipmentDef weapon, float score)
            : base(unit, targetHex, score)
        {
            Skill = skill;
            Weapon = weapon;
        }

        public override IBattleCommand CreateCommand(BattleState battle)
        {
            return CombatPipeline.Resolve(Unit, Weapon, Skill, Target, battle);
        }
    }
}

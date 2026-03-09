using TacticalGame.Grid;

namespace TacticalGame.AI
{
    public enum AIActionType
    {
        Move,
        Attack
    }

    public class AIAction
    {
        public AIActionType Type { get; }
        public HexCoord Target { get; }
        public Unit? TargetUnit { get; }
        public float Score { get; }

        private AIAction(AIActionType type, HexCoord target, Unit? targetUnit, float score)
        {
            Type = type;
            Target = target;
            TargetUnit = targetUnit;
            Score = score;
        }

        public static AIAction Move(HexCoord target, float score)
            => new(AIActionType.Move, target, null, score);

        public static AIAction Attack(Unit target, float score)
            => new(AIActionType.Attack, target.Position, target, score);
    }
}

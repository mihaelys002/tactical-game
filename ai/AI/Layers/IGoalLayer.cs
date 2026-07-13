using TacticalGame.Grid;

namespace TacticalGame.AI.Layers
{
    public interface IGoalLayer
    {
        void Evaluate(Unit unit, AIBlackboard blackboard, ScoringContext context);
    }
}

using TacticalGame.Grid;

namespace TacticalGame.AI.Layers
{
    public interface IStrategyLayer
    {
        void Evaluate(Unit unit, AIBlackboard blackboard, ScoringContext context);
    }
}

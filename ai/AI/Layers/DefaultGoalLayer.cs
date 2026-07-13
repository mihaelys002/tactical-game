using TacticalGame.Grid;

namespace TacticalGame.AI.Layers
{
    public class DefaultGoalLayer : IGoalLayer
    {
        public void Evaluate(Unit unit, AIBlackboard blackboard, ScoringContext context)
        {
            if (context.AssignedTarget != null)
                context.KillTargetBonus = 50f;

            if (context.Role == UnitRole.Retreating || context.ShouldRetreat)
                context.SurvivalBias = 100f;
        }
    }
}

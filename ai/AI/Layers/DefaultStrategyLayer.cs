using TacticalGame.Grid;

namespace TacticalGame.AI.Layers
{
    public class DefaultStrategyLayer : IStrategyLayer
    {
        public void Evaluate(Unit unit, AIBlackboard blackboard, ScoringContext context)
        {
            if (context.ForceRetreatUnit == unit)
            {
                context.Role = UnitRole.Retreating;
                context.ShouldRetreat = true;
                return;
            }

            if (context.PriorityTarget != null && context.PriorityTarget.IsAlive)
                context.AssignedTarget = context.PriorityTarget;

            float hpRatio = (float)unit.Stats.CurrentHP / unit.Stats.MaxHP;

            if (hpRatio < 0.25f)
            {
                context.Role = UnitRole.Retreating;
                context.ShouldRetreat = true;
            }
            else
            {
                context.Role = UnitRole.Attacker;
            }
        }
    }
}

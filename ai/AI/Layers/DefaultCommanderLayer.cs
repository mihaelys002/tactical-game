using TacticalGame.Grid;

namespace TacticalGame.AI.Layers
{
    public class DefaultCommanderLayer : ICommanderLayer
    {
        public float AggressionBias { get; set; }
        public Unit? PriorityTarget { get; set; }
        public Unit? ProtectTarget { get; set; }
        public Unit? ForceRetreatUnit { get; set; }

        public void Evaluate(AIBlackboard blackboard, ScoringContext context)
        {
            context.AggressionBias = AggressionBias;
            context.PriorityTarget = PriorityTarget;
            context.ProtectTarget = ProtectTarget;
            context.ForceRetreatUnit = ForceRetreatUnit;
        }
    }
}

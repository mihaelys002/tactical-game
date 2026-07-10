namespace TacticalGame.AI.Layers
{
    public interface ICommanderLayer
    {
        void Evaluate(AIBlackboard blackboard, ScoringContext context);
    }
}

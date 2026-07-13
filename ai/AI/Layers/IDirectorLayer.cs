using TacticalGame.Grid;

namespace TacticalGame.AI.Layers
{
    public interface IDirectorLayer
    {
        AIAction? Override(Unit unit, AIBlackboard blackboard);
    }
}

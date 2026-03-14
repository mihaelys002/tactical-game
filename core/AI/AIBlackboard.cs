using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    public class AIBlackboard
    {
        public BattleState Battle { get; }
        public IReadOnlyList<Unit> Friends { get; }
        public IReadOnlyList<Unit> Enemies { get; }

        public AIBlackboard(BattleState battle, Unit unit)
        {
            Battle = battle;
            Friends = battle.GetAllies(unit);
            Enemies = battle.GetEnemies(unit);
        }
    }
}

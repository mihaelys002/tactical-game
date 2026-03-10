using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    public class AIBlackboard
    {
        public BattleState Battle { get; }
        public IReadOnlyList<Unit> Friends { get; }
        public IReadOnlyList<Unit> Enemies { get; }

        public AIBlackboard(BattleState battle, IReadOnlyList<Unit> friends, IReadOnlyList<Unit> enemies)
        {
            Battle = battle;
            Friends = friends;
            Enemies = enemies;
        }
    }
}

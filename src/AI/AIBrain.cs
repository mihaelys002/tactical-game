using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    public class AIBrain
    {
        public AIAction? DecideAction(Unit unit, AIBlackboard blackboard)
        {
            var candidates = new List<AIAction>();

            ScoreAttacks(unit, blackboard, candidates);
            ScoreMoves(unit, blackboard, candidates);

            AIAction? best = null;
            foreach (var action in candidates)
            {
                if (best == null || action.Score > best.Score)
                    best = action;
            }

            return best;
        }

        private void ScoreAttacks(Unit unit, AIBlackboard blackboard, List<AIAction> candidates)
        {
            foreach (var enemy in blackboard.Enemies)
            {
                int distance = unit.Position.DistanceTo(enemy.Position);

                if (distance != 1)
                    continue;

                // Prefer low HP targets
                float hpRatio = (float)enemy.Stats.CurrentHP / enemy.Stats.MaxHP;
                float score = 100f * (1f - hpRatio) + 50f;

                candidates.Add(AIAction.Attack(enemy, score));
            }
        }

        private void ScoreMoves(Unit unit, AIBlackboard blackboard, List<AIAction> candidates)
        {
            var grid = blackboard.Battle.Grid;
            Unit? closestEnemy = FindClosestEnemy(unit, blackboard.Enemies);

            if (closestEnemy == null)
                return;

            int currentDistance = unit.Position.DistanceTo(closestEnemy.Position);

            foreach (var neighbor in grid.GetNeighbors(unit.Position))
            {
                if (!neighbor.IsWalkable)
                    continue;

                int newDistance = neighbor.Coord.DistanceTo(closestEnemy.Position);

                // Prefer cells that close the gap
                float score = (currentDistance - newDistance) * 30f;

                candidates.Add(AIAction.Move(neighbor.Coord, score));
            }
        }

        private Unit? FindClosestEnemy(Unit unit, IReadOnlyList<Unit> enemies)
        {
            Unit? closest = null;
            int closestDistance = int.MaxValue;

            foreach (var enemy in enemies)
            {
                int distance = unit.Position.DistanceTo(enemy.Position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = enemy;
                }
            }

            return closest;
        }
    }
}

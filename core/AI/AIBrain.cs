using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    public class AIBrain
    {
        public static AIAction? DecideAction(Unit unit, AIBlackboard blackboard)
        {
            var candidates = new List<AIAction>();

            ScoreSkills(unit, blackboard, candidates);
            ScoreMoves(unit, blackboard, candidates);

            AIAction? best = null;
            foreach (var action in candidates)
            {
                if (best == null || action.Score > best.Score)
                    best = action;
            }

            return best;
        }

        private static void ScoreSkills(Unit unit, AIBlackboard blackboard, List<AIAction> candidates)
        {
            // Self-cast skills (range 0) — score once, independent of enemies
            foreach (var equipment in unit.Equipment.All)
            {
                foreach (var skill in equipment.Def.GrantedSkills)
                {
                    if (skill.Range != 0) continue;
                    if (unit.Stats.CurrentFatigue + skill.FatigueCost > unit.Stats.MaxFatigue) continue;
                    if (!skill.HasValidUse(unit, blackboard.Battle)) continue;

                    float score = 20f;
                    float hpRatio = (float)unit.Stats.CurrentHP / unit.Stats.MaxHP;
                    score += 80f * (1f - hpRatio);

                    candidates.Add(AIAction.UseSkill(unit, unit.Position, skill, equipment.Def, score));
                }
            }

            // Targeted skills — score per enemy
            foreach (var enemy in blackboard.Enemies)
            {
                int distance = unit.Position.DistanceTo(enemy.Position);

                foreach (var equipment in unit.Equipment.All)
                {
                    foreach (var skill in equipment.Def.GrantedSkills)
                    {
                        if (skill.Range == 0) continue;
                        if (distance > skill.Range) continue;
                        if (unit.Stats.CurrentFatigue + skill.FatigueCost > unit.Stats.MaxFatigue) continue;

                        int power = skill.EstimatePower(unit, equipment.Def);
                        float hpRatio = (float)enemy.Stats.CurrentHP / enemy.Stats.MaxHP;
                        float score = power + 100f * (1f - hpRatio);

                        candidates.Add(AIAction.UseSkill(unit, enemy.Position, skill, equipment.Def, score));
                    }
                }
            }
        }

        private static void ScoreMoves(Unit unit, AIBlackboard blackboard, List<AIAction> candidates)
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
                float score = (currentDistance - newDistance) * 30f;

                candidates.Add(AIAction.Move(unit, neighbor.Coord, score));
            }
        }

        private static Unit? FindClosestEnemy(Unit unit, IReadOnlyList<Unit> enemies)
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

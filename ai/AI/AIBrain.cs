using System.Collections.Generic;
using TacticalGame.AI.Debug;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    public class AIBrain
    {
        public static AIAction? DecideAction(Unit unit, AIBlackboard blackboard)
        {
            return DecideAction(unit, blackboard, new ScoringContext());
        }

        public static AIAction? DecideAction(Unit unit, AIBlackboard blackboard, ScoringContext context,
            DecisionTrace? trace = null)
        {
            var candidates = new List<AIAction>();

            ScoreSkills(unit, blackboard, context, candidates, trace);
            ScoreMoves(unit, blackboard, context, candidates, trace);

            AIAction? best = null;
            foreach (var action in candidates)
            {
                if (best == null || action.Score > best.Score)
                    best = action;
            }

            trace?.MarkChosen(best);
            return best;
        }

        private static void ScoreSkills(Unit unit, AIBlackboard blackboard, ScoringContext context,
            List<AIAction> candidates, DecisionTrace? trace)
        {
            // Per-unit weights from resource file; defaults preserve original balance.
            var w = context.ActionWeights;
            float selfCastBase = w?.Get("selfCastBase", 20f) ?? 20f;
            float ownHpWeight = w?.Get("ownHpMissing", 80f) ?? 80f;
            float enemyHpWeight = w?.Get("enemyHpMissing", 100f) ?? 100f;

            // Self-cast skills (range 0) — score once, independent of enemies
            foreach (var equipment in unit.Equipment.Values)
            {
                foreach (var skill in equipment.Def.GrantedSkills)
                {
                    if (skill.Range != 0) continue;
                    if (unit.Stats.CurrentFatigue + skill.FatigueCost > unit.Stats.MaxFatigue) continue;
                    if (!skill.HasValidUse(unit, blackboard.Battle)) continue;

                    float score = selfCastBase;
                    float hpRatio = (float)unit.Stats.CurrentHP / unit.Stats.MaxHP;
                    score += ownHpWeight * (1f - hpRatio);

                    bool preferred = context.PreferredSkill == skill && context.AssignedTarget == null;
                    if (preferred)
                        score += context.PreferredSkillBonus;

                    var action = AIAction.UseSkill(unit, unit.Position, skill, equipment.Def, score);
                    candidates.Add(action);

                    if (trace != null)
                    {
                        var terms = new List<ScoreTerm>
                        {
                            new("baseSelfCast", 1f, selfCastBase, selfCastBase),
                            new("ownHpMissing", 1f - hpRatio, ownHpWeight, ownHpWeight * (1f - hpRatio)),
                        };
                        if (preferred)
                            terms.Add(new("preferredSkill", 1f, context.PreferredSkillBonus,
                                context.PreferredSkillBonus));
                        trace.Add(action, $"{skill.Name} → self", terms);
                    }
                }
            }

            // Targeted skills — score per enemy
            foreach (var enemy in blackboard.Enemies)
            {
                int distance = unit.Position.DistanceTo(enemy.Position);

                foreach (var equipment in unit.Equipment.Values)
                {
                    foreach (var skill in equipment.Def.GrantedSkills)
                    {
                        if (skill.Range == 0) continue;
                        if (distance > skill.Range) continue;
                        if (unit.Stats.CurrentFatigue + skill.FatigueCost > unit.Stats.MaxFatigue) continue;

                        int power = skill.EstimatePower(unit, equipment.Def);
                        float hpRatio = (float)enemy.Stats.CurrentHP / enemy.Stats.MaxHP;
                        float score = power + enemyHpWeight * (1f - hpRatio);

                        score *= (1f + context.AggressionBias);

                        if (context.AssignedTarget == enemy)
                            score += context.KillTargetBonus;

                        bool preferred = context.PreferredSkill == skill
                            && (context.AssignedTarget == null || context.AssignedTarget == enemy);
                        if (preferred)
                            score += context.PreferredSkillBonus;

                        if (context.SurvivalBias > 0)
                            score -= context.SurvivalBias;

                        var action = AIAction.UseSkill(unit, enemy.Position, skill, equipment.Def, score);
                        candidates.Add(action);

                        if (trace != null)
                        {
                            float baseScore = power + enemyHpWeight * (1f - hpRatio);
                            var terms = new List<ScoreTerm>
                            {
                                new("basePower", power, 1f, power),
                                new("enemyHpMissing", 1f - hpRatio, enemyHpWeight, enemyHpWeight * (1f - hpRatio)),
                            };
                            if (context.AggressionBias != 0)
                                terms.Add(new("aggressionBias", context.AggressionBias, baseScore,
                                    baseScore * context.AggressionBias));
                            if (context.AssignedTarget == enemy)
                                terms.Add(new("killTargetBonus", 1f, context.KillTargetBonus,
                                    context.KillTargetBonus));
                            if (preferred)
                                terms.Add(new("preferredSkill", 1f, context.PreferredSkillBonus,
                                    context.PreferredSkillBonus));
                            if (context.SurvivalBias > 0)
                                terms.Add(new("survivalBias", 1f, -context.SurvivalBias,
                                    -context.SurvivalBias));

                            trace.Add(action, $"{skill.Name} → {enemy}", terms);
                        }
                    }
                }
            }
        }

        private static void ScoreMoves(Unit unit, AIBlackboard blackboard, ScoringContext context,
            List<AIAction> candidates, DecisionTrace? trace)
        {
            var grid = blackboard.Battle.Grid;
            Unit? closestEnemy = FindClosestEnemy(unit, blackboard.Enemies);

            if (closestEnemy == null)
                return;

            float moveStep = context.ActionWeights?.Get("moveStep", 30f) ?? 30f;
            int currentDistance = unit.Position.DistanceTo(closestEnemy.Position);

            foreach (var neighbor in grid.GetNeighbors(unit.Position))
            {
                if (!neighbor.IsWalkable)
                    continue;

                int newDistance = neighbor.Coord.DistanceTo(closestEnemy.Position);
                float score;

                if (context.SurvivalBias > 0)
                    score = (newDistance - currentDistance) * moveStep + context.SurvivalBias;
                else
                    score = (currentDistance - newDistance) * moveStep;

                if (context.HoldPosition is HexCoord hold)
                {
                    int curDist = unit.Position.DistanceTo(hold);
                    int newDist = neighbor.Coord.DistanceTo(hold);
                    score += (curDist - newDist) * context.PositionBonus;
                }

                var action = AIAction.Move(unit, neighbor.Coord, score);
                candidates.Add(action);

                if (trace != null)
                {
                    var terms = new List<ScoreTerm>();
                    if (context.SurvivalBias > 0)
                    {
                        terms.Add(new("retreatDistanceGain", newDistance - currentDistance, moveStep,
                            (newDistance - currentDistance) * moveStep));
                        terms.Add(new("survivalBias", 1f, context.SurvivalBias, context.SurvivalBias));
                    }
                    else
                    {
                        terms.Add(new("approach", currentDistance - newDistance, moveStep,
                            (currentDistance - newDistance) * moveStep));
                    }
                    if (context.HoldPosition is HexCoord h)
                    {
                        int cd = unit.Position.DistanceTo(h);
                        int nd = neighbor.Coord.DistanceTo(h);
                        terms.Add(new("holdPosition", cd - nd, context.PositionBonus,
                            (cd - nd) * context.PositionBonus));
                    }

                    trace.Add(action, $"Move → {neighbor.Coord}", terms);
                }
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

using System.Collections.Generic;
using TacticalGame.AI.Debug;

namespace TacticalGame.AI.Utility
{
    // The one scoring formula used at every layer: score = Σ input × weight.
    // Inputs are considerations normalized to 0..1; weights come from a
    // WeightSet resource. Pure static — safe from any thread.
    public static class UtilityScorer
    {
        public static float Score(
            IReadOnlyList<(string name, float input)> considerations,
            WeightSet weights)
        {
            float total = 0f;
            foreach (var (name, input) in considerations)
                total += input * weights.Get(name);
            return total;
        }

        // Same computation, but also emits per-term breakdowns for debugging.
        public static float Score(
            IReadOnlyList<(string name, float input)> considerations,
            WeightSet weights,
            List<ScoreTerm> terms)
        {
            float total = 0f;
            foreach (var (name, input) in considerations)
            {
                float w = weights.Get(name);
                float contribution = input * w;
                total += contribution;
                terms.Add(new ScoreTerm(name, input, w, contribution));
            }
            return total;
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TacticalGame.AI.Debug
{
    public static class DecisionFormatter
    {
        // Tier 1 console log: top-N candidates with aligned term columns.
        public static string Format(DecisionTrace trace, int topN = 5)
        {
            var sorted = new List<ScoreBreakdown>(trace.Candidates);
            sorted.Sort((a, b) => b.Total.CompareTo(a.Total));

            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture,
                $"[T{trace.TurnNumber}] {trace.Unit} — {sorted.Count} candidates\n");

            int shown = 0;
            foreach (var c in sorted)
            {
                if (shown++ >= topN) break;

                bool chosen = trace.Chosen == c.Action;
                sb.Append(chosen ? "  > " : "    ");
                sb.Append(CultureInfo.InvariantCulture, $"{c.ActionLabel,-28} {c.Total,8:0.0}  ");

                foreach (var t in c.Terms)
                    sb.Append(CultureInfo.InvariantCulture, $"{t.Name}:{t.Contribution:+0.0;-0.0} ");

                sb.Append('\n');
            }

            return sb.ToString();
        }

        // "Why not X?" — per-term delta between the chosen action and the
        // one you expected. The largest gap is the weight to adjust.
        public static string Diff(ScoreBreakdown chosen, ScoreBreakdown wanted)
        {
            var terms = new Dictionary<string, (float chosen, float wanted)>();

            foreach (var t in chosen.Terms)
            {
                terms.TryGetValue(t.Name, out var v);
                terms[t.Name] = (v.chosen + t.Contribution, v.wanted);
            }
            foreach (var t in wanted.Terms)
            {
                terms.TryGetValue(t.Name, out var v);
                terms[t.Name] = (v.chosen, v.wanted + t.Contribution);
            }

            string? biggestTerm = null;
            float biggestGap = 0f;
            foreach (var (name, v) in terms)
            {
                float gap = v.chosen - v.wanted;
                if (biggestTerm == null || gap > biggestGap)
                {
                    biggestTerm = name;
                    biggestGap = gap;
                }
            }

            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture,
                $"Chosen: {chosen.ActionLabel} ({chosen.Total:0.0})   Wanted: {wanted.ActionLabel} ({wanted.Total:0.0})\n");

            foreach (var (name, v) in terms)
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"  {name,-20} {v.chosen,8:+0.0;-0.0;0} | {v.wanted,8:+0.0;-0.0;0}");
                if (name == biggestTerm)
                    sb.Append("  <- biggest gap, adjust this weight");
                sb.Append('\n');
            }

            return sb.ToString();
        }
    }
}

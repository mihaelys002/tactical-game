using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using TacticalGame.AI;
using TacticalGame.AI.Debug;
using TacticalGame.Grid;

namespace TacticalGame.Prototype
{
    // Tier 2 AI debug overlay: candidate panel with stacked term bars,
    // hex heatmap of move scores, layer trace header, disobedience marker.
    // Tier 3: browses any archived turn in DecisionHistory, not just the last.
    public partial class AIDebugOverlay : Node2D
    {
        private static readonly Dictionary<string, Color> TermColors = new()
        {
            ["basePower"] = new Color(0.2f, 0.75f, 0.3f),
            ["enemyHpMissing"] = new Color(0.4f, 0.9f, 0.4f),
            ["aggressionBias"] = new Color(0.1f, 0.55f, 0.25f),
            ["killTargetBonus"] = new Color(0.9f, 0.8f, 0.2f),
            ["approach"] = new Color(0.3f, 0.6f, 0.9f),
            ["holdPosition"] = new Color(0.2f, 0.4f, 0.8f),
            ["survivalBias"] = new Color(0.9f, 0.3f, 0.25f),
            ["retreatDistanceGain"] = new Color(0.95f, 0.55f, 0.2f),
            ["baseSelfCast"] = new Color(0.7f, 0.4f, 0.85f),
            ["ownHpMissing"] = new Color(0.55f, 0.25f, 0.75f),
        };

        private DecisionHistory _history = null!;
        private Func<HexCoord, Vector2> _hexToPixel = null!;
        private float _hexSize;

        public Unit? SelectedUnit { get; set; }
        public int ViewedTurn { get; set; }

        public void Init(DecisionHistory history, Func<HexCoord, Vector2> hexToPixel, float hexSize)
        {
            _history = history;
            _hexToPixel = hexToPixel;
            _hexSize = hexSize;
            ZIndex = 100;
        }

        public void Refresh() => QueueRedraw();

        public override void _Draw()
        {
            if (!Visible || SelectedUnit == null) return;

            var trace = _history.Find(ViewedTurn, SelectedUnit);
            if (trace == null)
            {
                DrawHeader($"[T{ViewedTurn}] {SelectedUnit} — no decision recorded", Colors.Gray);
                return;
            }

            DrawHeatmap(trace);
            DrawCandidatePanel(trace);
            DrawLayerTrace(trace);
            DrawDisobedienceMarker(trace);
        }

        // ── Hex heatmap: move candidates tinted red (worst) → green (best) ──

        private void DrawHeatmap(DecisionTrace trace)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var c in trace.Candidates)
            {
                if (c.Action is not MoveAction) continue;
                min = Math.Min(min, c.Total);
                max = Math.Max(max, c.Total);
            }
            if (min > max) return;

            float span = Math.Max(max - min, 0.001f);
            foreach (var c in trace.Candidates)
            {
                if (c.Action is not MoveAction) continue;

                float t = (c.Total - min) / span;
                var color = new Color(1f - t, t, 0.1f, 0.55f);
                DrawColoredPolygon(HexCorners(_hexToPixel(c.Action.Target)), color);

                var pos = _hexToPixel(c.Action.Target) + new Vector2(-12, 4);
                DrawString(ThemeDB.FallbackFont, pos,
                    c.Total.ToString("0", CultureInfo.InvariantCulture),
                    fontSize: 11, modulate: Colors.Black);
            }
        }

        // ── Candidate panel: sorted list with stacked term bars ─────────────

        private void DrawCandidatePanel(DecisionTrace trace)
        {
            var sorted = new List<ScoreBreakdown>(trace.Candidates);
            sorted.Sort((a, b) => b.Total.CompareTo(a.Total));

            const float panelX = 660f;
            const float panelW = 330f;
            float y = 60f;

            DrawRect(new Rect2(panelX - 10, y - 24, panelW, Math.Min(sorted.Count, 8) * 34 + 34),
                new Color(0f, 0f, 0f, 0.75f));
            DrawString(ThemeDB.FallbackFont, new Vector2(panelX, y - 6),
                string.Create(CultureInfo.InvariantCulture,
                    $"[T{trace.TurnNumber}] {trace.Unit} — {sorted.Count} candidates"),
                fontSize: 13, modulate: Colors.White);
            y += 14;

            // Bar scale: widest candidate (by sum of |contribution|) fills the panel.
            float maxAbs = 0.001f;
            foreach (var c in sorted)
            {
                float abs = 0f;
                foreach (var t in c.Terms) abs += Math.Abs(t.Contribution);
                maxAbs = Math.Max(maxAbs, abs);
            }
            float scale = (panelW - 60f) / maxAbs;

            int shown = 0;
            foreach (var c in sorted)
            {
                if (shown++ >= 8) break;

                bool chosen = trace.Chosen == c.Action;
                DrawString(ThemeDB.FallbackFont, new Vector2(panelX, y + 10),
                    string.Create(CultureInfo.InvariantCulture,
                        $"{(chosen ? "> " : "  ")}{c.ActionLabel}  {c.Total:0.0}"),
                    fontSize: 12, modulate: chosen ? Colors.Yellow : Colors.LightGray);

                float x = panelX + 12;
                foreach (var term in c.Terms)
                {
                    float w = Math.Abs(term.Contribution) * scale;
                    if (w < 0.5f) continue;

                    var color = TermColors.TryGetValue(term.Name, out var tc)
                        ? tc : Colors.Gray;
                    if (term.Contribution < 0)
                        color = color.Darkened(0.45f);

                    DrawRect(new Rect2(x, y + 14, w, 8), color);
                    x += w + 1;
                }

                if (chosen)
                    DrawRect(new Rect2(panelX - 4, y - 2, panelW - 12, 30),
                        Colors.Yellow, filled: false, width: 1f);

                y += 34;
            }
        }

        // ── Layer trace header: what the chain handed down ──────────────────

        private void DrawLayerTrace(DecisionTrace trace)
        {
            var ctx = trace.Context;
            string line = ctx == null
                ? "Layers: (bare utility, no context)"
                : (ctx.StrategyName != null ? $"Strategy: {ctx.StrategyName}  " : "") +
                  (ctx.GoalLabel != null ? $"Goal: {ctx.GoalLabel}  |  " : "") +
                  string.Create(CultureInfo.InvariantCulture,
                    $"Commander: aggr {ctx.AggressionBias:0.0}") +
                  (ctx.PriorityTarget != null ? $", focus {ctx.PriorityTarget}" : "") +
                  (ctx.ForceRetreatUnit == trace.Unit ? ", FORCE RETREAT" : "") +
                  $"  →  Strategy: {ctx.Role}" +
                  (ctx.AssignedTarget != null ? $", target {ctx.AssignedTarget}" : "") +
                  (ctx.HoldPosition != null ? $", hold {ctx.HoldPosition}" : "") +
                  string.Create(CultureInfo.InvariantCulture,
                    $"  →  Goal: kill +{ctx.KillTargetBonus:0}, survive {ctx.SurvivalBias:0}, pos {ctx.PositionBonus:0}");

            DrawHeader(line, Colors.Cyan);
        }

        private void DrawHeader(string text, Color color)
        {
            DrawRect(new Rect2(6, 30, 984, 20), new Color(0f, 0f, 0f, 0.75f));
            DrawString(ThemeDB.FallbackFont, new Vector2(10, 45), text,
                fontSize: 12, modulate: color);
        }

        // ── Disobedience: commander said focus X, unit attacked elsewhere ───

        private void DrawDisobedienceMarker(DecisionTrace trace)
        {
            var ctx = trace.Context;
            if (ctx?.PriorityTarget == null || trace.Chosen is not SkillAction) return;
            if (trace.Chosen.Target == ctx.PriorityTarget.Position) return;

            var pos = _hexToPixel(trace.Unit.Position) + new Vector2(_hexSize * 0.5f, -_hexSize);
            DrawString(ThemeDB.FallbackFont, pos, "!", fontSize: 26, modulate: Colors.Red);
            DrawString(ThemeDB.FallbackFont, pos + new Vector2(12, 0),
                $"disobeys: ignores {ctx.PriorityTarget}", fontSize: 11, modulate: Colors.Red);
        }

        private Vector2[] HexCorners(Vector2 center)
        {
            var corners = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float angleRad = MathF.PI / 180f * (60f * i);
                corners[i] = center + new Vector2(
                    _hexSize * MathF.Cos(angleRad),
                    _hexSize * MathF.Sin(angleRad));
            }
            return corners;
        }
    }
}

using System;
using System.Collections.Generic;
using Godot;
using TacticalGame.Grid;
using TacticalGame.Messages;

namespace TacticalGame.Prototype
{
    // Visualization for the message engine: speech bubbles above units with a
    // procedural unit portrait. Minimal dependencies by design: the message
    // lines, a unit→screen-position function, and the VisualTheme/UnitLooks
    // registries. Knows nothing about grids, planners, or how lines were made.
    //
    //   var layer = new MessageBubbleLayer();
    //   layer.Init(unit => visualizer.HexToPixel(unit.Position));
    //   AddChild(layer);
    //   foreach (var line in engine.DrainLines()) layer.Show(line);
    public partial class MessageBubbleLayer : Node2D
    {
        private sealed class Bubble
        {
            public MessageLine Line { get; }
            public double Age { get; set; }
            public double Lifetime { get; }

            public Bubble(MessageLine line, double lifetime)
            {
                Line = line;
                Lifetime = lifetime;
            }
        }

        private const float MaxTextWidth = 170f;
        private const float Padding = 6f;
        private const float PortraitSize = 32f;
        private const int FontSize = 13;
        private const double FadeTime = 0.5;
        private const float BubbleLift = 36f; // gap between unit and bubble bottom

        private static readonly Dictionary<string, Color> TagBorders = new()
        {
            ["disobedience"] = new Color(0.95f, 0.8f, 0.1f),
            ["revenge"] = new Color(0.85f, 0.1f, 0.15f),
            ["taunt"] = new Color(0.95f, 0.5f, 0.1f),
            ["reply"] = new Color(0.35f, 0.65f, 0.95f),
        };
        private static readonly Color DefaultBorder = new(0.5f, 0.5f, 0.5f);

        // One bubble per speaker — a new line replaces the old one.
        private readonly Dictionary<Unit, Bubble> _bubbles = new();
        private Func<Unit, Vector2> _unitPosition = null!;

        public void Init(Func<Unit, Vector2> unitPosition)
        {
            _unitPosition = unitPosition;
        }

        public void Show(MessageLine line)
        {
            // Longer lines linger longer, capped so bubbles never squat.
            double lifetime = Math.Min(6.0, 2.5 + line.Text.Length * 0.04);
            _bubbles[line.Speaker] = new Bubble(line, lifetime);
            QueueRedraw();
        }

        public void Clear()
        {
            _bubbles.Clear();
            QueueRedraw();
        }

        public override void _Process(double delta)
        {
            if (_bubbles.Count == 0) return;

            var expired = new List<Unit>();
            foreach (var (speaker, bubble) in _bubbles)
            {
                bubble.Age += delta;
                if (bubble.Age >= bubble.Lifetime) expired.Add(speaker);
            }
            foreach (var speaker in expired) _bubbles.Remove(speaker);

            QueueRedraw();
        }

        public override void _Draw()
        {
            foreach (var bubble in _bubbles.Values)
                DrawBubble(bubble);
        }

        private void DrawBubble(Bubble bubble)
        {
            var line = bubble.Line;
            var font = ThemeDB.FallbackFont;
            float alpha = FadeAlpha(bubble);

            var textLines = Wrap(font, line.Text, MaxTextWidth);
            float lineHeight = FontSize + 3f;
            float textWidth = 0f;
            foreach (var t in textLines)
                textWidth = Math.Max(textWidth,
                    font.GetStringSize(t, HorizontalAlignment.Left, -1, FontSize).X);

            // Name row above the text, portrait to the left of both.
            float nameHeight = FontSize + 2f;
            float contentHeight = Math.Max(PortraitSize, nameHeight + textLines.Count * lineHeight);
            float width = PortraitSize + Padding + Math.Max(textWidth, 40f) + Padding * 2;
            float height = contentHeight + Padding * 2;

            var unitPix = _unitPosition(line.Speaker);
            var topLeft = unitPix + new Vector2(-width / 2f, -BubbleLift - height);
            var rect = new Rect2(topLeft, new Vector2(width, height));

            var border = BorderFor(line.Tags);
            DrawPanel(rect, border, alpha);
            DrawTail(unitPix, topLeft, width, height, border, alpha);
            DrawPortrait(topLeft + new Vector2(Padding, (height - PortraitSize) / 2f),
                line.Speaker, border, alpha);

            // Speaker name in team color, then the spoken text.
            float textX = topLeft.X + Padding + PortraitSize + Padding;
            float y = topLeft.Y + Padding + FontSize;
            DrawString(font, new Vector2(textX, y), line.Speaker.ToString(),
                HorizontalAlignment.Left, -1, FontSize,
                WithAlpha(VisualTheme.TeamColor(line.Speaker.TeamIndex), alpha));
            y += nameHeight;

            var textColor = WithAlpha(new Color(0.1f, 0.1f, 0.1f), alpha);
            foreach (var t in textLines)
            {
                DrawString(font, new Vector2(textX, y), t,
                    HorizontalAlignment.Left, -1, FontSize, textColor);
                y += lineHeight;
            }
        }

        private void DrawPanel(Rect2 rect, Color border, float alpha)
        {
            var style = new StyleBoxFlat
            {
                BgColor = WithAlpha(new Color(0.96f, 0.96f, 0.92f), alpha),
                BorderColor = WithAlpha(border, alpha),
            };
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(6);
            style.Draw(GetCanvasItem(), rect);
        }

        private void DrawTail(Vector2 unitPix, Vector2 topLeft, float width, float height,
            Color border, float alpha)
        {
            float baseY = topLeft.Y + height;
            float cx = Math.Clamp(unitPix.X, topLeft.X + 12f, topLeft.X + width - 12f);
            var points = new[]
            {
                new Vector2(cx - 6f, baseY),
                new Vector2(cx + 6f, baseY),
                new Vector2(unitPix.X, baseY + 8f),
            };
            DrawPolygon(points, new[] { WithAlpha(border, alpha) });
        }

        // Procedural portrait from the unit's UnitLookDef: plate, head +
        // shoulders silhouette in the look's body color (team color fallback),
        // optional glyph. Replaced by real art via the same look def later.
        private void DrawPortrait(Vector2 topLeft, Unit unit, Color border, float alpha)
        {
            var look = UnitLooks.Of(unit);

            var rect = new Rect2(topLeft, new Vector2(PortraitSize, PortraitSize));
            var plate = new StyleBoxFlat { BgColor = WithAlpha(new Color(0.15f, 0.15f, 0.18f), alpha) };
            plate.SetCornerRadiusAll(4);
            plate.Draw(GetCanvasItem(), rect);

            var color = WithAlpha(look?.BodyColor ?? VisualTheme.TeamColor(unit.TeamIndex), alpha);
            var center = topLeft + new Vector2(PortraitSize / 2f, PortraitSize / 2f);

            DrawCircle(center + new Vector2(0, -PortraitSize * 0.12f), PortraitSize * 0.2f, color);
            var shoulders = new Rect2(
                topLeft + new Vector2(PortraitSize * 0.2f, PortraitSize * 0.62f),
                new Vector2(PortraitSize * 0.6f, PortraitSize * 0.3f));
            DrawRect(shoulders, color);

            if (look != null && look.Glyph.Length > 0)
                DrawString(ThemeDB.FallbackFont,
                    topLeft + new Vector2(PortraitSize - FontSize * 0.7f, PortraitSize - 3f),
                    look.Glyph, HorizontalAlignment.Left, -1, FontSize - 3,
                    WithAlpha(new Color(1f, 1f, 1f, 0.85f), alpha));

            DrawRect(rect, WithAlpha(border, alpha), filled: false, width: 1f);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static float FadeAlpha(Bubble bubble)
        {
            double remaining = bubble.Lifetime - bubble.Age;
            return remaining >= FadeTime ? 1f : (float)Math.Max(0, remaining / FadeTime);
        }

        private static Color BorderFor(IReadOnlyList<string> tags)
        {
            foreach (var tag in tags)
                if (TagBorders.TryGetValue(tag, out var color)) return color;
            return DefaultBorder;
        }

        private static Color WithAlpha(Color color, float alpha)
            => new(color.R, color.G, color.B, color.A * alpha);

        private static List<string> Wrap(Font font, string text, float maxWidth)
        {
            var lines = new List<string>();
            string current = "";
            foreach (var word in text.Split(' '))
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 &&
                    font.GetStringSize(candidate, HorizontalAlignment.Left, -1, FontSize).X > maxWidth)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }
            if (current.Length > 0) lines.Add(current);
            return lines;
        }
    }
}

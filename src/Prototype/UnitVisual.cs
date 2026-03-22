using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TacticalGame.Grid;

namespace TacticalGame.Prototype
{
    public partial class UnitVisual : Node2D
    {
        private Unit _unit = null!;
        private int _teamIndex;
        private float _hexSize;

        private static readonly Color[] TeamColors =
        {
            new(0.2f, 0.4f, 0.9f),
            new(0.9f, 0.2f, 0.2f),
            new(0.2f, 0.8f, 0.3f),
            new(0.9f, 0.7f, 0.1f),
        };

        private int _visualHP;
        private int _visualArmor;

        public Unit Unit => _unit;
        public bool VisuallyDead => _visualHP <= 0;

        public void Init(Unit unit, int teamIndex, float hexSize, Vector2 position)
        {
            _unit = unit;
            _teamIndex = teamIndex;
            _hexSize = hexSize;
            Position = position;
            _visualHP = unit.Stats.CurrentHP;
            _visualArmor = unit.Stats.CurrentArmor;
        }

        public void SyncToState(Vector2 position)
        {
            Position = position;
            Visible = _unit.IsAlive;
            _visualHP = _unit.Stats.CurrentHP;
            _visualArmor = _unit.Stats.CurrentArmor;
            QueueRedraw();
        }

        public void ApplyVisualDamage(int armorDmg, int hpDmg)
        {
            _visualArmor = Math.Max(0, _visualArmor - armorDmg);
            _visualHP = Math.Max(0, _visualHP - hpDmg);
        }

        public void ApplyVisualHeal(int hpHeal)
        {
            _visualHP = Math.Min(_unit.Stats.MaxHP, _visualHP + hpHeal);
        }

        public async Task PlaySwing(Vector2 targetPos)
        {
            var origin = Position;
            var lunge = origin + (targetPos - origin).Normalized() * _hexSize * 0.4f;
            var tween = CreateTween();
            tween.TweenProperty(this, "position", lunge, 0.08);
            tween.TweenProperty(this, "position", origin, 0.1);
            await ToSignal(tween, Tween.SignalName.Finished);
        }

        public async Task PlayHit(int damage)
        {
            var effects = new List<Task> { FlashWhite(), Shake() };
            if (damage > 0) effects.Add(FloatDamageNumber(damage));
            await Task.WhenAll(effects);
            QueueRedraw();
        }

        private async Task FlashWhite()
        {
            var tween = CreateTween();
            tween.TweenProperty(this, "modulate", new Color(3, 3, 3), 0.05);
            tween.TweenProperty(this, "modulate", Colors.White, 0.15);
            await ToSignal(tween, Tween.SignalName.Finished);
        }

        private async Task Shake()
        {
            var origin = Position;
            float d = _hexSize * 0.1f;
            float t = 0.03f;
            var tween = CreateTween();
            tween.TweenProperty(this, "position", origin + new Vector2(d, 0), t);
            tween.TweenProperty(this, "position", origin - new Vector2(d, 0), t);
            tween.TweenProperty(this, "position", origin + new Vector2(0, d), t);
            tween.TweenProperty(this, "position", origin - new Vector2(0, d), t);
            tween.TweenProperty(this, "position", origin, t);
            await ToSignal(tween, Tween.SignalName.Finished);
        }

        private async Task FloatDamageNumber(int damage)
        {
            var label = new Label();
            label.Text = $"-{damage}";
            label.Position = new Vector2(-_hexSize * 0.2f, -_hexSize * 0.5f);
            label.AddThemeColorOverride("font_color", new Color(1f, 0.2f, 0.2f));
            label.AddThemeFontSizeOverride("font_size", (int)(_hexSize * 0.4f));
            AddChild(label);

            var tween = CreateTween();
            tween.SetParallel();
            tween.TweenProperty(label, "position:y", -_hexSize * 1.2f, 0.5);
            tween.TweenProperty(label, "modulate:a", 0f, 0.5);
            await ToSignal(tween, Tween.SignalName.Finished);

            label.QueueFree();
        }

        public Task PlayDeath()
        {
            Visible = false;
            return Task.CompletedTask;
        }

        public Task AnimateMoveTo(Vector2 target)
        {
            // Future: tween position
            Position = target;
            return Task.CompletedTask;
        }

        public override void _Draw()
        {
            Color color = _teamIndex >= 0 && _teamIndex < TeamColors.Length
                ? TeamColors[_teamIndex]
                : Colors.White;

            DrawCircle(Vector2.Zero, _hexSize * 0.4f, color);

            float barWidth = _hexSize * 0.8f;
            float barHeight = 2.5f;
            var armorPos = new Vector2(-barWidth / 2, _hexSize * 0.3f);

            if (_unit.Stats.MaxArmor > 0)
            {
                float armorRatio = Math.Max(0, (float)_visualArmor / _unit.Stats.MaxArmor);
                DrawRect(new Rect2(armorPos, new Vector2(barWidth, barHeight)), new Color(0.2f, 0.2f, 0.2f));
                DrawRect(new Rect2(armorPos, new Vector2(barWidth * armorRatio, barHeight)), new Color(0.6f, 0.6f, 0.8f));
            }

            float hpRatio = Math.Max(0, (float)_visualHP / _unit.Stats.MaxHP);
            var hpPos = armorPos + new Vector2(0, barHeight + 1);
            DrawRect(new Rect2(hpPos, new Vector2(barWidth, barHeight)), new Color(0.3f, 0.0f, 0.0f));
            DrawRect(new Rect2(hpPos, new Vector2(barWidth * hpRatio, barHeight)), new Color(0.0f, 0.8f, 0.0f));

            // Equipment icons — small letters around the unit circle
            float iconRadius = _hexSize * 0.55f;
            var font = ThemeDB.FallbackFont;
            int fontSize = (int)(_hexSize * 0.28f);
            var iconColor = new Color(1f, 1f, 1f, 0.9f);

            foreach (var eq in _unit.Equipment.Values)
            {
                float angle = eq.Def.Slot switch
                {
                    EquipmentSlot.RightHand => 0f,
                    EquipmentSlot.LeftHand => MathF.PI,
                    EquipmentSlot.Helmet => -MathF.PI / 2f,
                    EquipmentSlot.Torso => MathF.PI / 2f,
                    EquipmentSlot.Amulet => -MathF.PI / 4f,
                    _ => 0f
                };

                string label = eq.Def.Name.Length > 0 ? eq.Def.Name[..1] : "?";
                var pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * iconRadius;
                DrawString(font, pos - new Vector2(fontSize * 0.3f, -fontSize * 0.3f), label, HorizontalAlignment.Left, -1, fontSize, iconColor);
            }
        }
    }
}

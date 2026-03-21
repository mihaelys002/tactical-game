using System;
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

        public Unit Unit => _unit;

        public void Init(Unit unit, int teamIndex, float hexSize, Vector2 position)
        {
            _unit = unit;
            _teamIndex = teamIndex;
            _hexSize = hexSize;
            Position = position;
        }

        public void SyncToState(Vector2 position)
        {
            Position = position;
            Visible = _unit.IsAlive;
            QueueRedraw();
        }

        public Task PlaySwing()
        {
            // Future: play swing animation using instance sprite/animation
            QueueRedraw();
            return Task.CompletedTask;
        }

        public Task PlayHit()
        {
            // Future: flash red, play particle effect
            QueueRedraw();
            return Task.CompletedTask;
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

            var stats = _unit.Stats;
            float barWidth = _hexSize * 0.8f;
            float barHeight = 2.5f;
            var armorPos = new Vector2(-barWidth / 2, _hexSize * 0.3f);

            if (stats.MaxArmor > 0)
            {
                float armorRatio = Math.Max(0, (float)stats.CurrentArmor / stats.MaxArmor);
                DrawRect(new Rect2(armorPos, new Vector2(barWidth, barHeight)), new Color(0.2f, 0.2f, 0.2f));
                DrawRect(new Rect2(armorPos, new Vector2(barWidth * armorRatio, barHeight)), new Color(0.6f, 0.6f, 0.8f));
            }

            float hpRatio = Math.Max(0, (float)stats.CurrentHP / stats.MaxHP);
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

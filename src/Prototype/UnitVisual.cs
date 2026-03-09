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
            // Future: play swing animation
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
        }
    }
}

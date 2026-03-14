using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TacticalGame.Grid;

namespace TacticalGame.Prototype
{
    public abstract class CommandVisual
    {
        public abstract Task Play();
    }

    public class MoveVisual : CommandVisual
    {
        private readonly UnitVisual _unitVisual;
        private readonly Godot.Vector2 _targetPixel;

        public MoveVisual(MoveCommand cmd, UnitVisual unitVisual, Func<HexCoord, Godot.Vector2> hexToPixel)
        {
            _unitVisual = unitVisual;
            _targetPixel = hexToPixel(cmd.To);
        }

        public override async Task Play()
        {
            await _unitVisual.AnimateMoveTo(_targetPixel);
        }
    }

    public class CompoundVisual : CommandVisual
    {
        private readonly UnitVisual _userVisual;
        private readonly List<(UnitVisual visual, BattleEffect effect)> _targetEffects;
        private readonly bool _hasDamage;

        public CompoundVisual(CompoundCommand cmd, Dictionary<Unit, UnitVisual> visuals)
        {
            _userVisual = visuals[cmd.Unit];
            _targetEffects = new List<(UnitVisual, BattleEffect)>();

            foreach (var effect in cmd.Effects)
            {
                if (effect is DamageEffect dmg && visuals.TryGetValue(dmg.Target, out var dv))
                {
                    _targetEffects.Add((dv, effect));
                    _hasDamage = true;
                }
                else if (effect is HealEffect heal && visuals.TryGetValue(heal.Target, out var hv))
                {
                    _targetEffects.Add((hv, effect));
                }
            }
        }

        public override async Task Play()
        {
            if (_hasDamage)
                await _userVisual.PlaySwing();

            foreach (var (visual, effect) in _targetEffects)
            {
                if (effect is DamageEffect)
                {
                    await visual.PlayHit();
                    if (!visual.Unit.IsAlive)
                        await visual.PlayDeath();
                }
                else if (effect is HealEffect)
                {
                    visual.QueueRedraw();
                }
            }

            // Self-targeting skills (like ShieldWall) — just redraw the user
            if (!_hasDamage)
                _userVisual.QueueRedraw();
        }
    }

    public static class CommandVisualFactory
    {
        public static CommandVisual Create(
            IBattleCommand cmd,
            Dictionary<Unit, UnitVisual> visuals,
            Func<HexCoord, Godot.Vector2> hexToPixel)
        {
            return cmd switch
            {
                MoveCommand move => new MoveVisual(move, visuals[move.Unit], hexToPixel),
                CompoundCommand compound => new CompoundVisual(compound, visuals),
                _ => throw new ArgumentException($"Unknown command type: {cmd.GetType()}")
            };
        }
    }
}

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

    public class AttackVisual : CommandVisual
    {
        private readonly UnitVisual _attackerVisual;
        private readonly UnitVisual _targetVisual;

        public AttackVisual(AttackCommand cmd, UnitVisual attackerVisual, UnitVisual targetVisual)
        {
            _attackerVisual = attackerVisual;
            _targetVisual = targetVisual;
        }

        public override async Task Play()
        {
            await _attackerVisual.PlaySwing();
            await _targetVisual.PlayHit();

            if (!_targetVisual.Unit.IsAlive)
                await _targetVisual.PlayDeath();
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
                AttackCommand attack => new AttackVisual(attack, visuals[attack.Unit], visuals[attack.Target]),
                _ => throw new ArgumentException($"Unknown command type: {cmd.GetType()}")
            };
        }
    }
}

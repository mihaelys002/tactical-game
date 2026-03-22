using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TacticalGame.Grid;

namespace TacticalGame.Prototype
{
    public class BattleOrchestrator
    {
        private readonly Dictionary<Unit, UnitVisual> _visuals;
        private readonly Func<HexCoord, Godot.Vector2> _hexToPixel;
        private readonly Queue<IBattleCommand> _pending = new();

        private const int BatchSize = 5;

        public bool HasPending => _pending.Count > 0;
        public int PendingCount => _pending.Count;

        public BattleOrchestrator(Dictionary<Unit, UnitVisual> visuals, Func<HexCoord, Godot.Vector2> hexToPixel)
        {
            _visuals = visuals;
            _hexToPixel = hexToPixel;
        }

        public void Enqueue(List<IBattleCommand> commands)
        {
            foreach (var cmd in commands)
                if (HasVisualEffect(cmd))
                    _pending.Enqueue(cmd);
        }

        public async Task PlayBatch()
        {
            int count = Math.Min(BatchSize, _pending.Count);
            for (int i = 0; i < count; i++)
            {
                var cmd = _pending.Dequeue();
                var visual = CommandVisualFactory.Create(cmd, _visuals, _hexToPixel);
                if (visual != null) await visual.Play();
            }
        }

        private static bool HasVisualEffect(IBattleCommand cmd)
        {
            if (cmd is MoveCommand) return true;
            if (cmd is CompoundCommand cc)
                return cc.Effects.Exists(e => e is DamageEffect or HealEffect);
            return false;
        }

        public void SyncAll()
        {
            _pending.Clear();
            foreach (var (unit, visual) in _visuals)
                visual.SyncToState(_hexToPixel(unit.Position));
        }
    }
}

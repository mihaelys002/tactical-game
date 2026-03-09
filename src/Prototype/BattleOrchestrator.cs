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

        public BattleOrchestrator(Dictionary<Unit, UnitVisual> visuals, Func<HexCoord, Godot.Vector2> hexToPixel)
        {
            _visuals = visuals;
            _hexToPixel = hexToPixel;
        }

        public async Task PlayTurn(List<IBattleCommand> commands)
        {
            foreach (var cmd in commands)
            {
                var visual = CommandVisualFactory.Create(cmd, _visuals, _hexToPixel);
                await visual.Play();
            }
        }

        public void SyncAll()
        {
            foreach (var (unit, visual) in _visuals)
                visual.SyncToState(_hexToPixel(unit.Position));
        }
    }
}

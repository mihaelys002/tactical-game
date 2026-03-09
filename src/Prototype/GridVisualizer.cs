using System;
using System.Collections.Generic;
using Godot;
using TacticalGame.Grid;

namespace TacticalGame.Prototype
{
    public partial class GridVisualizer : Node2D
    {
        private HexGrid? _grid;
        private float _hexSize = 32f;
        private Vector2 _offset = new(500, 350);
        private readonly Dictionary<Unit, UnitVisual> _unitVisuals = new();

        public IReadOnlyDictionary<Unit, UnitVisual> UnitVisuals => _unitVisuals;

        public Vector2 HexToPixel(HexCoord coord)
        {
            float x = _hexSize * (1.5f * coord.Q);
            float y = _hexSize * (MathF.Sqrt(3f) / 2f * coord.Q + MathF.Sqrt(3f) * coord.R);
            return new Vector2(x, y) + _offset;
        }

        public void SetState(BattleManager manager)
        {
            _grid = manager.Battle.Grid;

            foreach (var visual in _unitVisuals.Values)
                visual.QueueFree();
            _unitVisuals.Clear();

            for (int t = 0; t < manager.TeamCount; t++)
            {
                foreach (var unit in manager.Teams[t])
                {
                    var visual = new UnitVisual();
                    visual.Init(unit, t, _hexSize, HexToPixel(unit.Position));
                    AddChild(visual);
                    _unitVisuals[unit] = visual;
                }
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_grid == null) return;

            foreach (var cell in _grid.AllCells)
            {
                var center = HexToPixel(cell.Coord);
                var points = HexCorners(center);

                Color fill = cell.Terrain switch
                {
                    TerrainType.Plain => new Color(0.75f, 0.85f, 0.55f),
                    TerrainType.Forest => new Color(0.3f, 0.6f, 0.3f),
                    TerrainType.Hill => new Color(0.65f, 0.55f, 0.4f),
                    TerrainType.Water => new Color(0.3f, 0.5f, 0.8f),
                    TerrainType.Wall => new Color(0.4f, 0.4f, 0.4f),
                    _ => Colors.White
                };

                DrawColoredPolygon(points, fill);
                DrawPolyline(AppendFirst(points), new Color(0.2f, 0.2f, 0.2f), 1.5f);
            }
        }

        private Vector2[] HexCorners(Vector2 center)
        {
            var corners = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float angleDeg = 60f * i;
                float angleRad = MathF.PI / 180f * angleDeg;
                corners[i] = center + new Vector2(
                    _hexSize * MathF.Cos(angleRad),
                    _hexSize * MathF.Sin(angleRad));
            }
            return corners;
        }

        private static Vector2[] AppendFirst(Vector2[] points)
        {
            var closed = new Vector2[points.Length + 1];
            points.CopyTo(closed, 0);
            closed[points.Length] = points[0];
            return closed;
        }
    }
}

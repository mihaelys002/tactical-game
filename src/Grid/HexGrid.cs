using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TacticalGame.Grid
{
    public class HexGrid
    {
        private readonly Dictionary<HexCoord, HexCell> _cells = new();

        public int Count => _cells.Count;

        public void AddCell(HexCoord coord, TerrainType terrain, int elevation)
        {
            _cells[coord] = new HexCell(coord, terrain, elevation);
        }

        public bool TryGetCell(HexCoord coord, [NotNullWhen(true)] out HexCell? cell)
        {
            return _cells.TryGetValue(coord, out cell);
        }

        public HexCell? GetCell(HexCoord coord)
        {
            _cells.TryGetValue(coord, out var cell);
            return cell;
        }

        public bool HasCell(HexCoord coord) => _cells.ContainsKey(coord);

        public IEnumerable<HexCell> GetNeighbors(HexCoord coord)
        {
            foreach (var neighbor in coord.Neighbors())
            {
                if (_cells.TryGetValue(neighbor, out var cell))
                    yield return cell;
            }
        }

        public IEnumerable<HexCell> AllCells => _cells.Values;
    }
}

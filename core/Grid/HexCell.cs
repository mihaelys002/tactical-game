using System.Collections.Generic;
using System.Linq;

namespace TacticalGame.Grid
{
    public class HexCell
    {
        private readonly List<Unit> _occupants = new();

        public HexCoord Coord { get; }
        public TerrainType Terrain { get; }
        public int Elevation { get; }
        public IReadOnlyList<Unit> Occupants => _occupants;
        public bool IsWalkable => !_occupants.Any(u => u.IsAlive);
        public Loot? Loot { get; internal set; }

        internal void AddOccupant(Unit unit) => _occupants.Add(unit);
        internal void RemoveOccupant(Unit unit) => _occupants.Remove(unit);

        public HexCell(HexCoord coord, TerrainType terrain, int elevation)
        {
            Coord = coord;
            Terrain = terrain;
            Elevation = elevation;
        }

    }
}

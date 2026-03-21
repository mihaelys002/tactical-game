using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TacticalGame.Grid
{
    public class HexCell
    {
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Auto)]
        private readonly List<Unit> _occupants = new();

        public HexCoord Coord { get; }
        public TerrainType Terrain { get; }
        public int Elevation { get; }
        [JsonIgnore]
        public IReadOnlyList<Unit> Occupants => _occupants;
        [JsonIgnore]
        public bool IsWalkable => !_occupants.Any(u => u.IsAlive);

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

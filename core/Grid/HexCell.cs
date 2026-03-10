using System.Collections.Generic;
using System.IO;
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

        public void WriteTo(BinaryWriter writer)
        {
            Coord.WriteTo(writer);
            writer.Write((int)Terrain);
            writer.Write(Elevation);
        }

        public static HexCell ReadFrom(BinaryReader reader)
        {
            var coord = HexCoord.ReadFrom(reader);
            var terrain = (TerrainType)reader.ReadInt32();
            var elevation = reader.ReadInt32();
            return new HexCell(coord, terrain, elevation);
        }
    }
}

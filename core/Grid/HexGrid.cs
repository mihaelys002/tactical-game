using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TacticalGame.Grid
{
    public class HexGrid
    {
        [JsonIgnore]
        public Dictionary<HexCoord, HexCell> Cells { get; } = new();

        [JsonProperty]
        private List<HexCell> _serializedCells
        {
            get => Cells.Values.ToList();
            init
            {
                Cells.Clear();
                if (value == null) return;
                foreach (var cell in value)
                    Cells[cell.Coord] = cell;
            }
        }

        public IEnumerable<HexCell> GetNeighbors(HexCoord coord)
        {
            foreach (var neighbor in coord.Neighbors())
            {
                if (Cells.TryGetValue(neighbor, out var cell))
                    yield return cell;
            }
        }
    }
}

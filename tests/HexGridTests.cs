using System.Linq;
using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class HexGridTests
    {
        [Fact]
        public void AddCell_IncreasesCount()
        {
            var grid = new HexGrid();
            grid.Cells[HexCoord.Zero] = new HexCell(HexCoord.Zero, TerrainType.Plain, 0);
            Assert.Equal(1, grid.Cells.Count);
        }

        [Fact]
        public void GetCell_ReturnsNull_ForMissingCoord()
        {
            var grid = new HexGrid();
            Assert.False(grid.Cells.ContainsKey(new HexCoord(5, 5)));
        }

        [Fact]
        public void GetCell_ReturnsAddedCell()
        {
            var grid = new HexGrid();
            var coord = new HexCoord(1, 2);
            grid.Cells[coord] = new HexCell(coord, TerrainType.Forest, 0);
            Assert.Equal(TerrainType.Forest, grid.Cells[coord].Terrain);
        }

        [Fact]
        public void HasCell_ReturnsTrueForExisting()
        {
            var grid = new HexGrid();
            grid.Cells[HexCoord.Zero] = new HexCell(HexCoord.Zero, TerrainType.Plain, 0);
            Assert.True(grid.Cells.ContainsKey(HexCoord.Zero));
            Assert.False(grid.Cells.ContainsKey(new HexCoord(99, 99)));
        }

        [Fact]
        public void GetNeighbors_ReturnsOnlyCellsThatExist()
        {
            var grid = new HexGrid();
            grid.Cells[HexCoord.Zero] = new HexCell(HexCoord.Zero, TerrainType.Plain, 0);
            var c1 = new HexCoord(1, 0);
            var c2 = new HexCoord(-1, 0);
            grid.Cells[c1] = new HexCell(c1, TerrainType.Plain, 0);
            grid.Cells[c2] = new HexCell(c2, TerrainType.Plain, 0);

            var neighbors = grid.GetNeighbors(HexCoord.Zero).ToList();
            Assert.Equal(2, neighbors.Count);
        }

        [Fact]
        public void AddCell_OverwritesSameCoord()
        {
            var grid = new HexGrid();
            grid.Cells[HexCoord.Zero] = new HexCell(HexCoord.Zero, TerrainType.Plain, 0);
            grid.Cells[HexCoord.Zero] = new HexCell(HexCoord.Zero, TerrainType.Forest, 1);

            Assert.Equal(1, grid.Cells.Count);
            Assert.Equal(TerrainType.Forest, grid.Cells[HexCoord.Zero].Terrain);
            Assert.Equal(1, grid.Cells[HexCoord.Zero].Elevation);
        }

        [Fact]
        public void AllCells_ReturnsEveryCell()
        {
            var grid = TestHelpers.MakeGrid(2);
            Assert.Equal(grid.Cells.Count, grid.Cells.Values.Count());
        }
    }
}

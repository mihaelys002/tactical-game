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
            grid.AddCell(new HexCoord(0, 0), TerrainType.Plain, 0);
            Assert.Equal(1, grid.Count);
        }

        [Fact]
        public void GetCell_ReturnsNull_ForMissingCoord()
        {
            var grid = new HexGrid();
            Assert.Null(grid.GetCell(new HexCoord(5, 5)));
        }

        [Fact]
        public void GetCell_ReturnsAddedCell()
        {
            var grid = new HexGrid();
            grid.AddCell(new HexCoord(1, 2), TerrainType.Forest, 0);
            var cell = grid.GetCell(new HexCoord(1, 2));
            Assert.NotNull(cell);
            Assert.Equal(TerrainType.Forest, cell.Terrain);
        }

        [Fact]
        public void HasCell_ReturnsTrueForExisting()
        {
            var grid = new HexGrid();
            grid.AddCell(HexCoord.Zero, TerrainType.Plain, 0);
            Assert.True(grid.HasCell(HexCoord.Zero));
            Assert.False(grid.HasCell(new HexCoord(99, 99)));
        }

        [Fact]
        public void GetNeighbors_ReturnsOnlyCellsThatExist()
        {
            var grid = new HexGrid();
            // Add center and only 2 of its 6 neighbors
            grid.AddCell(HexCoord.Zero, TerrainType.Plain, 0);
            grid.AddCell(new HexCoord(1, 0), TerrainType.Plain, 0);
            grid.AddCell(new HexCoord(-1, 0), TerrainType.Plain, 0);

            var neighbors = grid.GetNeighbors(HexCoord.Zero).ToList();
            Assert.Equal(2, neighbors.Count);
        }

        [Fact]
        public void AddCell_OverwritesSameCoord()
        {
            var grid = new HexGrid();
            grid.AddCell(HexCoord.Zero, TerrainType.Plain, 0);
            grid.AddCell(HexCoord.Zero, TerrainType.Forest, 1);

            Assert.Equal(1, grid.Count);
            var cell = grid.GetCell(HexCoord.Zero);
            Assert.Equal(TerrainType.Forest, cell!.Terrain);
            Assert.Equal(1, cell.Elevation);
        }

        [Fact]
        public void AllCells_ReturnsEveryCell()
        {
            var grid = TestHelpers.MakeGrid(2);
            int count = grid.AllCells.Count();
            Assert.Equal(grid.Count, count);
        }
    }
}

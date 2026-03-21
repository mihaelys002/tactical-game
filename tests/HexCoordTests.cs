using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class HexCoordTests
    {
        // ── Axial identity ──────────────────────────────────────────────

        [Fact]
        public void S_IsImplicit_NegativeQMinusR()
        {
            var hex = new HexCoord(2, -3);
            Assert.Equal(1, hex.S); // s = -2 - (-3) = 1
        }

        [Fact]
        public void Zero_HasAllComponentsZero()
        {
            Assert.Equal(0, HexCoord.Zero.Q);
            Assert.Equal(0, HexCoord.Zero.R);
            Assert.Equal(0, HexCoord.Zero.S);
        }

        // ── Equality & hashing (critical — used as Dictionary key) ──────

        [Fact]
        public void Equal_Coords_AreEqual()
        {
            var a = new HexCoord(3, -1);
            var b = new HexCoord(3, -1);
            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Different_Coords_AreNotEqual()
        {
            var a = new HexCoord(1, 2);
            var b = new HexCoord(2, 1);
            Assert.NotEqual(a, b);
            Assert.True(a != b);
        }

        [Fact]
        public void Equals_WorksWithObjectOverload()
        {
            var a = new HexCoord(1, 1);
            object b = new HexCoord(1, 1);
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void Equals_ReturnsFalse_ForNonHexCoord()
        {
            var a = new HexCoord(1, 1);
            Assert.False(a.Equals("not a hex"));
        }

        // ── Arithmetic operators ────────────────────────────────────────

        [Fact]
        public void Addition()
        {
            var result = new HexCoord(1, 2) + new HexCoord(3, -1);
            Assert.Equal(new HexCoord(4, 1), result);
        }

        [Fact]
        public void Subtraction()
        {
            var result = new HexCoord(5, 3) - new HexCoord(2, 1);
            Assert.Equal(new HexCoord(3, 2), result);
        }

        [Fact]
        public void ScalarMultiply_LeftAndRight()
        {
            var hex = new HexCoord(2, -1);
            Assert.Equal(new HexCoord(6, -3), hex * 3);
            Assert.Equal(new HexCoord(6, -3), 3 * hex);
        }

        [Fact]
        public void DotProduct_UsesAllThreeAxes()
        {
            // dot = a.Q*b.Q + a.R*b.R + a.S*b.S
            var a = new HexCoord(1, 0); // S = -1
            var b = new HexCoord(1, 0); // S = -1
            // 1*1 + 0*0 + (-1)*(-1) = 2
            Assert.Equal(2, a * b);
        }

        // ── Distance ────────────────────────────────────────────────────

        [Fact]
        public void Distance_SameHex_IsZero()
        {
            var hex = new HexCoord(3, -2);
            Assert.Equal(0, hex.DistanceTo(hex));
        }

        [Fact]
        public void Distance_AdjacentHex_IsOne()
        {
            Assert.Equal(1, HexCoord.Zero.DistanceTo(new HexCoord(1, 0)));
        }

        [Fact]
        public void Distance_IsSymmetric()
        {
            var a = new HexCoord(3, -2);
            var b = new HexCoord(-1, 4);
            Assert.Equal(a.DistanceTo(b), b.DistanceTo(a));
        }

        [Fact]
        public void Distance_AcrossOrigin()
        {
            // (3,0) to (-3,0) should be 6
            Assert.Equal(6, new HexCoord(3, 0).DistanceTo(new HexCoord(-3, 0)));
        }

        // ── Neighbors ───────────────────────────────────────────────────

        [Fact]
        public void Neighbors_ReturnsExactlySix()
        {
            var neighbors = new System.Collections.Generic.List<HexCoord>(HexCoord.Zero.Neighbors());
            Assert.Equal(6, neighbors.Count);
        }

        [Fact]
        public void Neighbors_AllAtDistanceOne()
        {
            foreach (var n in HexCoord.Zero.Neighbors())
                Assert.Equal(1, HexCoord.Zero.DistanceTo(n));
        }

        [Fact]
        public void Neighbor_ByDirection_MatchesDirectionsArray()
        {
            for (int i = 0; i < 6; i++)
                Assert.Equal(HexCoord.Zero + HexCoord.Directions[i], HexCoord.Zero.Neighbor(i));
        }

        [Fact]
        public void Neighbor_WrapsDirectionIndex()
        {
            // direction 6 should wrap to 0 (East)
            Assert.Equal(HexCoord.Zero.Neighbor(0), HexCoord.Zero.Neighbor(6));
        }

        // ── Directions array ────────────────────────────────────────────

        [Fact]
        public void Directions_HasSixEntries()
        {
            Assert.Equal(6, HexCoord.Directions.Length);
        }

        [Fact]
        public void OppositeDirections_CancelOut()
        {
            // Direction i and i+3 should be opposites
            for (int i = 0; i < 3; i++)
            {
                var sum = HexCoord.Directions[i] + HexCoord.Directions[i + 3];
                Assert.Equal(HexCoord.Zero, sum);
            }
        }
    }
}

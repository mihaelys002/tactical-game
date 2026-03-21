using System.Collections.Generic;
using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class HitPatternTests
    {
        // ── SingleTarget ────────────────────────────────────────────────

        [Fact]
        public void SingleTarget_East_HitsTargetHex()
        {
            var user = HexCoord.Zero;
            var target = new HexCoord(1, 0); // east
            var hits = HitPattern.SingleTarget.Resolve(user, target);

            Assert.Single(hits);
            Assert.Equal(target, hits[0]);
        }

        [Fact]
        public void SingleTarget_West_HitsTargetHex()
        {
            var user = HexCoord.Zero;
            var target = new HexCoord(-1, 0); // west
            var hits = HitPattern.SingleTarget.Resolve(user, target);

            Assert.Single(hits);
            Assert.Equal(target, hits[0]);
        }

        [Fact]
        public void SingleTarget_AllSixDirections_HitsCorrectNeighbor()
        {
            var user = HexCoord.Zero;
            foreach (var dir in HexCoord.Directions)
            {
                var hits = HitPattern.SingleTarget.Resolve(user, dir);
                Assert.Single(hits);
                Assert.Equal(dir, hits[0]);
            }
        }

        // ── Line2 ───────────────────────────────────────────────────────

        [Fact]
        public void Line2_East_HitsTwoHexesInLine()
        {
            var user = HexCoord.Zero;
            var target = new HexCoord(1, 0);
            var hits = HitPattern.Line2.Resolve(user, target);

            Assert.Equal(2, hits.Count);
            Assert.Contains(new HexCoord(1, 0), hits);
            Assert.Contains(new HexCoord(2, 0), hits);
        }

        [Fact]
        public void Line2_West_RotatesCorrectly()
        {
            var user = HexCoord.Zero;
            var target = new HexCoord(-1, 0); // west
            var hits = HitPattern.Line2.Resolve(user, target);

            Assert.Equal(2, hits.Count);
            Assert.Contains(new HexCoord(-1, 0), hits);
            Assert.Contains(new HexCoord(-2, 0), hits);
        }

        // ── Sweep3 ─────────────────────────────────────────────────────

        [Fact]
        public void Sweep3_East_HitsThreeAdjacentHexes()
        {
            var user = HexCoord.Zero;
            var target = new HexCoord(1, 0); // east
            var hits = HitPattern.Sweep3.Resolve(user, target);

            Assert.Equal(3, hits.Count);
            // Sweep authored as (1,-1), (1,0), (0,1) — facing east, no rotation
            Assert.Contains(new HexCoord(1, -1), hits); // NE
            Assert.Contains(new HexCoord(1, 0), hits);  // E
            Assert.Contains(new HexCoord(0, 1), hits);  // SE
        }

        // ── Custom pattern ──────────────────────────────────────────────

        [Fact]
        public void CustomPattern_Resolves_FromNonOriginPosition()
        {
            // Single target from a non-origin position
            var user = new HexCoord(3, 2);
            var target = new HexCoord(4, 2); // east of user
            var hits = HitPattern.SingleTarget.Resolve(user, target);

            Assert.Single(hits);
            Assert.Equal(target, hits[0]);
        }

        // ── Rotation consistency ────────────────────────────────────────

        [Fact]
        public void Line2_AllDirections_AlwaysHitsTwoHexes()
        {
            var user = HexCoord.Zero;
            foreach (var dir in HexCoord.Directions)
            {
                var hits = HitPattern.Line2.Resolve(user, dir);
                Assert.Equal(2, hits.Count);
                // First hex should be at distance 1, second at distance 2
                Assert.Equal(1, user.DistanceTo(hits[0]));
                Assert.Equal(2, user.DistanceTo(hits[1]));
            }
        }
    }
}

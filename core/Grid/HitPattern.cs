using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public class HitPattern
    {
        /// <summary>
        /// Offsets as HexCoord deltas, authored facing East (direction 0).
        /// Resolve rotates them to match the actual attack direction.
        /// (1,0) = 1 hex forward, (2,0) = 2 hexes forward, (1,-1) = forward-left, etc.
        /// </summary>
        public IReadOnlyList<HexCoord> Offsets { get; }

        public HitPattern(IReadOnlyList<HexCoord> offsets)
        {
            Offsets = offsets;
        }

        /// <summary>Just the targeted hex.</summary>
        public static readonly HitPattern SingleTarget = new(new[] { new HexCoord(1, 0) });

        /// <summary>Line of 2: targeted hex + 1 hex behind it (spear thrust).</summary>
        public static readonly HitPattern Line2 = new(new[] { new HexCoord(1, 0), new HexCoord(2, 0) });

        /// <summary>Sweep of 3 adjacent hexes (wide slash).</summary>
        public static readonly HitPattern Sweep3 = new(new[]
        {
            new HexCoord(1, -1),
            new HexCoord(1, 0),
            new HexCoord(0, 1)
        });

        /// <summary>
        /// Given the user's position and a targeted hex, resolve which absolute
        /// hex coords are hit.
        /// </summary>
        public List<HexCoord> Resolve(HexCoord userPos, HexCoord targetHex)
        {
            int dir = FindDirection(userPos, targetHex);
            var results = new List<HexCoord>(Offsets.Count);

            foreach (var offset in Offsets)
            {
                var rotated = Rotate(offset, dir);
                results.Add(userPos + rotated);
            }

            return results;
        }

        /// <summary>
        /// Rotate a hex offset by N * 60 degrees counter-clockwise.
        /// CCW matches the direction array ordering (E, NE, NW, W, SW, SE).
        /// Rotation formula for axial coords: rotate60CCW(q, r) = (q + r, -q)
        /// </summary>
        private static HexCoord Rotate(HexCoord offset, int steps)
        {
            steps = ((steps % 6) + 6) % 6;
            var h = offset;
            for (int i = 0; i < steps; i++)
                h = new HexCoord(h.Q + h.R, -h.Q);
            return h;
        }

        private static int FindDirection(HexCoord from, HexCoord to)
        {
            var delta = to - from;
            int bestDir = 0;
            int bestDot = int.MinValue;
            for (int i = 0; i < 6; i++)
            {
                var d = HexCoord.Directions[i];
                int dot = delta * d;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestDir = i;
                }
            }
            return bestDir;
        }
    }
}

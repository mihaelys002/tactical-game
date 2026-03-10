using System;
using System.Collections.Generic;

namespace TacticalGame.Grid
{
    /// <summary>
    /// Axial hex coordinate (q, r). Flat-top orientation.
    /// S axis is implicit: s = -q - r.
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public int Q { get; }
        public int R { get; }
        public int S => -Q - R;

        public static readonly HexCoord Zero = new(0, 0);

        // Flat-top axial directions: E, NE, NW, W, SW, SE
        public static readonly HexCoord[] Directions =
        {
            new( 1,  0),  // 0: E
            new( 1, -1),  // 1: NE
            new( 0, -1),  // 2: NW
            new(-1,  0),  // 3: W
            new(-1,  1),  // 4: SW
            new( 0,  1),  // 5: SE
        };

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public HexCoord Neighbor(int direction) => this + Directions[direction % 6];

        public IEnumerable<HexCoord> Neighbors()
        {
            foreach (var dir in Directions)
                yield return this + dir;
        }

        public int DistanceTo(HexCoord other)
        {
            var d = this - other;
            return (Math.Abs(d.Q) + Math.Abs(d.R) + Math.Abs(d.Q + d.R)) / 2;
        }

        public static HexCoord operator +(HexCoord a, HexCoord b) => new(a.Q + b.Q, a.R + b.R);
        public static HexCoord operator -(HexCoord a, HexCoord b) => new(a.Q - b.Q, a.R - b.R);
        public static HexCoord operator *(HexCoord a, int scalar) => new(a.Q * scalar, a.R * scalar);
        public static HexCoord operator *(int scalar, HexCoord a) => new(a.Q * scalar, a.R * scalar);

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object? obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Q, R);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);

        public override string ToString() => $"HexCoord({Q}, {R})";
    }
}

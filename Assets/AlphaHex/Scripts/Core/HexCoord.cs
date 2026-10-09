using System;
using System.Collections.Generic;

namespace AlphaHex.Core
{
    /// <summary>Axial coordinates for a pointy-top hex grid. The third cube axis is -Q-R.</summary>
    [Serializable]
    public struct HexCoord : IEquatable<HexCoord>
    {
        public int Q;
        public int R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static int Distance(HexCoord a, HexCoord b)
        {
            long q = (long)a.Q - b.Q;
            long r = (long)a.R - b.R;
            long distance = (Math.Abs(q) + Math.Abs(r) + Math.Abs(q + r)) / 2;
            return distance > int.MaxValue ? int.MaxValue : (int)distance;
        }

        public IEnumerable<HexCoord> Neighbors()
        {
            yield return new HexCoord(Q + 1, R);
            yield return new HexCoord(Q + 1, R - 1);
            yield return new HexCoord(Q, R - 1);
            yield return new HexCoord(Q - 1, R);
            yield return new HexCoord(Q - 1, R + 1);
            yield return new HexCoord(Q, R + 1);
        }

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => unchecked((Q * 397) ^ R);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
        public override string ToString() => "(" + Q + ", " + R + ")";
    }
}

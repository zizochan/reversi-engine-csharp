using System;

namespace Zizo.Reversi
{
    // 盤面上の座標。X と Y が同じなら同じ座標とみなす（Contains() や Dictionary のキーにも使える）
    public readonly struct ReversiPosition : IEquatable<ReversiPosition>
    {
        public int X { get; }
        public int Y { get; }

        public ReversiPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(ReversiPosition other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is ReversiPosition other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(ReversiPosition left, ReversiPosition right) =>
            left.Equals(right);

        public static bool operator !=(ReversiPosition left, ReversiPosition right) =>
            !left.Equals(right);

        // デバッグ用の文字表現。例: (3, 4)
        public override string ToString() => $"({X}, {Y})";
    }
}

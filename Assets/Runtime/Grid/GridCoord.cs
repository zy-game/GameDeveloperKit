using System;
using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 通用网格坐标（用于正方形和菱形网格）
    /// </summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public readonly int X;
        public readonly int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridCoord Zero => new GridCoord(0, 0);
        public static GridCoord One => new GridCoord(1, 1);

        public static GridCoord operator +(GridCoord a, GridCoord b) => new GridCoord(a.X + b.X, a.Y + b.Y);
        public static GridCoord operator -(GridCoord a, GridCoord b) => new GridCoord(a.X - b.X, a.Y - b.Y);
        public static GridCoord operator *(GridCoord a, int scalar) => new GridCoord(a.X * scalar, a.Y * scalar);
        public static bool operator ==(GridCoord a, GridCoord b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(GridCoord a, GridCoord b) => !(a == b);

        public int ManhattanDistance(GridCoord other) => Mathf.Abs(X - other.X) + Mathf.Abs(Y - other.Y);
        public float EuclideanDistance(GridCoord other) => Mathf.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>
    /// 六边形网格坐标（Cube坐标系）
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q;
        public readonly int R;
        public int S => -Q - R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static HexCoord Zero => new HexCoord(0, 0);

        public static HexCoord operator +(HexCoord a, HexCoord b) => new HexCoord(a.Q + b.Q, a.R + b.R);
        public static HexCoord operator -(HexCoord a, HexCoord b) => new HexCoord(a.Q - b.Q, a.R - b.R);
        public static HexCoord operator *(HexCoord a, int scalar) => new HexCoord(a.Q * scalar, a.R * scalar);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Q == b.Q && a.R == b.R;
        public static bool operator !=(HexCoord a, HexCoord b) => !(a == b);

        /// <summary>
        /// 六边形距离
        /// </summary>
        public int Distance(HexCoord other)
        {
            var diff = this - other;
            return (Mathf.Abs(diff.Q) + Mathf.Abs(diff.R) + Mathf.Abs(diff.S)) / 2;
        }

        /// <summary>
        /// 六边形邻居方向（平顶六边形）
        /// </summary>
        public static readonly HexCoord[] Directions = new HexCoord[]
        {
            new HexCoord(1, 0),   // 右
            new HexCoord(1, -1),  // 右上
            new HexCoord(0, -1),  // 左上
            new HexCoord(-1, 0),  // 左
            new HexCoord(-1, 1),  // 左下
            new HexCoord(0, 1)    // 右下
        };

        public HexCoord GetNeighbor(int direction) => this + Directions[direction % 6];

        /// <summary>
        /// 转换为通用GridCoord（用于存储）
        /// </summary>
        public GridCoord ToGridCoord() => new GridCoord(Q, R);

        /// <summary>
        /// 从GridCoord转换
        /// </summary>
        public static HexCoord FromGridCoord(GridCoord coord) => new HexCoord(coord.X, coord.Y);

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Q, R);
        public override string ToString() => $"Hex({Q}, {R}, {S})";
    }
}

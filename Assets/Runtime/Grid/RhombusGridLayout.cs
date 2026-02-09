using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 菱形网格布局（等距视角）
    /// </summary>
    public class RhombusGridLayout : IGridLayout
    {
        public EGridLayoutType LayoutType => EGridLayoutType.Rhombus;
        public float CellSize { get; }
        
        /// <summary>
        /// 菱形角度（默认45度）
        /// </summary>
        public float Angle { get; }

        private readonly float _cosAngle;
        private readonly float _sinAngle;

        private static readonly GridCoord[] Directions = new GridCoord[]
        {
            new GridCoord(0, 1),   // 上
            new GridCoord(0, -1),  // 下
            new GridCoord(-1, 0),  // 左
            new GridCoord(1, 0),   // 右
            new GridCoord(-1, 1),  // 左上
            new GridCoord(1, 1),   // 右上
            new GridCoord(-1, -1), // 左下
            new GridCoord(1, -1)   // 右下
        };

        public RhombusGridLayout(float cellSize, float angle = 45f)
        {
            CellSize = cellSize;
            Angle = angle;
            _cosAngle = Mathf.Cos(angle * Mathf.Deg2Rad);
            _sinAngle = Mathf.Sin(angle * Mathf.Deg2Rad);
        }

        public Vector3 CoordToLocal(GridCoord coord)
        {
            // 斜交坐标系转换
            float x = (coord.X - coord.Y) * CellSize * _cosAngle;
            float z = (coord.X + coord.Y) * CellSize * _sinAngle;
            return new Vector3(x, 0, z);
        }

        public GridCoord LocalToCoord(Vector3 localPosition)
        {
            // 逆变换
            float u = localPosition.x / (CellSize * _cosAngle);
            float v = localPosition.z / (CellSize * _sinAngle);
            int x = Mathf.RoundToInt((u + v) * 0.5f);
            int y = Mathf.RoundToInt((v - u) * 0.5f);
            return new GridCoord(x, y);
        }

        public IEnumerable<GridCoord> GetNeighbors(GridCoord coord)
        {
            foreach (var dir in Directions)
            {
                yield return coord + dir;
            }
        }

        public IEnumerable<GridCoord> GetCoordsInRange(GridCoord center, int range)
        {
            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    yield return new GridCoord(center.X + x, center.Y + y);
                }
            }
        }

        public IEnumerable<GridCoord> GetCoordsOnLine(GridCoord start, GridCoord end)
        {
            int dx = Mathf.Abs(end.X - start.X);
            int dy = Mathf.Abs(end.Y - start.Y);
            int sx = start.X < end.X ? 1 : -1;
            int sy = start.Y < end.Y ? 1 : -1;
            int err = dx - dy;

            int x = start.X, y = start.Y;
            while (true)
            {
                yield return new GridCoord(x, y);
                if (x == end.X && y == end.Y) break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
            }
        }

        public int GetDistance(GridCoord a, GridCoord b)
        {
            return Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        }

        public Vector3[] GetCellVertices(GridCoord coord)
        {
            var center = CoordToLocal(coord);
            float halfW = CellSize * _cosAngle;
            float halfH = CellSize * _sinAngle;
            
            return new Vector3[]
            {
                center + new Vector3(-halfW, 0, 0),
                center + new Vector3(0, 0, halfH),
                center + new Vector3(halfW, 0, 0),
                center + new Vector3(0, 0, -halfH)
            };
        }
    }
}

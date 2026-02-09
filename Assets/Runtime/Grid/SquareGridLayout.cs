using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 正方形网格布局
    /// </summary>
    public class SquareGridLayout : IGridLayout
    {
        public EGridLayoutType LayoutType => EGridLayoutType.Square;
        public float CellSize { get; }
        public ESquareNeighborMode NeighborMode { get; set; }

        private static readonly GridCoord[] FourWayDirections = new GridCoord[]
        {
            new GridCoord(0, 1),   // 上
            new GridCoord(0, -1),  // 下
            new GridCoord(-1, 0),  // 左
            new GridCoord(1, 0)    // 右
        };

        private static readonly GridCoord[] EightWayDirections = new GridCoord[]
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

        public SquareGridLayout(float cellSize, ESquareNeighborMode neighborMode = ESquareNeighborMode.FourWay)
        {
            CellSize = cellSize;
            NeighborMode = neighborMode;
        }

        public Vector3 CoordToLocal(GridCoord coord)
        {
            return new Vector3(coord.X * CellSize, 0, coord.Y * CellSize);
        }

        public GridCoord LocalToCoord(Vector3 localPosition)
        {
            return new GridCoord(
                Mathf.FloorToInt(localPosition.x / CellSize + 0.5f),
                Mathf.FloorToInt(localPosition.z / CellSize + 0.5f)
            );
        }

        public IEnumerable<GridCoord> GetNeighbors(GridCoord coord)
        {
            var directions = NeighborMode == ESquareNeighborMode.FourWay ? FourWayDirections : EightWayDirections;
            foreach (var dir in directions)
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
                    var coord = new GridCoord(center.X + x, center.Y + y);
                    if (NeighborMode == ESquareNeighborMode.FourWay)
                    {
                        if (coord.ManhattanDistance(center) <= range)
                            yield return coord;
                    }
                    else
                    {
                        yield return coord;
                    }
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
            return NeighborMode == ESquareNeighborMode.FourWay
                ? a.ManhattanDistance(b)
                : Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        }

        public Vector3[] GetCellVertices(GridCoord coord)
        {
            float half = CellSize * 0.5f;
            var center = CoordToLocal(coord);
            return new Vector3[]
            {
                center + new Vector3(-half, 0, -half),
                center + new Vector3(-half, 0, half),
                center + new Vector3(half, 0, half),
                center + new Vector3(half, 0, -half)
            };
        }
    }
}

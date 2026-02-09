using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 六边形网格布局
    /// </summary>
    public class HexGridLayout : IGridLayout
    {
        public EGridLayoutType LayoutType => EGridLayoutType.Hexagon;
        public float CellSize { get; }
        public EHexOrientation Orientation { get; }

        private readonly float _width;
        private readonly float _height;
        private readonly float _horizontalSpacing;
        private readonly float _verticalSpacing;

        public HexGridLayout(float cellSize, EHexOrientation orientation = EHexOrientation.PointyTop)
        {
            CellSize = cellSize;
            Orientation = orientation;

            if (orientation == EHexOrientation.PointyTop)
            {
                _width = Mathf.Sqrt(3f) * cellSize;
                _height = 2f * cellSize;
                _horizontalSpacing = _width;
                _verticalSpacing = _height * 0.75f;
            }
            else
            {
                _width = 2f * cellSize;
                _height = Mathf.Sqrt(3f) * cellSize;
                _horizontalSpacing = _width * 0.75f;
                _verticalSpacing = _height;
            }
        }

        public Vector3 CoordToLocal(GridCoord coord)
        {
            var hex = HexCoord.FromGridCoord(coord);
            float x, z;

            if (Orientation == EHexOrientation.PointyTop)
            {
                x = _horizontalSpacing * (hex.Q + hex.R * 0.5f);
                z = _verticalSpacing * hex.R;
            }
            else
            {
                x = _horizontalSpacing * hex.Q;
                z = _verticalSpacing * (hex.R + hex.Q * 0.5f);
            }

            return new Vector3(x, 0, z);
        }

        public GridCoord LocalToCoord(Vector3 localPosition)
        {
            float q, r;

            if (Orientation == EHexOrientation.PointyTop)
            {
                q = (Mathf.Sqrt(3f) / 3f * localPosition.x - 1f / 3f * localPosition.z) / CellSize;
                r = (2f / 3f * localPosition.z) / CellSize;
            }
            else
            {
                q = (2f / 3f * localPosition.x) / CellSize;
                r = (-1f / 3f * localPosition.x + Mathf.Sqrt(3f) / 3f * localPosition.z) / CellSize;
            }

            return HexRound(q, r).ToGridCoord();
        }

        private HexCoord HexRound(float q, float r)
        {
            float s = -q - r;
            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);

            float qDiff = Mathf.Abs(rq - q);
            float rDiff = Mathf.Abs(rr - r);
            float sDiff = Mathf.Abs(rs - s);

            if (qDiff > rDiff && qDiff > sDiff)
                rq = -rr - rs;
            else if (rDiff > sDiff)
                rr = -rq - rs;

            return new HexCoord(rq, rr);
        }

        public IEnumerable<GridCoord> GetNeighbors(GridCoord coord)
        {
            var hex = HexCoord.FromGridCoord(coord);
            for (int i = 0; i < 6; i++)
            {
                yield return hex.GetNeighbor(i).ToGridCoord();
            }
        }

        public IEnumerable<GridCoord> GetCoordsInRange(GridCoord center, int range)
        {
            var hexCenter = HexCoord.FromGridCoord(center);
            for (int q = -range; q <= range; q++)
            {
                int r1 = Mathf.Max(-range, -q - range);
                int r2 = Mathf.Min(range, -q + range);
                for (int r = r1; r <= r2; r++)
                {
                    yield return new HexCoord(hexCenter.Q + q, hexCenter.R + r).ToGridCoord();
                }
            }
        }

        public IEnumerable<GridCoord> GetCoordsOnLine(GridCoord start, GridCoord end)
        {
            var hexStart = HexCoord.FromGridCoord(start);
            var hexEnd = HexCoord.FromGridCoord(end);
            int distance = hexStart.Distance(hexEnd);

            if (distance == 0)
            {
                yield return start;
                yield break;
            }

            for (int i = 0; i <= distance; i++)
            {
                float t = (float)i / distance;
                float q = Mathf.Lerp(hexStart.Q, hexEnd.Q, t);
                float r = Mathf.Lerp(hexStart.R, hexEnd.R, t);
                yield return HexRound(q, r).ToGridCoord();
            }
        }

        public int GetDistance(GridCoord a, GridCoord b)
        {
            return HexCoord.FromGridCoord(a).Distance(HexCoord.FromGridCoord(b));
        }

        public Vector3[] GetCellVertices(GridCoord coord)
        {
            var center = CoordToLocal(coord);
            var vertices = new Vector3[6];

            for (int i = 0; i < 6; i++)
            {
                float angle = Orientation == EHexOrientation.PointyTop
                    ? 60f * i - 30f
                    : 60f * i;
                float rad = angle * Mathf.Deg2Rad;
                vertices[i] = center + new Vector3(CellSize * Mathf.Cos(rad), 0, CellSize * Mathf.Sin(rad));
            }

            return vertices;
        }
    }
}

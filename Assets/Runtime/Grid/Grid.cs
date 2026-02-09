using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格实例，组合布局、表面和数据存储
    /// </summary>
    public class Grid<T> where T : struct
    {
        /// <summary>
        /// 网格名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 网格布局
        /// </summary>
        public IGridLayout Layout { get; }

        /// <summary>
        /// 网格表面
        /// </summary>
        public IGridSurface Surface { get; }

        /// <summary>
        /// 网格数据
        /// </summary>
        public IGridMap<T> Data { get; }

        public Grid(string name, IGridLayout layout, IGridSurface surface, IGridMap<T> data)
        {
            Name = name;
            Layout = layout;
            Surface = surface;
            Data = data;
        }

        #region 坐标转换

        /// <summary>
        /// 网格坐标转世界坐标
        /// </summary>
        public Vector3 CoordToWorld(GridCoord coord)
        {
            var local = Layout.CoordToLocal(coord);
            return Surface.LocalToWorld(local);
        }

        /// <summary>
        /// 世界坐标转网格坐标
        /// </summary>
        public GridCoord WorldToCoord(Vector3 worldPosition)
        {
            var local = Surface.WorldToLocal(worldPosition);
            return Layout.LocalToCoord(local);
        }

        /// <summary>
        /// 吸附到网格中心
        /// </summary>
        public Vector3 SnapToGridCenter(Vector3 worldPosition)
        {
            var coord = WorldToCoord(worldPosition);
            return CoordToWorld(coord);
        }

        /// <summary>
        /// 获取格子在世界空间的旋转
        /// </summary>
        public Quaternion GetCellRotation(GridCoord coord)
        {
            var local = Layout.CoordToLocal(coord);
            return Surface.GetRotation(local);
        }

        #endregion

        #region 数据操作

        /// <summary>
        /// 获取格子数据
        /// </summary>
        public T GetCell(GridCoord coord) => Data.Get(coord);

        /// <summary>
        /// 设置格子数据
        /// </summary>
        public void SetCell(GridCoord coord, T value) => Data.Set(coord, value);

        /// <summary>
        /// 是否有数据
        /// </summary>
        public bool HasCell(GridCoord coord) => Data.Contains(coord);

        /// <summary>
        /// 移除格子数据
        /// </summary>
        public bool RemoveCell(GridCoord coord) => Data.Remove(coord);

        #endregion

        #region 查询操作

        /// <summary>
        /// 获取邻居
        /// </summary>
        public IEnumerable<GridCoord> GetNeighbors(GridCoord coord) => Layout.GetNeighbors(coord);

        /// <summary>
        /// 获取范围内的坐标
        /// </summary>
        public IEnumerable<GridCoord> GetCoordsInRange(GridCoord center, int range) 
            => Layout.GetCoordsInRange(center, range);

        /// <summary>
        /// 获取射线上的坐标
        /// </summary>
        public IEnumerable<GridCoord> GetCoordsOnLine(GridCoord start, GridCoord end) 
            => Layout.GetCoordsOnLine(start, end);

        /// <summary>
        /// 计算距离
        /// </summary>
        public int GetDistance(GridCoord a, GridCoord b) => Layout.GetDistance(a, b);

        /// <summary>
        /// 获取格子顶点（世界坐标）
        /// </summary>
        public Vector3[] GetCellWorldVertices(GridCoord coord)
        {
            var localVertices = Layout.GetCellVertices(coord);
            var worldVertices = new Vector3[localVertices.Length];
            for (int i = 0; i < localVertices.Length; i++)
            {
                worldVertices[i] = Surface.LocalToWorld(localVertices[i]);
            }
            return worldVertices;
        }

        #endregion

        #region 区域查询

        /// <summary>
        /// 获取矩形区域内的坐标
        /// </summary>
        public IEnumerable<GridCoord> GetCoordsInArea(GridCoord min, GridCoord max)
        {
            for (int x = min.X; x <= max.X; x++)
            {
                for (int y = min.Y; y <= max.Y; y++)
                {
                    yield return new GridCoord(x, y);
                }
            }
        }

        /// <summary>
        /// 获取世界空间包围盒内的坐标
        /// </summary>
        public IEnumerable<GridCoord> GetCoordsInWorldBounds(Bounds bounds)
        {
            var minCoord = WorldToCoord(bounds.min);
            var maxCoord = WorldToCoord(bounds.max);
            return GetCoordsInArea(minCoord, maxCoord);
        }

        #endregion
    }
}

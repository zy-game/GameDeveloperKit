using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格布局接口
    /// </summary>
    public interface IGridLayout
    {
        /// <summary>
        /// 布局类型
        /// </summary>
        EGridLayoutType LayoutType { get; }

        /// <summary>
        /// 格子大小
        /// </summary>
        float CellSize { get; }

        /// <summary>
        /// 网格坐标转本地坐标
        /// </summary>
        Vector3 CoordToLocal(GridCoord coord);

        /// <summary>
        /// 本地坐标转网格坐标
        /// </summary>
        GridCoord LocalToCoord(Vector3 localPosition);

        /// <summary>
        /// 获取邻居坐标
        /// </summary>
        IEnumerable<GridCoord> GetNeighbors(GridCoord coord);

        /// <summary>
        /// 获取范围内的坐标
        /// </summary>
        IEnumerable<GridCoord> GetCoordsInRange(GridCoord center, int range);

        /// <summary>
        /// 获取两点之间的坐标（射线）
        /// </summary>
        IEnumerable<GridCoord> GetCoordsOnLine(GridCoord start, GridCoord end);

        /// <summary>
        /// 计算两个坐标之间的距离
        /// </summary>
        int GetDistance(GridCoord a, GridCoord b);

        /// <summary>
        /// 获取格子顶点（用于绘制）
        /// </summary>
        Vector3[] GetCellVertices(GridCoord coord);
    }
}

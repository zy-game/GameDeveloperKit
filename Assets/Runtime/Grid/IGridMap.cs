using System.Collections.Generic;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格数据容器接口
    /// </summary>
    public interface IGridMap<T> where T : struct
    {
        /// <summary>
        /// 获取格子数据
        /// </summary>
        T Get(GridCoord coord);

        /// <summary>
        /// 设置格子数据
        /// </summary>
        void Set(GridCoord coord, T value);

        /// <summary>
        /// 是否包含指定坐标
        /// </summary>
        bool Contains(GridCoord coord);

        /// <summary>
        /// 移除格子数据
        /// </summary>
        bool Remove(GridCoord coord);

        /// <summary>
        /// 清空所有数据
        /// </summary>
        void Clear();

        /// <summary>
        /// 获取所有有数据的坐标
        /// </summary>
        IEnumerable<GridCoord> GetAllCoords();

        /// <summary>
        /// 获取数据数量
        /// </summary>
        int Count { get; }
    }
}

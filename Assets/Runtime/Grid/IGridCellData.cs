namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格格子数据接口，业务层实现此接口来存储自定义格子数据
    /// </summary>
    public interface IGridCellData
    {
        /// <summary>
        /// 格子是否被占用
        /// </summary>
        bool IsOccupied { get; }

        /// <summary>
        /// 格子是否可通行
        /// </summary>
        bool IsWalkable { get; }
    }

    /// <summary>
    /// 空格子数据（默认实现）
    /// </summary>
    public struct EmptyCellData : IGridCellData
    {
        public bool IsOccupied => false;
        public bool IsWalkable => true;
    }
}

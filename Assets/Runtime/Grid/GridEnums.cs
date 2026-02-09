namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格布局类型
    /// </summary>
    public enum EGridLayoutType
    {
        Square,
        Hexagon,
        Rhombus
    }

    /// <summary>
    /// 正方形网格邻居模式
    /// </summary>
    public enum ESquareNeighborMode
    {
        /// <summary>
        /// 4方向（上下左右）
        /// </summary>
        FourWay,
        /// <summary>
        /// 8方向（含对角线）
        /// </summary>
        EightWay
    }

    /// <summary>
    /// 六边形网格朝向
    /// </summary>
    public enum EHexOrientation
    {
        /// <summary>
        /// 平顶六边形
        /// </summary>
        FlatTop,
        /// <summary>
        /// 尖顶六边形
        /// </summary>
        PointyTop
    }

    /// <summary>
    /// 格子可视化状态
    /// </summary>
    public enum ECellVisualState
    {
        None,
        Normal,
        Hovered,
        Selected,
        Valid,
        Invalid,
        Preview
    }
}

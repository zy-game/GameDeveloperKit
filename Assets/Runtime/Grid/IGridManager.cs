namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格管理器接口
    /// </summary>
    public interface IGridManager:IModule
    {
        /// <summary>
        /// 创建正方形网格
        /// </summary>
        Grid<T> CreateSquareGrid<T>(string name, float cellSize, ESquareNeighborMode neighborMode = ESquareNeighborMode.FourWay) where T : struct;

        /// <summary>
        /// 创建六边形网格
        /// </summary>
        Grid<T> CreateHexGrid<T>(string name, float cellSize, EHexOrientation orientation = EHexOrientation.PointyTop) where T : struct;

        /// <summary>
        /// 创建菱形网格
        /// </summary>
        Grid<T> CreateRhombusGrid<T>(string name, float cellSize, float angle = 45f) where T : struct;

        /// <summary>
        /// 创建自定义网格
        /// </summary>
        Grid<T> CreateGrid<T>(string name, IGridLayout layout, IGridSurface surface, IGridMap<T> data) where T : struct;

        /// <summary>
        /// 获取网格可视化器
        /// </summary>
        GridVisualizer GetVisualizer(string gridName);

        /// <summary>
        /// 销毁网格
        /// </summary>
        void DestroyGrid(string name);

        /// <summary>
        /// 是否存在网格
        /// </summary>
        bool HasGrid(string name);
    }
}

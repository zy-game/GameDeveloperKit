namespace GameDeveloperKit.World
{
    /// <summary>
    /// 系统生命周期接口
    /// 在系统被添加到GameWorld时调用OnStartup，在系统被移除或World销毁时调用OnShutdown
    /// </summary>
    public interface INormalSystem : ISystem
    {
        /// <summary>
        /// 系统启动时调用，用于初始化系统
        /// </summary>
        /// <param name="world">游戏世界</param>
        void OnStartup(GameWorld world);

        /// <summary>
        /// 系统关闭时调用，用于清理系统资源
        /// </summary>
        /// <param name="world">游戏世界</param>
        void OnShutdown(GameWorld world);
    }
}

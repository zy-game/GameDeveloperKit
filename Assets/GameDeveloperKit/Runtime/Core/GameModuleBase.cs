namespace GameDeveloperKit
{
    /// <summary>
    /// 模块基类
    /// </summary>
    public abstract class GameModuleBase : IGameModule
    {
        /// <summary>
        /// 模块启动
        /// </summary>
        public abstract void Startup();

        /// <summary>
        /// 模块关闭
        /// </summary>
        public abstract void Shutdown();
    }
}

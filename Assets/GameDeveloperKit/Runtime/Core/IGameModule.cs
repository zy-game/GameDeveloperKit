namespace GameDeveloperKit
{
    /// <summary>
    /// 游戏模块接口，定义模块启动和关闭生命周期。
    /// </summary>
    public interface IGameModule : IReference
    {
        /// <summary>
        /// 启动模块。
        /// </summary>
        void Startup();

        /// <summary>
        /// 关闭模块。
        /// </summary>
        void Shutdown();

        /// <summary>
        /// 释放模块，默认调用关闭流程。
        /// </summary>
        void IReference.Release()
        {
            Shutdown();
        }
    }

}

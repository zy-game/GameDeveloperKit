namespace GameDeveloperKit
{
    /// <summary>
    /// 游戏模块接口，定义模块启动和关闭生命周期。
    /// </summary>
    public interface IGameModule : IReference
    {
        void Startup();

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

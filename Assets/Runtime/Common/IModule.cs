namespace GameDeveloperKit
{
    /// <summary>
    /// 游戏框架模块接口。
    /// </summary>
    public interface IModule : IReference
    {
        /// <summary>
        /// 模块初始化。
        /// </summary>
        void OnStartup();

        /// <summary>
        /// 模块轮询。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        void OnUpdate(float elapseSeconds);
    }
}
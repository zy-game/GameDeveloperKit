namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源模块显式初始化状态。
    /// </summary>
    public enum ResourceInitializeState
    {
        /// <summary>
        /// 尚未初始化。
        /// </summary>
        NotInitialized = 0,

        /// <summary>
        /// 正在初始化。
        /// </summary>
        Initializing = 1,

        /// <summary>
        /// 已完成初始化。
        /// </summary>
        Initialized = 2,

        /// <summary>
        /// 初始化失败。
        /// </summary>
        Failed = 3
    }
}

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源包状态枚举，表示资源包生命周期中的当前阶段。
    /// </summary>
    public enum ResourcePackageState
    {
        /// <summary>
        /// 资源包尚未初始化。
        /// </summary>
        Uninitialized = 0,

        /// <summary>
        /// 资源包正在初始化。
        /// </summary>
        Initializing = 1,

        /// <summary>
        /// 资源包已初始化完成。
        /// </summary>
        Initialized = 2,

        /// <summary>
        /// 资源包正在更新。
        /// </summary>
        Updating = 3,

        /// <summary>
        /// 资源包更新完成。
        /// </summary>
        Updated = 4,

        /// <summary>
        /// 资源包正在准备可用资源。
        /// </summary>
        Preparing = 5,

        /// <summary>
        /// 资源包已就绪可用。
        /// </summary>
        Ready = 6,

        /// <summary>
        /// 资源包处理失败。
        /// </summary>
        Failed = 7
    }
}

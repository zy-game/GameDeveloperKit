namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源更新状态枚举，表示资源更新流程中的执行阶段。
    /// </summary>
    public enum ResourceUpdateState
    {
        /// <summary>
        /// 空闲状态。
        /// </summary>
        Idle,

        /// <summary>
        /// 正在检查资源更新。
        /// </summary>
        Checking,

        /// <summary>
        /// 正在下载资源。
        /// </summary>
        Downloading,

        /// <summary>
        /// 正在校验资源。
        /// </summary>
        Verifying,

        /// <summary>
        /// 正在应用更新结果。
        /// </summary>
        Applying,

        /// <summary>
        /// 正在执行回滚。
        /// </summary>
        RollingBack,

        /// <summary>
        /// 更新流程已完成。
        /// </summary>
        Completed,

        /// <summary>
        /// 更新流程执行失败。
        /// </summary>
        Failed
    }
}

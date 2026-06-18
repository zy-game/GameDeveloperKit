namespace GameDeveloperKit.Operation
{
    /// <summary>
    /// 操作状态，用于描述异步操作句柄的生命周期阶段。
    /// </summary>
    public enum OperationStatus : byte
    {
        /// <summary>
        /// 未初始化状态。
        /// </summary>
        None = 0,

        /// <summary>
        /// 等待执行状态。
        /// </summary>
        Pending = 1,

        /// <summary>
        /// 正在执行状态。
        /// </summary>
        Running = 2,

        /// <summary>
        /// 已暂停状态。
        /// </summary>
        Paused = 3,

        /// <summary>
        /// 已取消状态。
        /// </summary>
        Cancelled = 4,

        /// <summary>
        /// 执行成功状态。
        /// </summary>
        Succeeded = 5,

        /// <summary>
        /// 执行失败状态。
        /// </summary>
        Failed = 6,
    }
}

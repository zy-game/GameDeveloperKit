namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示框架操作执行过程中的阶段。
    /// </summary>
    public enum FrameworkOperationStage
    {
        /// <summary>
        /// 未开始任何阶段。
        /// </summary>
        None = 0,

        /// <summary>
        /// 正在校验输入或运行前置条件。
        /// </summary>
        Validating = 1,

        /// <summary>
        /// 正在准备执行所需资源或环境。
        /// </summary>
        Preparing = 2,

        /// <summary>
        /// 正在执行核心操作。
        /// </summary>
        Executing = 3,

        /// <summary>
        /// 正在下载相关内容。
        /// </summary>
        Downloading = 4,

        /// <summary>
        /// 正在验证执行结果或下载内容。
        /// </summary>
        Verifying = 5,

        /// <summary>
        /// 正在应用执行结果。
        /// </summary>
        Applying = 6,

        /// <summary>
        /// 操作已成功完成。
        /// </summary>
        Completed = 7,

        /// <summary>
        /// 操作执行失败。
        /// </summary>
        Failed = 8,

        /// <summary>
        /// 操作已被取消。
        /// </summary>
        Cancelled = 9
    }
}

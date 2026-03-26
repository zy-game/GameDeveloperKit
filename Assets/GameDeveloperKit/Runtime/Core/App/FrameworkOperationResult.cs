namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示框架操作的通用结果。
    /// </summary>
    public class FrameworkOperationResult
    {
        /// <summary>
        /// 获取或设置操作是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置当前操作阶段。
        /// </summary>
        public FrameworkOperationStage Stage { get; set; } = FrameworkOperationStage.None;

        /// <summary>
        /// 获取或设置错误消息。
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 获取或设置详细错误信息。
        /// </summary>
        public FrameworkError Error { get; set; }

        /// <summary>
        /// 获取或设置失败类型标识。
        /// </summary>
        public string FailureKind { get; set; }
    }
}

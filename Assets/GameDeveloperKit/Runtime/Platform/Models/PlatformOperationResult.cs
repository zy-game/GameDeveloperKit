using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 平台操作结果，封装平台相关操作的结果和错误信息。
    /// </summary>
    [Serializable]
    public sealed class PlatformOperationResult
    {
        /// <summary>
        /// 获取或设置操作是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置当前操作阶段。
        /// </summary>
        public string Stage { get; set; } = "None";

        /// <summary>
        /// 获取或设置错误消息。
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 获取或设置详细错误信息。
        /// </summary>
        public GameFrameworkException Error { get; set; }

        /// <summary>
        /// 获取或设置失败类型标识。
        /// </summary>
        public string FailureKind { get; set; }

        /// <summary>
        /// 平台特定的错误代码。
        /// </summary>
        public string ErrorCode;
    }
}




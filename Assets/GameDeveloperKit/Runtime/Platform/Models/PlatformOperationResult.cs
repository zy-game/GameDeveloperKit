using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 平台操作结果，封装平台相关操作的结果和错误信息。
    /// </summary>
    [Serializable]
    public sealed class PlatformOperationResult : FrameworkOperationResult
    {
        /// <summary>
        /// 平台特定的错误代码。
        /// </summary>
        public string ErrorCode;
    }
}

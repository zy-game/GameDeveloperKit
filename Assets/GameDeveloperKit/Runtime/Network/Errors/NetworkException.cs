using System;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络模块异常。
    /// </summary>
    public sealed class NetworkException : GameException
    {
        /// <summary>
        /// 初始化 Network Exception。
        /// </summary>
        public NetworkException(string message, NetworkFailureKind failureKind) : base(message)
        {
            FailureKind = failureKind;
            StatusCode = 0L;
        }

        /// <summary>
        /// 初始化 Network Exception。
        /// </summary>
        /// <param name="failureKind">failure Kind 参数。</param>
        /// <param name="innerException">inner Exception 参数。</param>
        public NetworkException(string message, NetworkFailureKind failureKind, Exception innerException) : base(message, innerException)
        {
            FailureKind = failureKind;
            StatusCode = 0L;
        }

        /// <summary>
        /// 初始化 Network Exception。
        /// </summary>
        /// <param name="failureKind">failure Kind 参数。</param>
        public NetworkException(string message, NetworkFailureKind failureKind, long statusCode) : base(message)
        {
            FailureKind = failureKind;
            StatusCode = statusCode;
        }

        public NetworkFailureKind FailureKind { get; }

        public long StatusCode { get; }
    }
}

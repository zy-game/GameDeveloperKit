using System;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络模块异常。
    /// </summary>
    public sealed class NetworkException : GameException
    {
        public NetworkException(string message, NetworkFailureKind failureKind) : base(message)
        {
            FailureKind = failureKind;
            StatusCode = 0L;
        }

        public NetworkException(string message, NetworkFailureKind failureKind, Exception innerException) : base(message, innerException)
        {
            FailureKind = failureKind;
            StatusCode = 0L;
        }

        public NetworkException(string message, NetworkFailureKind failureKind, long statusCode) : base(message)
        {
            FailureKind = failureKind;
            StatusCode = statusCode;
        }

        public NetworkFailureKind FailureKind { get; }

        public long StatusCode { get; }
    }
}

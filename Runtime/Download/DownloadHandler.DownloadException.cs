namespace GameDeveloperKit.Download
{
    public partial class DownloadHandler
    {
        /// <summary>
        /// 下载异常类，继承自GameException，包含一个表示下载失败类型的属性，用于在下载过程中捕获和区分不同类型的错误情况。
        /// </summary>
        private sealed class DownloadException : GameException
        {
            /// <summary>
            /// 下载失败类型。
            /// </summary>
            public DownloadFailureKind FailureKind { get; }

            /// <summary>
            /// 初始化下载异常。
            /// </summary>
            /// <param name="message">异常消息。</param>
            /// <param name="failureKind">下载失败类型。</param>
            public DownloadException(string message, DownloadFailureKind failureKind) : base(message)
            {
                FailureKind = failureKind;
            }
        }
    }
}

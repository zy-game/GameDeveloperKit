namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示单个下载请求的执行结果。
    /// </summary>
    public sealed class DownloadResult : FrameworkOperationResult
    {
        /// <summary>
        /// 获取或设置下载状态。
        /// </summary>
        public DownloadStatus Status { get; set; }

        /// <summary>
        /// 获取或设置最终保存路径。
        /// </summary>
        public string SavePath { get; set; }

        /// <summary>
        /// 获取或设置下载过程中使用的工作文件保存路径。
        /// </summary>
        public string WorkingSavePath { get; set; }

        /// <summary>
        /// 获取或设置已下载的字节数。
        /// </summary>
        public long DownloadedBytes { get; set; }

        /// <summary>
        /// 获取或设置资源总字节数。
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 获取或设置下载结果是否通过校验。
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// 获取或设置本次下载是否从断点续传恢复。
        /// </summary>
        public bool IsResumed { get; set; }

        /// <summary>
        /// 获取或设置实际尝试下载的次数。
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// 获取或设置最终成功或最后一次使用的源地址。
        /// </summary>
        public string SourceUrl { get; set; }

        /// <summary>
        /// 获取或设置本次下载尝试过的地址列表。
        /// </summary>
        public string[] AttemptedUrls { get; set; }

        /// <summary>
        /// 获取或设置发生源地址回退的次数。
        /// </summary>
        public int FallbackCount { get; set; }

        /// <summary>
        /// 获取或设置是否使用了临时文件。
        /// </summary>
        public bool UsedTemporaryFile { get; set; }

        /// <summary>
        /// 获取或设置最终提交下载文件是否成功。
        /// </summary>
        public bool CommitSucceeded { get; set; }

        /// <summary>
        /// 获取或设置是否执行了清理操作。
        /// </summary>
        public bool CleanupPerformed { get; set; }
    }
}

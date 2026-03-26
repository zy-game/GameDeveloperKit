namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 定义下载过程中的附加策略配置。
    /// </summary>
    public sealed class DownloadPolicy
    {
        /// <summary>
        /// 获取或设置是否使用临时文件完成下载。
        /// </summary>
        public bool UseTemporaryFile { get; set; } = true;

        /// <summary>
        /// 获取或设置临时文件目录。
        /// </summary>
        public string TemporaryDirectory { get; set; }

        /// <summary>
        /// 获取或设置下载失败时是否清理临时文件。
        /// </summary>
        public bool CleanupTemporaryFileOnFailure { get; set; } = true;

        /// <summary>
        /// 获取或设置下载取消时是否清理临时文件。
        /// </summary>
        public bool CleanupTemporaryFileOnCancel { get; set; }

        /// <summary>
        /// 获取或设置重试次数覆盖值，负数表示使用请求默认值。
        /// </summary>
        public int RetryCountOverride { get; set; } = -1;

        /// <summary>
        /// 获取或设置两次重试之间的延迟毫秒数。
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 250;

        /// <summary>
        /// 获取或设置超时时间覆盖值（秒），负数表示使用请求默认值。
        /// </summary>
        public int TimeoutSecondsOverride { get; set; } = -1;

        /// <summary>
        /// 获取或设置下载任务优先级。
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// 获取或设置最大下载限速（字节每秒）。
        /// </summary>
        public long MaxBytesPerSecond { get; set; }
    }
}

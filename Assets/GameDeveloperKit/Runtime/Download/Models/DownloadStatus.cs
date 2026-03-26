namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示下载任务的状态。
    /// </summary>
    public enum DownloadStatus
    {
        /// <summary>
        /// 未开始。
        /// </summary>
        None,

        /// <summary>
        /// 等待执行。
        /// </summary>
        Pending,

        /// <summary>
        /// 下载中。
        /// </summary>
        Downloading,

        /// <summary>
        /// 下载成功。
        /// </summary>
        Succeeded,

        /// <summary>
        /// 下载失败。
        /// </summary>
        Failed,

        /// <summary>
        /// 下载已取消。
        /// </summary>
        Cancelled,

        /// <summary>
        /// 下载已暂停。
        /// </summary>
        Paused
    }
}

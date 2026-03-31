using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示批量下载任务的执行结果。
    /// </summary>
    public sealed class DownloadBatchResult
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
        /// 获取或设置批量下载的整体状态。
        /// </summary>
        public DownloadStatus Status { get; set; }

        /// <summary>
        /// 获取或设置各个下载项的结果集合。
        /// </summary>
        public IReadOnlyList<DownloadResult> Results { get; set; }

        /// <summary>
        /// 获取或设置批量下载已完成的总字节数。
        /// </summary>
        public long DownloadedBytes { get; set; }

        /// <summary>
        /// 获取或设置批量下载目标的总字节数。
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 获取或设置下载成功的任务数量。
        /// </summary>
        public int SucceededCount { get; set; }

        /// <summary>
        /// 获取或设置所有任务累计尝试下载的总次数。
        /// </summary>
        public int TotalAttemptCount { get; set; }

        /// <summary>
        /// 获取或设置执行过清理操作的任务数量。
        /// </summary>
        public int CleanupPerformedCount { get; set; }
    }
}




using System;

namespace GameDeveloperKit.Runtime
{
    [Serializable]
    /// <summary>
    /// 表示下载任务断点续传时持久化的状态数据。
    /// </summary>
    internal sealed class DownloadResumeState
    {
        /// <summary>
        /// 下载目标最终保存路径。
        /// </summary>
        public string SavePath;

        /// <summary>
        /// 下载过程中使用的工作文件路径。
        /// </summary>
        public string WorkingSavePath;

        /// <summary>
        /// 当前下载任务可用的地址列表。
        /// </summary>
        public string[] Urls;

        /// <summary>
        /// 下载超时时间（秒）。
        /// </summary>
        public int TimeoutSeconds;

        /// <summary>
        /// 下载失败后的重试次数。
        /// </summary>
        public int RetryCount;

        /// <summary>
        /// 分片下载数量。
        /// </summary>
        public int ChunkCount;

        /// <summary>
        /// 是否覆盖已存在文件。
        /// </summary>
        public bool Overwrite;

        /// <summary>
        /// 期望的文件哈希值。
        /// </summary>
        public string ExpectedHash;

        /// <summary>
        /// 期望的文件大小（字节）。
        /// </summary>
        public long ExpectedSizeBytes;

        /// <summary>
        /// 是否使用临时文件下载。
        /// </summary>
        public bool UseTemporaryFile;

        /// <summary>
        /// 临时文件目录。
        /// </summary>
        public string TemporaryDirectory;

        /// <summary>
        /// 下载失败时是否清理临时文件。
        /// </summary>
        public bool CleanupTemporaryFileOnFailure;

        /// <summary>
        /// 下载取消时是否清理临时文件。
        /// </summary>
        public bool CleanupTemporaryFileOnCancel;

        /// <summary>
        /// 重试次数覆盖值。
        /// </summary>
        public int RetryCountOverride;

        /// <summary>
        /// 重试间隔毫秒数。
        /// </summary>
        public int RetryDelayMilliseconds;

        /// <summary>
        /// 超时时间覆盖值（秒）。
        /// </summary>
        public int TimeoutSecondsOverride;

        /// <summary>
        /// 下载优先级。
        /// </summary>
        public int Priority;

        /// <summary>
        /// 最大下载限速（字节每秒）。
        /// </summary>
        public long MaxBytesPerSecond;

        /// <summary>
        /// 已下载的字节数。
        /// </summary>
        public long DownloadedBytes;

        /// <summary>
        /// 当前已尝试的次数。
        /// </summary>
        public int AttemptCount;

        /// <summary>
        /// 最后更新时间（UTC）。
        /// </summary>
        public string LastUpdatedUtc;
    }
}

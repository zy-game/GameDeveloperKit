using System;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 下载结果
    /// </summary>
    public class DownloadResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 下载状态
        /// </summary>
        public DownloadStatus Status { get; set; }

        /// <summary>
        /// 下载完成的文件路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// 文件总大小（字节）
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 已接收字节数
        /// </summary>
        public long ReceivedBytes { get; set; }

        /// <summary>
        /// 下载进度 (0-1)
        /// </summary>
        public float Progress { get; set; }

        /// <summary>
        /// 当前下载速度（字节/秒）
        /// </summary>
        public long BytesPerSecond { get; set; }

        /// <summary>
        /// 平均下载速度（字节/秒）
        /// </summary>
        public long AverageBytesPerSecond { get; set; }

        /// <summary>
        /// 预计剩余时间
        /// </summary>
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }
}

using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 下载请求，包含下载所需的配置信息。
    /// </summary>
    public sealed class DownloadRequest
    {
        /// <summary>
        /// 获取或设置下载 URL 列表（支持多源备用）。
        /// </summary>
        public IReadOnlyList<string> Urls { get; set; }

        /// <summary>
        /// 获取或设置保存路径。
        /// </summary>
        public string SavePath { get; set; }

        /// <summary>
        /// 获取或设置超时时间（秒）。
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 获取或设置重试次数。
        /// </summary>
        public int RetryCount { get; set; } = 2;

        /// <summary>
        /// 获取或设置分片下载数。
        /// </summary>
        public int ChunkCount { get; set; } = 1;

        /// <summary>
        /// 获取或设置是否覆盖已存在的文件。
        /// </summary>
        public bool Overwrite { get; set; } = true;

        /// <summary>
        /// 获取或设置期望的文件 SHA256 哈希值（用于验证）。
        /// </summary>
        public string ExpectedHash { get; set; }

        /// <summary>
        /// 获取或设置期望的文件大小（字节）。
        /// </summary>
        public long ExpectedSizeBytes { get; set; }

        /// <summary>
        /// 获取或设置下载策略。
        /// </summary>
        public DownloadPolicy Policy { get; set; } = new();
    }
}

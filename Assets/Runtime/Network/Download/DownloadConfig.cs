namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 下载配置
    /// </summary>
    public class DownloadConfig
    {
        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 初始延迟（毫秒）
        /// </summary>
        public int InitialDelayMs { get; set; } = 500;

        /// <summary>
        /// 退避因子
        /// </summary>
        public float BackoffFactor { get; set; } = 2f;

        /// <summary>
        /// 超时时间（秒）
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 分块数量（用于大文件）
        /// </summary>
        public int ChunkCount { get; set; } = 8;

        /// <summary>
        /// 启用断点续传
        /// </summary>
        public bool EnableResume { get; set; } = true;

        /// <summary>
        /// 优先级（数值越大优先级越高）
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// 文件期望哈希值（用于完整性校验）
        /// </summary>
        public string ExpectedHash { get; set; }

        /// <summary>
        /// 哈希算法类型
        /// </summary>
        public HashAlgorithmType HashType { get; set; } = HashAlgorithmType.None;
    }
}

namespace GameDeveloperKit.Download
{
    /// <summary>
    /// 下载失败类型
    /// </summary>
    public enum DownloadFailureKind
    {
        /// <summary>
        /// 未知错误
        /// </summary>
        None,
        /// <summary>
        /// 网络错误
        /// </summary>
        Network,
        /// <summary>
        /// 超时
        /// </summary>
        Timeout,
        /// <summary>
        /// HTTP状态错误
        /// </summary>
        HttpStatus,
        /// <summary>
        /// 文件I/O错误
        /// </summary>
        FileIO,
        /// <summary>
        /// 无效响应
        /// </summary>
        InvalidResponse,
        /// <summary>
        /// 已取消
        /// </summary>
        Canceled
    }
}

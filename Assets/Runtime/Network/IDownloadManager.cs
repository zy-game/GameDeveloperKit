using System.Threading;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 下载管理器
    /// </summary>
    public interface IDownloadManager : IModule
    {
        /// <summary>
        /// 设置最大并发下载数
        /// </summary>
        void SetMaxConcurrentDownloads(int max);

        /// <summary>
        /// 设置默认配置
        /// </summary>
        void SetDefaultConfig(DownloadConfig config);

        /// <summary>
        /// 下载文件（异步）
        /// </summary>
        DownloadHandle DownloadAsync(string url, string version, DownloadConfig config = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 创建下载集合
        /// </summary>
        DownloadCollection CreateCollection();

        /// <summary>
        /// 获取下载句柄
        /// </summary>
        DownloadHandle GetDownload(string url, string version);

        /// <summary>
        /// 检查下载是否存在
        /// </summary>
        bool HasDownload(string url, string version);

        /// <summary>
        /// 获取活动下载数量
        /// </summary>
        int GetActiveDownloadCount();

        /// <summary>
        /// 获取队列中的下载数量
        /// </summary>
        int GetQueuedDownloadCount();

        /// <summary>
        /// 取消所有下载
        /// </summary>
        void CancelAll();

        /// <summary>
        /// 暂停所有下载
        /// </summary>
        void PauseAll();

        /// <summary>
        /// 恢复所有下载
        /// </summary>
        void ResumeAll();
    }
}
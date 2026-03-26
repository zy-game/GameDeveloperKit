using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 下载模块接口，管理下载任务队列和下载进度。
    /// </summary>
    public interface IDownloadModule : IGameFrameworkModule
    {
        /// <summary>
        /// 获取或设置最大并发下载数量。
        /// </summary>
        int MaxConcurrentTasks { get; set; }

        /// <summary>
        /// 获取当前运行中的任务数量。
        /// </summary>
        int RunningTaskCount { get; }

        /// <summary>
        /// 获取当前队列中的任务数量。
        /// </summary>
        int QueuedTaskCount { get; }

        /// <summary>
        /// 获取总体下载进度（0.0-1.0）。
        /// </summary>
        double AggregateProgress { get; }

        /// <summary>
        /// 获取总体已下载字节数。
        /// </summary>
        long AggregateDownloadedBytes { get; }

        /// <summary>
        /// 获取总体需要下载的总字节数。
        /// </summary>
        long AggregateTotalBytes { get; }

        /// <summary>
        /// 创建下载任务但不加入队列。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <returns>下载任务实例。</returns>
        IDownloadTask CreateTask(DownloadRequest request);

        /// <summary>
        /// 将下载请求加入队列并创建下载任务。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <returns>下载任务实例。</returns>
        IDownloadTask Enqueue(DownloadRequest request);

        /// <summary>
        /// 将多个下载请求批量加入队列。
        /// </summary>
        /// <param name="requests">下载请求列表。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>下载批量结果的异步任务。</returns>
        UniTask<DownloadBatchResult> EnqueueBatchAsync(IReadOnlyList<DownloadRequest> requests, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步执行下载请求。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>下载结果的异步任务。</returns>
        UniTask<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量执行下载请求。
        /// </summary>
        /// <param name="requests">下载请求列表。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>下载批量结果的异步任务。</returns>
        UniTask<DownloadBatchResult> DownloadBatchAsync(IReadOnlyList<DownloadRequest> requests, CancellationToken cancellationToken = default);

        /// <summary>
        /// 尝试从保存路径加载持久化的下载请求。
        /// </summary>
        /// <param name="savePath">保存路径。</param>
        /// <param name="request">输出的下载请求。</param>
        /// <returns>如果加载成功返回true，否则返回false。</returns>
        bool TryLoadPersistedRequest(string savePath, out DownloadRequest request);

        /// <summary>
        /// 恢复持久化的下载任务。
        /// </summary>
        /// <param name="savePath">保存路径。</param>
        /// <returns>下载任务实例。</returns>
        IDownloadTask ResumePersistedTask(string savePath);

        /// <summary>
        /// 验证文件完整性。
        /// </summary>
        /// <param name="filePath">文件路径。</param>
        /// <param name="expectedHash">期望的文件哈希值。</param>
        /// <returns>如果验证通过返回true，否则返回false。</returns>
        bool VerifyFile(string filePath, string expectedHash);

        /// <summary>
        /// 下载任务开始时触发的事件。
        /// </summary>
        event System.Action<IDownloadTask> TaskStarted;

        /// <summary>
        /// 下载任务进度改变时触发的事件。
        /// </summary>
        event System.Action<IDownloadTask> TaskProgressChanged;

        /// <summary>
        /// 下载任务完成时触发的事件。
        /// </summary>
        event System.Action<IDownloadTask> TaskCompleted;

        /// <summary>
        /// 总体下载进度改变时触发的事件。
        /// </summary>
        event System.Action<IDownloadModule> AggregateProgressChanged;
    }
}

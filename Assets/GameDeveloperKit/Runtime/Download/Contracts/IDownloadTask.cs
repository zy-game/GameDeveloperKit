using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 下载任务接口，定义单个下载任务的状态和操作。
    /// </summary>
    public interface IDownloadTask
    {
        /// <summary>
        /// 获取下载任务状态。
        /// </summary>
        DownloadStatus Status { get; }

        /// <summary>
        /// 获取下载请求。
        /// </summary>
        DownloadRequest Request { get; }

        /// <summary>
        /// 获取下载结果。
        /// </summary>
        DownloadResult Result { get; }

        /// <summary>
        /// 获取下载进度（0.0-1.0）。
        /// </summary>
        double Progress { get; }

        /// <summary>
        /// 获取已下载的字节数。
        /// </summary>
        long DownloadedBytes { get; }

        /// <summary>
        /// 获取需要下载的总字节数。
        /// </summary>
        long TotalBytes { get; }

        /// <summary>
        /// 获取下载速度（字节/秒）。
        /// </summary>
        double SpeedBytesPerSecond { get; }

        /// <summary>
        /// 获取预计剩余时间（秒）。
        /// </summary>
        double EstimatedRemainingSeconds { get; }

        /// <summary>
        /// 获取尝试次数。
        /// </summary>
        int AttemptCount { get; }

        /// <summary>
        /// 获取任务优先级。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 获取最大下载速度（字节/秒）。
        /// </summary>
        long MaxBytesPerSecond { get; }

        /// <summary>
        /// 获取当前下载URL。
        /// </summary>
        string CurrentUrl { get; }

        /// <summary>
        /// 获取回退URL使用次数。
        /// </summary>
        int FallbackCount { get; }

        /// <summary>
        /// 任务开始时触发的事件。
        /// </summary>
        event Action<IDownloadTask> Started;

        /// <summary>
        /// 任务进度改变时触发的事件。
        /// </summary>
        event Action<IDownloadTask> ProgressChanged;

        /// <summary>
        /// 任务完成时触发的事件。
        /// </summary>
        event Action<IDownloadTask> Completed;

        /// <summary>
        /// 异步启动下载任务。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>下载结果的异步任务。</returns>
        UniTask<DownloadResult> StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 暂停下载任务。
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复下载任务。
        /// </summary>
        void Resume();

        /// <summary>
        /// 取消下载任务。
        /// </summary>
        void Cancel();
    }
}

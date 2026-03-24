using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public interface IDownloadTask
    {
        DownloadStatus Status { get; }

        DownloadRequest Request { get; }

        double Progress { get; }

        long DownloadedBytes { get; }

        long TotalBytes { get; }

        event Action<IDownloadTask> ProgressChanged;

        UniTask<DownloadResult> StartAsync(CancellationToken cancellationToken = default);

        void Pause();

        void Resume();

        void Cancel();
    }
}

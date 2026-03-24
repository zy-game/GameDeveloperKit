using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public interface IDownloadModule : IGameFrameworkModule
    {
        UniTask<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default);

        UniTask<DownloadBatchResult> DownloadBatchAsync(IReadOnlyList<DownloadRequest> requests, CancellationToken cancellationToken = default);

        bool VerifyFile(string filePath, string expectedHash);
    }
}

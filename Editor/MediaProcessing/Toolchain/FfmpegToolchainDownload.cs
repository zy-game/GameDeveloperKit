using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace GameDeveloperKit.MediaEditor
{
    internal interface IFfmpegToolchainDownload
    {
        UniTask DownloadAsync(
            FfmpegToolchainPackage package,
            string destinationPath,
            IProgress<ToolchainInstallProgress> progress,
            CancellationToken cancellationToken);
    }

    internal sealed class UnityWebRequestFfmpegToolchainDownload : IFfmpegToolchainDownload
    {
        public async UniTask DownloadAsync(
            FfmpegToolchainPackage package,
            string destinationPath,
            IProgress<ToolchainInstallProgress> progress,
            CancellationToken cancellationToken)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            using (var request = UnityWebRequest.Get(package.ArchiveUrl))
            {
                request.downloadHandler = new DownloadHandlerFile(destinationPath);
                var operation = request.SendWebRequest();
                while (operation.isDone is false)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        request.Abort();
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    progress?.Report(new ToolchainInstallProgress(
                        ToolchainInstallStage.Downloading,
                        request.downloadProgress,
                        $"正在下载 FFmpeg（{FormatBytes(request.downloadedBytes)} / {FormatBytes((ulong)package.ArchiveSize)}）"));
                    await UniTask.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new InvalidOperationException($"下载 FFmpeg 失败：{request.error}");
                }

                progress?.Report(new ToolchainInstallProgress(
                    ToolchainInstallStage.Downloading,
                    1f,
                    "FFmpeg 下载完成。"));
            }
        }

        private static string FormatBytes(ulong bytes)
        {
            const double megabyte = 1024d * 1024d;
            return $"{bytes / megabyte:0.0} MB";
        }
    }
}

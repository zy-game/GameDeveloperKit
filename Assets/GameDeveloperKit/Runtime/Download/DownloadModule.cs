using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using Downloader;

namespace GameDeveloperKit.Runtime
{
    public sealed class DownloadModule : IDownloadModule
    {
        public async UniTask<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Urls == null || request.Urls.Count == 0)
            {
                throw new ArgumentException("Download request requires at least one url.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.SavePath))
            {
                throw new ArgumentException("Download request requires a save path.", nameof(request));
            }

            var directory = Path.GetDirectoryName(request.SavePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(request.SavePath) && !request.Overwrite)
            {
                return new DownloadResult
                {
                    Status = DownloadStatus.Succeeded,
                    SavePath = request.SavePath,
                    DownloadedBytes = new FileInfo(request.SavePath).Length,
                    TotalBytes = new FileInfo(request.SavePath).Length
                };
            }

            var configuration = new DownloadConfiguration
            {
                ChunkCount = Math.Max(1, request.ChunkCount),
                Timeout = Math.Max(1, request.TimeoutSeconds),
                MaxTryAgainOnFailover = Math.Max(0, request.RetryCount),
                ParallelDownload = request.ChunkCount > 1,
                RangeDownload = request.ChunkCount > 1
            };

            var downloader = DownloadBuilder.New()
                .WithConfiguration(configuration)
                .WithFileLocation(request.SavePath)
                .Build(new DownloadPackage
                {
                    Urls = ToArray(request),
                    FileName = Path.GetFileName(request.SavePath)
                });

            try
            {
                using var stream = await downloader.StartAsync(cancellationToken);
                var fileInfo = new FileInfo(request.SavePath);
                var isVerified = VerifyFile(request.SavePath, request.ExpectedHash);
                return new DownloadResult
                {
                    Status = File.Exists(request.SavePath) && isVerified ? DownloadStatus.Succeeded : DownloadStatus.Failed,
                    SavePath = request.SavePath,
                    DownloadedBytes = fileInfo.Exists ? fileInfo.Length : 0,
                    TotalBytes = fileInfo.Exists ? fileInfo.Length : 0,
                    IsVerified = isVerified
                };
            }
            catch (OperationCanceledException)
            {
                return new DownloadResult
                {
                    Status = DownloadStatus.Cancelled,
                    SavePath = request.SavePath
                };
            }
            catch (Exception exception)
            {
                return new DownloadResult
                {
                    Status = DownloadStatus.Failed,
                    SavePath = request.SavePath,
                    ErrorMessage = exception.Message
                };
            }
            finally
            {
                downloader?.Stop();
            }
        }

        public void Dispose()
        {
        }

        public bool VerifyFile(string filePath, string expectedHash)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            var actualHash = BitConverter.ToString(hash).Replace("-", string.Empty);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        public async UniTask<DownloadBatchResult> DownloadBatchAsync(IReadOnlyList<DownloadRequest> requests, CancellationToken cancellationToken = default)
        {
            if (requests == null || requests.Count == 0)
            {
                return new DownloadBatchResult
                {
                    Status = DownloadStatus.Succeeded,
                    Results = Array.Empty<DownloadResult>()
                };
            }

            var results = new List<DownloadResult>(requests.Count);
            long downloadedBytes = 0;
            long totalBytes = 0;

            for (var i = 0; i < requests.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await DownloadAsync(requests[i], cancellationToken);
                results.Add(result);
                downloadedBytes += result.DownloadedBytes;
                totalBytes += result.TotalBytes;

                if (result.Status != DownloadStatus.Succeeded)
                {
                    return new DownloadBatchResult
                    {
                        Status = result.Status,
                        Results = results,
                        ErrorMessage = result.ErrorMessage,
                        DownloadedBytes = downloadedBytes,
                        TotalBytes = totalBytes
                    };
                }
            }

            return new DownloadBatchResult
            {
                Status = DownloadStatus.Succeeded,
                Results = results,
                DownloadedBytes = downloadedBytes,
                TotalBytes = totalBytes
            };
        }

        private static string[] ToArray(DownloadRequest request)
        {
            var urls = new string[request.Urls.Count];
            for (var i = 0; i < request.Urls.Count; i++)
            {
                urls[i] = request.Urls[i];
            }

            return urls;
        }
    }
}

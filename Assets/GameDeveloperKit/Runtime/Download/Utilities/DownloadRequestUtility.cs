using System;
using System.IO;

namespace GameDeveloperKit.Runtime
{
    internal static class DownloadRequestUtility
    {
        internal static DownloadRequest Clone(DownloadRequest request)
        {
            Validate(request);
            return new DownloadRequest
            {
                Urls = ToArray(request),
                SavePath = request.SavePath,
                TimeoutSeconds = request.TimeoutSeconds,
                RetryCount = request.RetryCount,
                ChunkCount = request.ChunkCount,
                Overwrite = request.Overwrite,
                ExpectedHash = request.ExpectedHash,
                ExpectedSizeBytes = request.ExpectedSizeBytes,
                Policy = ClonePolicy(request.Policy)
            };
        }

        internal static void Validate(DownloadRequest request)
        {
            if (request == null)
            {
                throw GameFrameworkException.Create("DownloadRequestNull", "Download request can not be null.", "Configuration");
            }

            if (request.Urls == null || request.Urls.Count == 0)
            {
                throw GameFrameworkException.Create("DownloadUrlMissing", "Download request requires at least one url.", "Configuration");
            }

            for (var i = 0; i < request.Urls.Count; i++)
            {
                var url = request.Urls[i];
                if (string.IsNullOrWhiteSpace(url))
                {
                    throw GameFrameworkException.Create("DownloadUrlInvalid", "Download request contains an empty url.", "Configuration");
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw GameFrameworkException.Create("DownloadUrlInvalid", $"Download request url '{url}' is invalid.", "Configuration");
                }
            }

            if (string.IsNullOrWhiteSpace(request.SavePath))
            {
                throw GameFrameworkException.Create("DownloadSavePathMissing", "Download request requires a save path.", "Configuration");
            }
        }

        internal static void PrepareSavePath(DownloadRequest request)
        {
            var directory = Path.GetDirectoryName(request.SavePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        internal static bool CanReuseExistingFile(DownloadRequest request)
        {
            return File.Exists(request.SavePath) && !request.Overwrite;
        }

        internal static DownloadResult CreateExistingFileResult(DownloadRequest request, Func<string, string, bool> verifyFile)
        {
            var fileInfo = new FileInfo(request.SavePath);
            var sizeMatched = request.ExpectedSizeBytes <= 0 || !fileInfo.Exists || fileInfo.Length == request.ExpectedSizeBytes;
            var isVerified = sizeMatched && (verifyFile?.Invoke(request.SavePath, request.ExpectedHash) ?? true);
            return new DownloadResult
            {
                Status = isVerified ? DownloadStatus.Succeeded : DownloadStatus.Failed,
                Stage = isVerified ? "Completed" : "Verifying",
                SavePath = request.SavePath,
                DownloadedBytes = fileInfo.Exists ? fileInfo.Length : 0,
                TotalBytes = request.ExpectedSizeBytes > 0 ? request.ExpectedSizeBytes : fileInfo.Exists ? fileInfo.Length : 0,
                IsVerified = isVerified,
                ErrorMessage = isVerified ? null : $"Existing file verification failed: {request.SavePath}",
                Error = isVerified
                    ? null
                    : GameFrameworkException.Create("ExistingFileVerificationFailed", $"Existing file verification failed: {request.SavePath}", "Validation", true, request.SavePath, stage: "Verifying"),
                IsResumed = false,
                WorkingSavePath = request.SavePath,
                UsedTemporaryFile = false,
                CommitSucceeded = true,
                CleanupPerformed = false
            };
        }

        internal static string[] ToArray(DownloadRequest request)
        {
            var urls = new string[request.Urls.Count];
            for (var i = 0; i < request.Urls.Count; i++)
            {
                urls[i] = request.Urls[i];
            }

            return urls;
        }

        private static DownloadPolicy ClonePolicy(DownloadPolicy policy)
        {
            if (policy == null)
            {
                return null;
            }

            return new DownloadPolicy
            {
                UseTemporaryFile = policy.UseTemporaryFile,
                TemporaryDirectory = policy.TemporaryDirectory,
                CleanupTemporaryFileOnFailure = policy.CleanupTemporaryFileOnFailure,
                CleanupTemporaryFileOnCancel = policy.CleanupTemporaryFileOnCancel,
                RetryCountOverride = policy.RetryCountOverride,
                RetryDelayMilliseconds = policy.RetryDelayMilliseconds,
                TimeoutSecondsOverride = policy.TimeoutSecondsOverride,
                Priority = policy.Priority,
                MaxBytesPerSecond = policy.MaxBytesPerSecond
            };
        }
    }
}

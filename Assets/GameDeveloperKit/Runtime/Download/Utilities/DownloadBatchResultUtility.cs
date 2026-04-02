using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    internal static class DownloadBatchResultUtility
    {
        internal static DownloadBatchResult CreateEmptySuccess()
        {
            return new DownloadBatchResult
            {
                Success = true,
                Status = DownloadStatus.Succeeded,
                Stage = "Completed",
                Results = Array.Empty<DownloadResult>(),
                SucceededCount = 0,
                CleanupPerformedCount = 0
            };
        }

        internal static DownloadBatchResult CreateFailure(
            IReadOnlyList<DownloadResult> results,
            DownloadResult failedResult,
            long downloadedBytes,
            long totalBytes)
        {
            return new DownloadBatchResult
            {
                Success = false,
                Status = failedResult?.Status ?? DownloadStatus.Failed,
                Stage = failedResult?.Stage ?? "Failed",
                Results = results ?? Array.Empty<DownloadResult>(),
                ErrorMessage = failedResult?.ErrorMessage,
                Error = failedResult?.Error,
                DownloadedBytes = downloadedBytes,
                TotalBytes = totalBytes,
                SucceededCount = CountSucceeded(results),
                TotalAttemptCount = CountAttempts(results),
                CleanupPerformedCount = CountCleanupPerformed(results),
                FailureKind = failedResult?.FailureKind
            };
        }

        internal static DownloadBatchResult CreateSuccess(
            IReadOnlyList<DownloadResult> results,
            long downloadedBytes,
            long totalBytes)
        {
            return new DownloadBatchResult
            {
                Success = true,
                Status = DownloadStatus.Succeeded,
                Stage = "Completed",
                Results = results ?? Array.Empty<DownloadResult>(),
                DownloadedBytes = downloadedBytes,
                TotalBytes = totalBytes,
                SucceededCount = CountSucceeded(results),
                TotalAttemptCount = CountAttempts(results),
                CleanupPerformedCount = CountCleanupPerformed(results)
            };
        }

        internal static int CountSucceeded(IReadOnlyList<DownloadResult> results)
        {
            if (results == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < results.Count; i++)
            {
                if (results[i]?.Status == DownloadStatus.Succeeded)
                {
                    count++;
                }
            }

            return count;
        }

        internal static int CountAttempts(IReadOnlyList<DownloadResult> results)
        {
            if (results == null)
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < results.Count; i++)
            {
                total += Math.Max(0, results[i]?.AttemptCount ?? 0);
            }

            return total;
        }

        internal static int CountCleanupPerformed(IReadOnlyList<DownloadResult> results)
        {
            if (results == null)
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < results.Count; i++)
            {
                if (results[i]?.CleanupPerformed == true)
                {
                    total++;
                }
            }

            return total;
        }
    }
}

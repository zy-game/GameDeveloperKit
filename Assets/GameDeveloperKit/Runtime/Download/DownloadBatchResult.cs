using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    public sealed class DownloadBatchResult
    {
        public DownloadStatus Status { get; set; }

        public IReadOnlyList<DownloadResult> Results { get; set; }

        public string ErrorMessage { get; set; }

        public long DownloadedBytes { get; set; }

        public long TotalBytes { get; set; }
    }
}

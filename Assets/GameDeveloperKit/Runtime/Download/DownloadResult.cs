namespace GameDeveloperKit.Runtime
{
    public sealed class DownloadResult
    {
        public DownloadStatus Status { get; set; }

        public string SavePath { get; set; }

        public string ErrorMessage { get; set; }

        public long DownloadedBytes { get; set; }

        public long TotalBytes { get; set; }

        public bool IsVerified { get; set; }
    }
}

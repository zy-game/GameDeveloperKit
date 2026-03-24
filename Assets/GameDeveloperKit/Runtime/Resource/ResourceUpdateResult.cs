namespace GameDeveloperKit.Runtime
{
    public sealed class ResourceUpdateResult
    {
        public bool IsUpdated { get; set; }

        public int DownloadedFileCount { get; set; }

        public long DownloadedBytes { get; set; }

        public string ErrorMessage { get; set; }
    }
}

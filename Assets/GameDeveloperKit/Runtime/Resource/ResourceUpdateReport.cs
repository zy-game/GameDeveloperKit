namespace GameDeveloperKit.Runtime
{
    public sealed class ResourceUpdateReport
    {
        public bool IsUpdated { get; set; }

        public ResourceUpdateState State { get; set; }

        public int DownloadedFileCount { get; set; }

        public int RemovedFileCount { get; set; }

        public long DownloadedBytes { get; set; }

        public string ErrorMessage { get; set; }

        public string LocalManifestVersion { get; set; }

        public string RemoteManifestVersion { get; set; }
    }
}

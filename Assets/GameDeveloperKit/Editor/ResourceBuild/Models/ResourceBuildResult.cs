namespace GameDeveloperKit.Editor
{
    internal sealed class ResourceBuildResult
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public int PackageCount { get; set; }

        public int BundleCount { get; set; }

        public int EntryCount { get; set; }

        public string OutputRoot { get; set; }
    }
}

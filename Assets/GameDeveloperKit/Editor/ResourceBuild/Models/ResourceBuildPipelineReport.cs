namespace GameDeveloperKit.Editor
{
    internal sealed class ResourceBuildPipelineReport
    {
        public bool Success { get; set; }

        public string ErrorMessage { get; set; }

        public int BundleCount { get; set; }

        public int EntryCount { get; set; }

        public string OutputRoot { get; set; }
    }
}

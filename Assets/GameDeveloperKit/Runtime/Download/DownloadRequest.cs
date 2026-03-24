using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    public sealed class DownloadRequest
    {
        public IReadOnlyList<string> Urls { get; set; }

        public string SavePath { get; set; }

        public int TimeoutSeconds { get; set; } = 30;

        public int RetryCount { get; set; } = 2;

        public int ChunkCount { get; set; } = 1;

        public bool Overwrite { get; set; } = true;

        public string ExpectedHash { get; set; }
    }
}

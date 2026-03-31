using System;

namespace GameDeveloperKit.Editor
{
    [Serializable]
    internal sealed class ResourceVersionDetail
    {
        public string Version;
        public string BuildTimeUtc;
        public long SizeBytes;
        public int BundleCount;
        public string ManifestPath;
    }
}

using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    public sealed class ResourceManifestComparisonResult
    {
        public bool IsChanged { get; set; }

        public IReadOnlyList<ResourceManifestEntry> AddedOrModifiedEntries { get; set; }

        public IReadOnlyList<ResourceManifestEntry> RemovedEntries { get; set; }

        public string LocalVersion { get; set; }

        public string RemoteVersion { get; set; }
    }
}

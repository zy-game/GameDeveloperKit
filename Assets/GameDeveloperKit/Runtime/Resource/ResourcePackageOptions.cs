using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    public sealed class ResourcePackageOptions
    {
        public string RootPath { get; set; }

        public float ReleaseDelaySeconds { get; set; } = 5f;

        public IReadOnlyList<ResourceEntry> Entries { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    [Serializable]
    public sealed class ResourceManifest
    {
        public string Version;

        public List<ResourceManifestEntry> Entries = new();
    }

    [Serializable]
    public sealed class ResourceManifestEntry
    {
        public string Name;

        public string Version;

        public string Hash;

        public long SizeBytes;

        public string AssetType;

        public List<string> Labels = new();

        public List<string> Dependencies = new();

        public string FullPath;

        public ResourceEntryKind Kind;
    }
}

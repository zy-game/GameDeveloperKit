using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    public sealed class ResourceEntry
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public string Hash { get; set; }

        public long SizeBytes { get; set; }

        public Type AssetType { get; set; }

        public IReadOnlyList<string> Labels { get; set; }

        public IReadOnlyList<string> Dependencies { get; set; }

        public string FullPath { get; set; }

        public ResourceEntryKind Kind { get; set; }
    }
}

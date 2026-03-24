using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    [Serializable]
    public sealed class ResourcePackageDefinition
    {
        public string PackageName;

        public bool IsDefault;

        public ResourcePackageRole Role;

        public string ManifestRelativePath;

        public string StreamingAssetsRoot;

        public string PersistentRoot;

        public string RemoteBaseUrl;

        public List<ResourceEntry> Entries = new();
    }
}

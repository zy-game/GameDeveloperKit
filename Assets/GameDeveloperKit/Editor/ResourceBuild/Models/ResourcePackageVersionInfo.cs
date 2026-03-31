using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Editor
{
    [Serializable]
    internal sealed class ResourcePackageVersionInfo
    {
        public string Name;
        public string CurrentVersion;
        public string PackageRole;
        public List<ResourceVersionDetail> Versions = new();
    }
}

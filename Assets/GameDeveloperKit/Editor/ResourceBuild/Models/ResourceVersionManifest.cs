using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Editor
{
    [Serializable]
    internal sealed class ResourceVersionManifest
    {
        public string Version = "1.0";
        public string UpdateTimeUtc;
        public List<ResourcePackageVersionInfo> Packages = new();
    }
}

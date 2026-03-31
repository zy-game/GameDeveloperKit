using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Editor
{
    [Serializable]
    internal sealed class ResourceBuiltBundleRecord
    {
        public string BundleName;
        public string FileName;
        public string Hash;
        public long SizeBytes;
        public List<string> Dependencies = new();
        public List<string> AssetPaths = new();
    }
}

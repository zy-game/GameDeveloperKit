using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Editor
{
    [Serializable]
    internal sealed class ResourceBuildExecutionReport
    {
        public string PackageName;
        public string PackageVersion;
        public bool Success;
        public string ErrorMessage;
        public string BuildStartUtc;
        public string BuildEndUtc;
        public double DurationSeconds;
        public string OutputPath;
        public int BundleCount;
        public long TotalSizeBytes;
        public List<ResourceBuiltBundleRecord> Bundles = new();
        public List<string> Logs = new();
    }
}

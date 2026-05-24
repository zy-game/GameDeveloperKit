using System.Collections.Generic;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源组信息
    /// </summary>
    public sealed class PackageInfo
    {
        public string Name;
        public string Version;
        public string Hash;
        public List<BundleInfo> Bundles = new List<BundleInfo>();
    }
}
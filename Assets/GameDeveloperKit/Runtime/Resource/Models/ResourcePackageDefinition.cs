using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源包定义，描述资源包的基础配置和资源条目。
    /// </summary>
    [Serializable]
    public sealed class ResourcePackageDefinition
    {
        /// <summary>
        /// 资源包名称。
        /// </summary>
        public string PackageName;

        /// <summary>
        /// 资源包角色。
        /// </summary>
        public ResourcePackageRole Role;

        /// <summary>
        /// 资源清单相对路径。
        /// </summary>
        public string ManifestRelativePath;

        /// <summary>
        /// StreamingAssets 根目录。
        /// </summary>
        public string StreamingAssetsRoot;

        /// <summary>
        /// 持久化目录根路径。
        /// </summary>
        public string PersistentRoot;

        /// <summary>
        /// 远端资源基础地址。
        /// </summary>
        public string RemoteBaseUrl;

        /// <summary>
        /// 资源条目集合。
        /// </summary>
        public List<ResourceEntry> Entries = new();

        /// <summary>
        /// 模拟模式下的搜索根目录集合。
        /// </summary>
        public List<string> SimulateSearchRoots = new();
    }
}

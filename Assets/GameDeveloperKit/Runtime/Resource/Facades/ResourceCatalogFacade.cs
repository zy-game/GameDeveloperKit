using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源目录门面，提供对资源模块的资源目录功能的访问。
    /// </summary>
    public sealed class ResourceCatalogFacade : IResourceCatalog
    {
        private readonly ResourceModule _module;

        /// <summary>
        /// 初始化资源目录门面的新实例。
        /// </summary>
        /// <param name="module">资源模块。</param>
        public ResourceCatalogFacade(ResourceModule module)
        {
            _module = module;
        }

        /// <summary>
        /// 检查是否存在指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>如果资源包存在则返回true，否则返回false。</returns>
        public bool HasPackage(string packageName)
        {
            return _module.HasPackage(packageName);
        }

        /// <summary>
        /// 尝试获取指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="package">输出的资源包。</param>
        /// <returns>如果资源包存在则返回true，否则返回false。</returns>
        public bool TryGetPackage(string packageName, out IResourcePackage package)
        {
            return _module.TryGetPackage(packageName, out package);
        }

        /// <summary>
        /// 获取指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源包。</returns>
        public IResourcePackage GetPackage(string packageName)
        {
            return _module.GetPackage(packageName);
        }

        /// <summary>
        /// 查找指定位置的资源。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源类型筛选。</param>
        /// <returns>资源条目列表。</returns>
        public IReadOnlyList<ResourceEntry> Find(ResourceLocation location, ResourceEntryKind? kind = null)
        {
            return _module.Find(location, kind);
        }

        /// <summary>
        /// 解析指定位置的资源包。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源类型筛选。</param>
        /// <returns>资源包。</returns>
        public IResourcePackage ResolvePackage(ResourceLocation location, ResourceEntryKind? kind = null)
        {
            return _module.ResolvePackage(location, kind);
        }
    }
}

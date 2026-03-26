using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源目录接口，用于管理和查询资源包及其条目。
    /// </summary>
    public interface IResourceCatalog
    {
        /// <summary>
        /// 检查是否存在指定名称的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>如果存在返回true，否则返回false。</returns>
        bool HasPackage(string packageName);

        /// <summary>
        /// 尝试获取指定名称的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="package">输出的资源包实例。</param>
        /// <returns>如果获取成功返回true，否则返回false。</returns>
        bool TryGetPackage(string packageName, out IResourcePackage package);

        /// <summary>
        /// 获取指定名称的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源包实例。</returns>
        IResourcePackage GetPackage(string packageName);

        /// <summary>
        /// 查找指定位置的资源条目列表。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源条目类型。</param>
        /// <returns>资源条目列表。</returns>
        IReadOnlyList<ResourceEntry> Find(ResourceLocation location, ResourceEntryKind? kind = null);

        /// <summary>
        /// 解析指定位置的资源包。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源条目类型。</param>
        /// <returns>资源包实例。</returns>
        IResourcePackage ResolvePackage(ResourceLocation location, ResourceEntryKind? kind = null);
    }
}

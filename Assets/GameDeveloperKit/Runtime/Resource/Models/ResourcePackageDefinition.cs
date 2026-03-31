using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源包打包策略。
    /// </summary>
    public enum ResourcePackageBuildStrategy
    {
        /// <summary>
        /// 一个资源文件一个 AssetBundle。
        /// </summary>
        OneFile,

        /// <summary>
        /// 按收集根目录下一层目录打包，不继续向更深层拆分。
        /// </summary>
        Dir,

        /// <summary>
        /// 按资源标签打包。
        /// </summary>
        Label
    }

    /// <summary>
    /// 资源收集策略。
    /// </summary>
    public enum ResourcePackageCollectionStrategy
    {
        /// <summary>
        /// 不启用自动收集。
        /// </summary>
        ManualEntries,

        /// <summary>
        /// 按目录收集。
        /// </summary>
        Directory,

        /// <summary>
        /// 按标签收集。
        /// </summary>
        Label,

        /// <summary>
        /// 按类型收集。
        /// </summary>
        Type,

        /// <summary>
        /// 按依赖收集。
        /// </summary>
        Dependency,

        /// <summary>
        /// 按 AssetDatabase 查询语法收集。
        /// </summary>
        Query
    }

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
        /// 资源包版本。
        /// </summary>
        public string Version = "1.0.0";

        /// <summary>
        /// 资源包打包策略。
        /// </summary>
        public ResourcePackageBuildStrategy BuildStrategy = ResourcePackageBuildStrategy.OneFile;

        /// <summary>
        /// 资源收集策略。
        /// </summary>
        public ResourcePackageCollectionStrategy CollectionStrategy = ResourcePackageCollectionStrategy.ManualEntries;

        /// <summary>
        /// 逻辑包名称覆盖。
        /// </summary>
        public string BundleNameOverride;

        /// <summary>
        /// 资源清单相对路径；为空时默认使用 manifest.json。
        /// </summary>
        public string ManifestRelativePath = "manifest.json";

        /// <summary>
        /// StreamingAssets 根目录覆盖；为空时按包名自动推导。
        /// </summary>
        public string StreamingAssetsRoot;

        /// <summary>
        /// 持久化目录根路径覆盖；为空时按包名自动推导。
        /// </summary>
        public string PersistentRoot;

        /// <summary>
        /// 远端资源基础地址。
        /// </summary>
        public string RemoteBaseUrl;

        /// <summary>
        /// 自动收集的搜索根目录集合。
        /// </summary>
        public List<string> CollectRoots = new();

        /// <summary>
        /// 目录收集时包含的文件扩展名列表（例如 ".prefab", ".png"）。为空时包含所有扩展名。
        /// </summary>
        public List<string> SearchExtensions = new();

        /// <summary>
        /// 目录收集时是否包含子目录。
        /// </summary>
        public bool IncludeSubDirectories = true;

        /// <summary>
        /// 标签收集器使用的标签列表。
        /// </summary>
        public List<string> Labels = new();

        /// <summary>
        /// 类型收集器使用的类型名称。
        /// </summary>
        public string TypeName;

        /// <summary>
        /// 依赖收集器使用的根资源路径。
        /// </summary>
        public string RootAssetPath;

        /// <summary>
        /// 查询收集器使用的 AssetDatabase 查询语句。
        /// </summary>
        public string Query;

        /// <summary>
        /// 收集器通用排除模式。
        /// </summary>
        public List<string> ExcludePatterns = new();

        /// <summary>
        /// 资源条目集合。
        /// </summary>
        public List<ResourceEntry> Entries = new();

        /// <summary>
        /// 模拟模式下的搜索根目录集合。
        /// </summary>
        public List<string> SimulateSearchRoots = new();

        public string ResolveManifestRelativePath()
        {
            return string.IsNullOrWhiteSpace(ManifestRelativePath) ? "manifest.json" : ManifestRelativePath.Replace('\\', '/');
        }

        public string ResolveStreamingAssetsRelativeRoot()
        {
            return string.IsNullOrWhiteSpace(StreamingAssetsRoot)
                ? $"GameDeveloperKit/Packages/{PackageName}"
                : StreamingAssetsRoot.Replace('\\', '/');
        }

        public string ResolvePersistentRelativeRoot()
        {
            return string.IsNullOrWhiteSpace(PersistentRoot)
                ? $"GameDeveloperKit/Packages/{PackageName}"
                : PersistentRoot.Replace('\\', '/');
        }
    }
}

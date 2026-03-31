using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源清单，描述指定资源包的版本及其条目集合。
    /// </summary>
    [Serializable]
    public sealed class ResourceManifest
    {
        /// <summary>
        /// 应用版本。
        /// </summary>
        public string AppVersion;

        /// <summary>
        /// 构建时间（UTC ISO8601）。
        /// </summary>
        public string BuildTimeUtc;

        /// <summary>
        /// 兼容旧版字段：清单版本号。
        /// </summary>
        public string Version;

        /// <summary>
        /// 统一清单中的资源包集合。
        /// </summary>
        public List<ResourceManifestPackage> Packages = new();
    }

    [Serializable]
    public sealed class ResourceManifestPackage
    {
        public string Name;
        public ResourcePackageRole Role;
        public string Version;
        public ResourcePackageBuildStrategy BuildStrategy;
        public List<ResourceManifestEntry> Entries = new();
    }
}

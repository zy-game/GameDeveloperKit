using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源清单比对结果，记录本地与远端清单之间的差异。
    /// </summary>
    public sealed class ResourceManifestComparisonResult
    {
        /// <summary>
        /// 获取或设置清单内容是否发生变化。
        /// </summary>
        public bool IsChanged { get; set; }

        /// <summary>
        /// 获取或设置新增或已修改的资源条目集合。
        /// </summary>
        public IReadOnlyList<ResourceManifestEntry> AddedOrModifiedEntries { get; set; }

        /// <summary>
        /// 获取或设置已移除的资源条目集合。
        /// </summary>
        public IReadOnlyList<ResourceManifestEntry> RemovedEntries { get; set; }

        /// <summary>
        /// 获取或设置本地资源清单版本号。
        /// </summary>
        public string LocalVersion { get; set; }

        /// <summary>
        /// 获取或设置远端资源清单版本号。
        /// </summary>
        public string RemoteVersion { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源条目，描述单个资源的元数据。
    /// </summary>
    public sealed class ResourceEntry
    {
        /// <summary>
        /// 资源名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 资源版本。
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 资源哈希值。
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// 资源大小（字节）。
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// 资源类型。
        /// </summary>
        public Type AssetType { get; set; }

        /// <summary>
        /// 资源标签列表。
        /// </summary>
        public IReadOnlyList<string> Labels { get; set; }

        /// <summary>
        /// 资源依赖列表。
        /// </summary>
        public IReadOnlyList<string> Dependencies { get; set; }

        /// <summary>
        /// 资源完整路径。
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// 资源所在的 AssetBundle 文件名。
        /// </summary>
        public string BundleName { get; set; }

        /// <summary>
        /// 资源条目类型。
        /// </summary>
        public ResourceEntryKind Kind { get; set; }
    }
}

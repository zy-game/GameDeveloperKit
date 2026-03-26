using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源清单条目，描述单个资源文件的元数据。
    /// </summary>
    [Serializable]
    public sealed class ResourceManifestEntry
    {
        /// <summary>
        /// 资源名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 资源版本号。
        /// </summary>
        public string Version;

        /// <summary>
        /// 资源哈希值。
        /// </summary>
        public string Hash;

        /// <summary>
        /// 资源大小（字节）。
        /// </summary>
        public long SizeBytes;

        /// <summary>
        /// 资源类型名称。
        /// </summary>
        public string AssetType;

        /// <summary>
        /// 资源标签集合。
        /// </summary>
        public List<string> Labels = new();

        /// <summary>
        /// 资源依赖集合。
        /// </summary>
        public List<string> Dependencies = new();

        /// <summary>
        /// 资源完整路径。
        /// </summary>
        public string FullPath;

        /// <summary>
        /// 资源条目类型。
        /// </summary>
        public ResourceEntryKind Kind;
    }
}

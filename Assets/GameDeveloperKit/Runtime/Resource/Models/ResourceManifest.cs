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
        /// 资源清单版本号。
        /// </summary>
        public string Version;

        /// <summary>
        /// 资源清单条目集合。
        /// </summary>
        public List<ResourceManifestEntry> Entries = new();
    }
}

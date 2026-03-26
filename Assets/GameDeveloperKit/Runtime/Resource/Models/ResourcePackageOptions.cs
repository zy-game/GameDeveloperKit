using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源包选项，定义资源加载与释放时使用的参数。
    /// </summary>
    public sealed class ResourcePackageOptions
    {
        /// <summary>
        /// 获取或设置资源根路径。
        /// </summary>
        public string RootPath { get; set; }

        /// <summary>
        /// 获取或设置资源释放延迟时间（秒）。
        /// </summary>
        public float ReleaseDelaySeconds { get; set; } = 5f;

        /// <summary>
        /// 获取或设置资源条目集合。
        /// </summary>
        public IReadOnlyList<ResourceEntry> Entries { get; set; }
    }
}

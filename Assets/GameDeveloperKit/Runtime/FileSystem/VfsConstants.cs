namespace GameDeveloperKit.File
{
    /// <summary>
    /// 虚拟文件系统常量。
    /// </summary>
    public static class VfsConstants
    {
        /// <summary>
        /// 默认单条目容量阈值，单位为字节。
        /// </summary>
        public const int DefaultThreshold = 4096;

        /// <summary>
        /// 每个虚拟文件包预分配的文件条目数量。
        /// </summary>
        public const int BundleFileCount = 20;

        /// <summary>
        /// 虚拟文件系统清单文件名。
        /// </summary>
        public const string ManifestFileName = "vfs_manifest.json";
    }
}

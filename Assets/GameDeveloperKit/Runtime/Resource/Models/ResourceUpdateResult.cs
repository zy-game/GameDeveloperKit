namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源更新结果，封装资源更新流程的最终执行结果。
    /// </summary>
    public sealed class ResourceUpdateResult : FrameworkOperationResult
    {
        /// <summary>
        /// 获取或设置资源包名称。
        /// </summary>
        public string PackageName { get; set; }

        /// <summary>
        /// 获取或设置资源是否已更新。
        /// </summary>
        public bool IsUpdated { get; set; }

        /// <summary>
        /// 获取或设置更新结束时的状态。
        /// </summary>
        public ResourceUpdateState State { get; set; }

        /// <summary>
        /// 获取或设置是否执行了回滚。
        /// </summary>
        public bool IsRolledBack { get; set; }

        /// <summary>
        /// 获取或设置已下载文件数量。
        /// </summary>
        public int DownloadedFileCount { get; set; }

        /// <summary>
        /// 获取或设置已移除文件数量。
        /// </summary>
        public int RemovedFileCount { get; set; }

        /// <summary>
        /// 获取或设置已下载字节数。
        /// </summary>
        public long DownloadedBytes { get; set; }

        /// <summary>
        /// 获取或设置回滚字节数。
        /// </summary>
        public long RolledBackBytes { get; set; }

        /// <summary>
        /// 获取或设置恢复说明信息。
        /// </summary>
        public string RecoveryMessage { get; set; }
    }
}

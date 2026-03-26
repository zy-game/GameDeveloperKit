using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源更新报告，记录更新流程中的状态、统计信息和状态流转轨迹。
    /// </summary>
    public sealed class ResourceUpdateReport : FrameworkOperationResult
    {
        /// <summary>
        /// 获取或设置资源是否已更新。
        /// </summary>
        public bool IsUpdated { get; set; }

        /// <summary>
        /// 获取或设置当前更新状态。
        /// </summary>
        public ResourceUpdateState State { get; set; }

        /// <summary>
        /// 获取或设置上一个更新状态。
        /// </summary>
        public ResourceUpdateState PreviousState { get; set; }

        /// <summary>
        /// 获取或设置已下载文件数量。
        /// </summary>
        public int DownloadedFileCount { get; set; }

        /// <summary>
        /// 获取或设置已移除文件数量。
        /// </summary>
        public int RemovedFileCount { get; set; }

        /// <summary>
        /// 获取或设置已回滚文件数量。
        /// </summary>
        public int RolledBackFileCount { get; set; }

        /// <summary>
        /// 获取或设置已下载字节数。
        /// </summary>
        public long DownloadedBytes { get; set; }

        /// <summary>
        /// 获取或设置已回滚字节数。
        /// </summary>
        public long RolledBackBytes { get; set; }

        /// <summary>
        /// 获取或设置恢复说明信息。
        /// </summary>
        public string RecoveryMessage { get; set; }

        /// <summary>
        /// 获取或设置本地资源清单版本号。
        /// </summary>
        public string LocalManifestVersion { get; set; }

        /// <summary>
        /// 获取或设置远端资源清单版本号。
        /// </summary>
        public string RemoteManifestVersion { get; set; }

        /// <summary>
        /// 获取更新状态流转记录。
        /// </summary>
        public List<ResourceUpdateTransition> Transitions { get; } = new();
    }
}

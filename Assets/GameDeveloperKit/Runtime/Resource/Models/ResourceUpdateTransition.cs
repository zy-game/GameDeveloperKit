using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源更新状态流转记录，描述一次状态切换的详细信息。
    /// </summary>
    [Serializable]
    public sealed class ResourceUpdateTransition
    {
        /// <summary>
        /// 获取或设置流转前的状态。
        /// </summary>
        public ResourceUpdateState PreviousState { get; set; }

        /// <summary>
        /// 获取或设置流转后的状态。
        /// </summary>
        public ResourceUpdateState State { get; set; }

        /// <summary>
        /// 获取或设置当前操作阶段。
        /// </summary>
        public FrameworkOperationStage Stage { get; set; }

        /// <summary>
        /// 获取或设置状态流转附加消息。
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 获取或设置状态流转时间（UTC）。
        /// </summary>
        public string TimestampUtc { get; set; }
    }
}

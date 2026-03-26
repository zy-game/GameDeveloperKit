using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 平台功能报告，包含平台环境、能力和权限的完整信息。
    /// </summary>
    [Serializable]
    public sealed class PlatformFeatureReport
    {
        /// <summary>
        /// 运行平台名称。
        /// </summary>
        public string Platform;

        /// <summary>
        /// 系统语言。
        /// </summary>
        public string Language;

        /// <summary>
        /// 渠道标识。
        /// </summary>
        public string Channel;

        /// <summary>
        /// 网络可达性状态。
        /// </summary>
        public string NetworkReachability;

        /// <summary>
        /// 桥接框架标识。
        /// </summary>
        public string Bridge;

        /// <summary>
        /// 是否为移动平台。
        /// </summary>
        public bool IsMobilePlatform;

        /// <summary>
        /// 是否在编辑器中运行。
        /// </summary>
        public bool IsEditor;

        /// <summary>
        /// 平台能力列表。
        /// </summary>
        public PlatformFeatureEntry[] Capabilities;

        /// <summary>
        /// 权限状态列表。
        /// </summary>
        public PlatformFeatureEntry[] Permissions;

        /// <summary>
        /// 自定义值列表。
        /// </summary>
        public PlatformFeatureEntry[] CustomValues;
    }
}

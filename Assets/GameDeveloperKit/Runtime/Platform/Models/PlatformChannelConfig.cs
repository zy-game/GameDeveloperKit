using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 平台渠道配置，包含多个渠道配置条目的集合。
    /// </summary>
    [Serializable]
    public sealed class PlatformChannelConfig
    {
        /// <summary>
        /// 渠道配置条目数组。
        /// </summary>
        public PlatformChannelConfigEntry[] Entries;
    }
}

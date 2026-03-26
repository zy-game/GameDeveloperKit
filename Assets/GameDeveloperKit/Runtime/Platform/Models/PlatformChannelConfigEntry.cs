using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 平台渠道配置条目，表示单个渠道配置的键值对。
    /// </summary>
    [Serializable]
    public sealed class PlatformChannelConfigEntry
    {
        /// <summary>
        /// 配置键名。
        /// </summary>
        public string Key;

        /// <summary>
        /// 配置值。
        /// </summary>
        public string Value;
    }
}

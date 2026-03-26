using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 平台功能条目，表示单个平台特性或权限的键值对。
    /// </summary>
    [Serializable]
    public sealed class PlatformFeatureEntry
    {
        /// <summary>
        /// 特性键名。
        /// </summary>
        public string Key;

        /// <summary>
        /// 特性值。
        /// </summary>
        public string Value;
    }
}

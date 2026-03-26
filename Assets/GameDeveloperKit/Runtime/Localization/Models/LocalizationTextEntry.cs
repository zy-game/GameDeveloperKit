using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 本地化文本条目，定义键值对形式的本地化文本
    /// </summary>
    [Serializable]
    public sealed class LocalizationTextEntry
    {
        /// <summary>
        /// 本地化键
        /// </summary>
        public string Key;

        /// <summary>
        /// 本地化值
        /// </summary>
        public string Value;
    }
}

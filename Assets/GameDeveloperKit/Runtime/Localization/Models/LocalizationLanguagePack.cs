using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 本地化语言包，包含特定语言的本地化文本和资源
    /// </summary>
    [Serializable]
    public sealed class LocalizationLanguagePack
    {
        /// <summary>
        /// 语言标识
        /// </summary>
        public string Language;

        /// <summary>
        /// 本地化文本条目数组
        /// </summary>
        public LocalizationTextEntry[] Texts;

        /// <summary>
        /// 本地化资源条目数组
        /// </summary>
        public LocalizationAssetEntry[] Assets;
    }
}

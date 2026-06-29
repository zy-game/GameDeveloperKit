using System.Collections.Generic;

namespace GameDeveloperKit.Localization
{
    /// <summary>
    /// 本地化模块快照。
    /// </summary>
    public readonly struct LocalizationSnapshot
    {
        /// <summary>
        /// 初始化本地化模块快照。
        /// </summary>
        /// <param name="currentLocale">当前 locale。</param>
        /// <param name="fallbackLocale">回退 locale。</param>
        /// <param name="loadedLocales">已加载 locale 列表。</param>
        /// <param name="missingEntries">缺失项列表。</param>
        public LocalizationSnapshot(
            string currentLocale,
            string fallbackLocale,
            IReadOnlyList<string> loadedLocales,
            IReadOnlyList<MissingLocalizationEntry> missingEntries)
        {
            CurrentLocale = currentLocale;
            FallbackLocale = fallbackLocale;
            LoadedLocales = loadedLocales;
            MissingEntries = missingEntries;
        }

        /// <summary>
        /// 当前 locale。
        /// </summary>
        public string CurrentLocale { get; }

        /// <summary>
        /// 回退 locale。
        /// </summary>
        public string FallbackLocale { get; }

        /// <summary>
        /// 已加载 locale 列表。
        /// </summary>
        public IReadOnlyList<string> LoadedLocales { get; }

        /// <summary>
        /// 缺失项列表。
        /// </summary>
        public IReadOnlyList<MissingLocalizationEntry> MissingEntries { get; }
    }
}

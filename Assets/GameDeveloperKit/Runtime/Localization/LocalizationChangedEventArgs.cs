namespace GameDeveloperKit.Localization
{
    /// <summary>
    /// 本地化语言切换事件参数。
    /// </summary>
    public readonly struct LocalizationChangedEventArgs
    {
        /// <summary>
        /// 初始化本地化语言切换事件参数。
        /// </summary>
        /// <param name="previousLocale">切换前的 locale。</param>
        /// <param name="currentLocale">切换后的 locale。</param>
        public LocalizationChangedEventArgs(string previousLocale, string currentLocale)
        {
            PreviousLocale = previousLocale;
            CurrentLocale = currentLocale;
        }

        /// <summary>
        /// 切换前的 locale。
        /// </summary>
        public string PreviousLocale { get; }

        /// <summary>
        /// 切换后的 locale。
        /// </summary>
        public string CurrentLocale { get; }
    }
}

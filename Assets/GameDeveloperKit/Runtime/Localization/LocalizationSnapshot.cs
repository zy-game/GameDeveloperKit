using System.Collections.Generic;

namespace GameDeveloperKit.Localization
{
    public readonly struct LocalizationSnapshot
    {
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

        public string CurrentLocale { get; }

        public string FallbackLocale { get; }

        public IReadOnlyList<string> LoadedLocales { get; }

        public IReadOnlyList<MissingLocalizationEntry> MissingEntries { get; }
    }
}

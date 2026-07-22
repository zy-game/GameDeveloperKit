using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Localization
{
    internal sealed class LocalizationRuntimeState
    {
        private readonly IReadOnlyDictionary<string, long> m_KeyIds;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<long, string>> m_Texts;
        private readonly IReadOnlyList<LocalizationAssetLease> m_Leases;

        public LocalizationRuntimeState(
            string catalogLocation,
            LocalizationCatalogAsset catalog,
            string currentLocale,
            IReadOnlyList<string> localeOrder,
            IReadOnlyDictionary<string, LocalizationLocaleAsset> localeAssets,
            IReadOnlyList<LocalizationAssetLease> leases)
        {
            CatalogLocation = catalogLocation;
            Catalog = catalog;
            CurrentLocale = currentLocale;
            LocaleOrder = localeOrder.ToArray();
            FallbackLocale = LocaleOrder.Count > 1 ? LocaleOrder[1] : null;
            m_Leases = leases.ToArray();
            m_KeyIds = catalog.Keys
                .Where(entry => entry != null)
                .ToDictionary(entry => entry.Key, entry => entry.Id, StringComparer.Ordinal);
            m_Texts = localeAssets.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<long, string>)pair.Value.Entries
                    .Where(entry => entry != null)
                    .ToDictionary(entry => entry.KeyId, entry => entry.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        public string CatalogLocation { get; }

        public LocalizationCatalogAsset Catalog { get; }

        public string CurrentLocale { get; }

        public string FallbackLocale { get; }

        public IReadOnlyList<string> LocaleOrder { get; }

        public bool TryGetKeyId(string key, out long keyId)
        {
            return m_KeyIds.TryGetValue(key, out keyId);
        }

        public bool TryGetText(string locale, long keyId, out string text)
        {
            text = null;
            return m_Texts.TryGetValue(locale, out var entries) && entries.TryGetValue(keyId, out text);
        }

        public async UniTask ReleaseAsync()
        {
            for (var i = m_Leases.Count - 1; i >= 0; i--)
            {
                await m_Leases[i].ReleaseAsync();
            }
        }
    }
}

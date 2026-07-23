using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameDeveloperKit.Localization
{
    public sealed class LocalizationCatalogAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int m_SchemaVersion = CurrentSchemaVersion;
        [SerializeField] private string m_CatalogId;
        [SerializeField] private string m_DefaultLocale;
        [SerializeField] private List<LocalizationKeyEntry> m_Keys = new List<LocalizationKeyEntry>();
        [SerializeField] private List<LocalizationLocaleDescriptor> m_Locales = new List<LocalizationLocaleDescriptor>();

        public int SchemaVersion => m_SchemaVersion;

        public string CatalogId => m_CatalogId;

        public string DefaultLocale => m_DefaultLocale;

        public IReadOnlyList<LocalizationKeyEntry> Keys => m_Keys;

        public IReadOnlyList<LocalizationLocaleDescriptor> Locales => m_Locales;

        public void Replace(
            string catalogId,
            string defaultLocale,
            IEnumerable<LocalizationKeyEntry> keys,
            IEnumerable<LocalizationLocaleDescriptor> locales,
            int schemaVersion = CurrentSchemaVersion)
        {
            m_SchemaVersion = schemaVersion;
            m_CatalogId = catalogId;
            m_DefaultLocale = defaultLocale;
            m_Keys = CopyKeys(keys);
            m_Locales = CopyLocales(locales);
        }

        public bool TryGetKey(string key, out LocalizationKeyEntry entry)
        {
            entry = m_Keys.FirstOrDefault(candidate =>
                string.Equals(candidate?.Key, key, StringComparison.Ordinal));
            return entry != null;
        }

        public bool TryGetKey(long keyId, out LocalizationKeyEntry entry)
        {
            entry = m_Keys.FirstOrDefault(candidate => candidate != null && candidate.Id == keyId);
            return entry != null;
        }

        public bool TryGetLocale(string locale, out LocalizationLocaleDescriptor descriptor)
        {
            descriptor = m_Locales.FirstOrDefault(candidate =>
                string.Equals(candidate?.Locale, locale, StringComparison.OrdinalIgnoreCase));
            return descriptor != null;
        }

        private static List<LocalizationKeyEntry> CopyKeys(IEnumerable<LocalizationKeyEntry> entries)
        {
            return entries?
                       .Select(entry => entry == null ? null : new LocalizationKeyEntry(entry.Id, entry.Key))
                       .ToList() ??
                   new List<LocalizationKeyEntry>();
        }

        private static List<LocalizationLocaleDescriptor> CopyLocales(
            IEnumerable<LocalizationLocaleDescriptor> descriptors)
        {
            return descriptors?
                       .Select(descriptor => descriptor == null
                           ? null
                           : new LocalizationLocaleDescriptor(
                               descriptor.Locale,
                               descriptor.ResourceLocation,
                               descriptor.FallbackLocale))
                       .ToList() ??
                   new List<LocalizationLocaleDescriptor>();
        }
    }
}

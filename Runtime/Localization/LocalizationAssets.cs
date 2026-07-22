using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameDeveloperKit.Localization
{
    [Serializable]
    public sealed class LocalizationKeyEntry
    {
        [SerializeField] private long m_Id;
        [SerializeField] private string m_Key;

        public LocalizationKeyEntry(long id, string key)
        {
            m_Id = id;
            m_Key = key;
        }

        public long Id => m_Id;

        public string Key => m_Key;
    }

    [Serializable]
    public sealed class LocalizationLocaleDescriptor
    {
        [SerializeField] private string m_Locale;
        [SerializeField] private string m_ResourceLocation;
        [SerializeField] private string m_FallbackLocale;

        public LocalizationLocaleDescriptor(string locale, string resourceLocation, string fallbackLocale = null)
        {
            m_Locale = locale;
            m_ResourceLocation = resourceLocation;
            m_FallbackLocale = fallbackLocale;
        }

        public string Locale => m_Locale;

        public string ResourceLocation => m_ResourceLocation;

        public string FallbackLocale => m_FallbackLocale;
    }

    [Serializable]
    public sealed class LocalizationValueEntry
    {
        [SerializeField] private long m_KeyId;
        [SerializeField] private string m_Value;

        public LocalizationValueEntry(long keyId, string value)
        {
            m_KeyId = keyId;
            m_Value = value;
        }

        public long KeyId => m_KeyId;

        public string Value => m_Value;
    }

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

    public sealed class LocalizationLocaleAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int m_SchemaVersion = CurrentSchemaVersion;
        [SerializeField] private string m_Locale;
        [SerializeField] private long m_Revision;
        [SerializeField] private List<LocalizationValueEntry> m_Entries = new List<LocalizationValueEntry>();

        public int SchemaVersion => m_SchemaVersion;

        public string Locale => m_Locale;

        public long Revision => m_Revision;

        public IReadOnlyList<LocalizationValueEntry> Entries => m_Entries;

        public void Replace(
            string locale,
            IEnumerable<LocalizationValueEntry> entries,
            long revision,
            int schemaVersion = CurrentSchemaVersion)
        {
            m_SchemaVersion = schemaVersion;
            m_Locale = locale;
            m_Revision = revision;
            m_Entries = entries?
                            .Select(entry => entry == null
                                ? null
                                : new LocalizationValueEntry(entry.KeyId, entry.Value))
                            .ToList() ??
                        new List<LocalizationValueEntry>();
        }

        public bool TryGetValue(long keyId, out string value)
        {
            var entry = m_Entries.FirstOrDefault(candidate => candidate != null && candidate.KeyId == keyId);
            value = entry?.Value;
            return entry != null;
        }
    }
}

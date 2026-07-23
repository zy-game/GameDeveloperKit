using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameDeveloperKit.Localization
{
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

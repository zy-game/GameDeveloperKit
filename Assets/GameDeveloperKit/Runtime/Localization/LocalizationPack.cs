using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Localization
{
    public sealed class LocalizationPack : IReference
    {
        private readonly Dictionary<string, string> m_Entries;

        public LocalizationPack(string locale, IDictionary<string, string> entries)
        {
            if (locale == null)
            {
                throw new ArgumentNullException(nameof(locale));
            }

            if (string.IsNullOrWhiteSpace(locale))
            {
                throw new ArgumentException("Locale cannot be empty.", nameof(locale));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            Locale = locale;
            m_Entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in entries)
            {
                if (pair.Key == null)
                {
                    throw new ArgumentNullException(nameof(entries), "Localization key cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    throw new ArgumentException("Localization key cannot be empty.", nameof(entries));
                }

                if (m_Entries.ContainsKey(pair.Key))
                {
                    throw new GameException($"Duplicate localization key: {pair.Key}");
                }

                m_Entries.Add(pair.Key, pair.Value ?? string.Empty);
            }
        }

        public string Locale { get; private set; }

        public IReadOnlyDictionary<string, string> Entries => m_Entries;

        public static LocalizationPack FromDictionary(string locale, IDictionary<string, string> entries)
        {
            return new LocalizationPack(locale, entries);
        }

        public bool TryGetText(string key, out string text)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Localization key cannot be empty.", nameof(key));
            }

            return m_Entries.TryGetValue(key, out text);
        }

        public void Release()
        {
            Locale = null;
            m_Entries.Clear();
        }
    }
}

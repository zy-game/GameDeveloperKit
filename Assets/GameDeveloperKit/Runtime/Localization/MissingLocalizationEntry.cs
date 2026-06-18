using System;

namespace GameDeveloperKit.Localization
{
    public readonly struct MissingLocalizationEntry : IEquatable<MissingLocalizationEntry>
    {
        public MissingLocalizationEntry(string locale, string key)
        {
            Locale = locale;
            Key = key;
        }

        public string Locale { get; }

        public string Key { get; }

        public bool Equals(MissingLocalizationEntry other)
        {
            return string.Equals(Locale, other.Locale, StringComparison.Ordinal) &&
                   string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MissingLocalizationEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Locale != null ? Locale.GetHashCode() : 0) * 397) ^ (Key != null ? Key.GetHashCode() : 0);
            }
        }
    }
}

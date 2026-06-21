using System;

namespace GameDeveloperKit.Localization
{
    /// <summary>
    /// 本地化缺失项记录。
    /// </summary>
    public readonly struct MissingLocalizationEntry : IEquatable<MissingLocalizationEntry>
    {
        /// <summary>
        /// 初始化本地化缺失项记录。
        /// </summary>
        /// <param name="locale">缺失文本所属 locale。</param>
        /// <param name="key">缺失的本地化 key。</param>
        public MissingLocalizationEntry(string locale, string key)
        {
            Locale = locale;
            Key = key;
        }

        /// <summary>
        /// 缺失文本所属 locale。
        /// </summary>
        public string Locale { get; }

        /// <summary>
        /// 缺失的本地化 key。
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 判断缺失项是否相同。
        /// </summary>
        /// <param name="other">待比较的缺失项。</param>
        /// <returns>相同时返回 true。</returns>
        public bool Equals(MissingLocalizationEntry other)
        {
            return string.Equals(Locale, other.Locale, StringComparison.Ordinal) &&
                   string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断对象是否为相同缺失项。
        /// </summary>
        /// <param name="obj">待比较对象。</param>
        /// <returns>相同时返回 true。</returns>
        public override bool Equals(object obj)
        {
            return obj is MissingLocalizationEntry other && Equals(other);
        }

        /// <summary>
        /// 获取缺失项哈希值。
        /// </summary>
        /// <returns>哈希值。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Locale != null ? Locale.GetHashCode() : 0) * 397) ^ (Key != null ? Key.GetHashCode() : 0);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.Localization
{
    /// <summary>
    /// 本地化语言包，保存单个 locale 的 key 到文本映射。
    /// </summary>
    public sealed class LocalizationPack : IReference
    {
        /// <summary>
        /// 存储本地化条目。
        /// </summary>
        private readonly Dictionary<string, string> m_Entries;

        /// <summary>
        /// 初始化本地化语言包。
        /// </summary>
        /// <param name="locale">语言包 locale。</param>
        /// <param name="entries">本地化条目。</param>
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

        /// <summary>
        /// 语言包 locale。
        /// </summary>
        public string Locale { get; private set; }

        /// <summary>
        /// 只读本地化条目。
        /// </summary>
        public IReadOnlyDictionary<string, string> Entries => m_Entries;

        /// <summary>
        /// 从字典创建本地化语言包。
        /// </summary>
        /// <param name="locale">语言包 locale。</param>
        /// <param name="entries">本地化条目。</param>
        /// <returns>本地化语言包。</returns>
        public static LocalizationPack FromDictionary(string locale, IDictionary<string, string> entries)
        {
            return new LocalizationPack(locale, entries);
        }

        /// <summary>
        /// 解析框架标准本地化语言包 JSON。
        /// </summary>
        /// <param name="locale">语言包 locale。</param>
        /// <param name="json">语言包 JSON。</param>
        /// <param name="source">错误信息中的数据来源。</param>
        /// <returns>解析完成的语言包。</returns>
        public static LocalizationPack Parse(string locale, string json, string source = null)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new GameException($"Localization pack JSON is empty: {source ?? locale}");
            }

            try
            {
                var root = JToken.Parse(json, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (root is not JObject rootObject)
                {
                    throw new GameException($"Localization pack JSON must be an object: {source ?? locale}");
                }

                var entriesToken = rootObject["entries"];
                if (entriesToken != null && entriesToken is not JObject)
                {
                    throw new GameException($"Localization pack entries must be an object: {source ?? locale}");
                }

                var entriesObject = entriesToken as JObject ?? rootObject;
                var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in entriesObject.Properties())
                {
                    if (property.Value.Type == JTokenType.Object || property.Value.Type == JTokenType.Array)
                    {
                        throw new GameException($"Localization value must be scalar: {property.Name}");
                    }

                    entries.Add(property.Name, property.Value.Type == JTokenType.Null ? string.Empty : property.Value.ToString());
                }

                return new LocalizationPack(locale, entries);
            }
            catch (Exception exception) when (exception is not GameException)
            {
                throw new GameException($"Failed to parse localization pack JSON from '{source ?? locale}'.", exception);
            }
        }

        /// <summary>
        /// 尝试按 key 获取本地化文本。
        /// </summary>
        /// <param name="key">本地化 key。</param>
        /// <param name="text">本地化文本。</param>
        /// <returns>找到文本时返回 true。</returns>
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

        /// <summary>
        /// 释放语言包内容。
        /// </summary>
        public void Release()
        {
            Locale = null;
            m_Entries.Clear();
        }
    }
}

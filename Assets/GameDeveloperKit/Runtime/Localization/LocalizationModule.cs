using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.Localization
{
    /// <summary>
    /// 本地化模块，管理语言包、当前 locale、回退 locale 和文本查询。
    /// </summary>
    public sealed class LocalizationModule : GameModuleBase
    {
        /// <summary>
        /// 存储语言包。
        /// </summary>
        private readonly Dictionary<string, LocalizationPack> m_Packs = new Dictionary<string, LocalizationPack>(StringComparer.Ordinal);
        /// <summary>
        /// 存储缺失本地化条目。
        /// </summary>
        private readonly HashSet<MissingLocalizationEntry> m_MissingEntries = new HashSet<MissingLocalizationEntry>();

        /// <summary>
        /// locale 切换事件。
        /// </summary>
        public event Action<LocalizationChangedEventArgs> LocaleChanged;

        /// <summary>
        /// 当前 locale。
        /// </summary>
        public string CurrentLocale { get; private set; }

        /// <summary>
        /// 回退 locale。
        /// </summary>
        public string FallbackLocale { get; private set; }

        /// <summary>
        /// 启动本地化模块。
        /// </summary>
        public override void Startup()
        {
            ReleasePacks();
            m_MissingEntries.Clear();
            CurrentLocale = null;
            FallbackLocale = null;
        }

        /// <summary>
        /// 关闭本地化模块并释放语言包。
        /// </summary>
        public override void Shutdown()
        {
            ReleasePacks();
            m_MissingEntries.Clear();
            CurrentLocale = null;
            FallbackLocale = null;
            LocaleChanged = null;
        }

        /// <summary>
        /// 设置回退 locale。
        /// </summary>
        /// <param name="locale">回退 locale。</param>
        public void SetFallbackLocale(string locale)
        {
            ValidateText(locale, nameof(locale), "Locale cannot be empty.");
            FallbackLocale = locale;
        }

        /// <summary>
        /// 设置当前 locale。
        /// </summary>
        /// <param name="locale">当前 locale。</param>
        public void SetLocale(string locale)
        {
            ValidateText(locale, nameof(locale), "Locale cannot be empty.");
            if (string.Equals(CurrentLocale, locale, StringComparison.Ordinal))
            {
                return;
            }

            var previousLocale = CurrentLocale;
            CurrentLocale = locale;
            LocaleChanged?.Invoke(new LocalizationChangedEventArgs(previousLocale, CurrentLocale));
        }

        /// <summary>
        /// 注册本地化语言包，同 locale 已存在时替换旧语言包。
        /// </summary>
        /// <param name="pack">本地化语言包。</param>
        public void RegisterPack(LocalizationPack pack)
        {
            if (pack == null)
            {
                throw new ArgumentNullException(nameof(pack));
            }

            if (string.IsNullOrWhiteSpace(pack.Locale))
            {
                throw new ArgumentException("Locale cannot be empty.", nameof(pack));
            }

            if (m_Packs.TryGetValue(pack.Locale, out var oldPack))
            {
                oldPack.Release();
            }

            m_Packs[pack.Locale] = pack;
        }

        /// <summary>
        /// 从 Resource raw asset 加载并注册本地化语言包。
        /// </summary>
        /// <param name="locale">语言包 locale。</param>
        /// <param name="location">资源位置。</param>
        /// <returns>加载完成的语言包。</returns>
        public UniTask<LocalizationPack> LoadPackAsync(string locale, string location)
        {
            return LoadPackInternalAsync(locale, location);
        }

        /// <summary>
        /// 判断当前 locale 或回退 locale 是否包含指定文本。
        /// </summary>
        /// <param name="key">本地化 key。</param>
        /// <returns>存在文本时返回 true。</returns>
        public bool HasText(string key)
        {
            ValidateText(key, nameof(key), "Localization key cannot be empty.");
            if (TryGetPackText(CurrentLocale, key, out _))
            {
                return true;
            }

            return TryGetPackText(FallbackLocale, key, out _);
        }

        /// <summary>
        /// 获取本地化文本，按当前 locale、回退 locale、key 原文顺序回退。
        /// </summary>
        /// <param name="key">本地化 key。</param>
        /// <returns>本地化文本或 key 原文。</returns>
        public string GetText(string key)
        {
            ValidateText(key, nameof(key), "Localization key cannot be empty.");
            if (TryGetPackText(CurrentLocale, key, out var currentText))
            {
                return currentText;
            }

            RecordMissing(CurrentLocale, key);
            if (TryGetPackText(FallbackLocale, key, out var fallbackText))
            {
                return fallbackText;
            }

            RecordMissing(FallbackLocale, key);
            return key;
        }

        /// <summary>
        /// 获取本地化文本并执行 string.Format 格式化。
        /// </summary>
        /// <param name="key">本地化 key。</param>
        /// <param name="args">格式化参数。</param>
        /// <returns>格式化后的文本。</returns>
        public string Format(string key, params object[] args)
        {
            try
            {
                return string.Format(GetText(key), args ?? Array.Empty<object>());
            }
            catch (FormatException exception)
            {
                throw new GameException($"Failed to format localized text: {key}", exception);
            }
        }

        /// <summary>
        /// 获取本地化模块快照。
        /// </summary>
        /// <returns>本地化模块快照。</returns>
        public LocalizationSnapshot Snapshot()
        {
            return new LocalizationSnapshot(
                CurrentLocale,
                FallbackLocale,
                new List<string>(m_Packs.Keys),
                new List<MissingLocalizationEntry>(m_MissingEntries));
        }

        /// <summary>
        /// 校验文本参数。
        /// </summary>
        /// <param name="value">文本值。</param>
        /// <param name="parameterName">参数名。</param>
        /// <param name="emptyMessage">空白文本异常消息。</param>
        private static void ValidateText(string value, string parameterName, string emptyMessage)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(emptyMessage, parameterName);
            }
        }

        /// <summary>
        /// 尝试从指定 locale 的语言包获取文本。
        /// </summary>
        /// <param name="locale">locale。</param>
        /// <param name="key">本地化 key。</param>
        /// <param name="text">本地化文本。</param>
        /// <returns>找到文本时返回 true。</returns>
        private bool TryGetPackText(string locale, string key, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(locale))
            {
                return false;
            }

            return m_Packs.TryGetValue(locale, out var pack) && pack.TryGetText(key, out text);
        }

        /// <summary>
        /// 记录缺失本地化条目。
        /// </summary>
        /// <param name="locale">locale。</param>
        /// <param name="key">本地化 key。</param>
        private void RecordMissing(string locale, string key)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return;
            }

            m_MissingEntries.Add(new MissingLocalizationEntry(locale, key));
        }

        /// <summary>
        /// 释放所有语言包。
        /// </summary>
        private void ReleasePacks()
        {
            foreach (var pack in m_Packs.Values)
            {
                pack.Release();
            }

            m_Packs.Clear();
        }

        /// <summary>
        /// 执行语言包资源加载流程。
        /// </summary>
        /// <param name="locale">语言包 locale。</param>
        /// <param name="location">资源位置。</param>
        /// <returns>加载完成的语言包。</returns>
        private async UniTask<LocalizationPack> LoadPackInternalAsync(string locale, string location)
        {
            ValidateText(locale, nameof(locale), "Locale cannot be empty.");
            ValidateText(location, nameof(location), "Location cannot be empty.");

            RawAssetHandle handle = null;
            try
            {
                handle = await App.Resource.LoadRawAssetAsync(location);
                if (handle == null || handle.Status is not ResourceStatus.Succeeded)
                {
                    throw new GameException($"Localization pack load failed: {location}", handle?.Error);
                }

                var pack = ParsePack(locale, location, handle.GetString());
                RegisterPack(pack);
                return pack;
            }
            catch (Exception exception) when (exception is not ArgumentNullException && exception is not ArgumentException)
            {
                if (exception is GameException)
                {
                    throw;
                }

                throw new GameException($"Failed to load localization pack '{locale}' from '{location}'.", exception);
            }
            finally
            {
                if (handle != null && handle.Info != null)
                {
                    await App.Resource.UnloadRawAsset(handle);
                }
            }
        }

        /// <summary>
        /// 解析本地化语言包 JSON。
        /// </summary>
        /// <param name="locale">语言包 locale。</param>
        /// <param name="location">资源位置。</param>
        /// <param name="json">JSON 文本。</param>
        /// <returns>本地化语言包。</returns>
        private static LocalizationPack ParsePack(string locale, string location, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new GameException($"Localization pack JSON is empty: {location}");
            }

            try
            {
                var root = JToken.Parse(json, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (root.Type != JTokenType.Object)
                {
                    throw new GameException($"Localization pack JSON must be an object: {location}");
                }

                var rootObject = (JObject)root;
                var entriesToken = rootObject["entries"];
                if (entriesToken != null && entriesToken is not JObject)
                {
                    throw new GameException($"Localization pack entries must be an object: {location}");
                }

                var entriesObject = entriesToken is JObject wrapperEntries ? wrapperEntries : rootObject;

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
                throw new GameException($"Failed to parse localization pack JSON from '{location}'.", exception);
            }
        }
    }
}

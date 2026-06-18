using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.Localization
{
    public sealed class LocalizationModule : GameModuleBase
    {
        private readonly Dictionary<string, LocalizationPack> m_Packs = new Dictionary<string, LocalizationPack>(StringComparer.Ordinal);
        private readonly HashSet<MissingLocalizationEntry> m_MissingEntries = new HashSet<MissingLocalizationEntry>();

        public event Action<LocalizationChangedEventArgs> LocaleChanged;

        public string CurrentLocale { get; private set; }

        public string FallbackLocale { get; private set; }

        public override void Startup()
        {
            m_Packs.Clear();
            m_MissingEntries.Clear();
            CurrentLocale = null;
            FallbackLocale = null;
        }

        public override void Shutdown()
        {
            foreach (var pack in m_Packs.Values)
            {
                pack.Release();
            }

            m_Packs.Clear();
            m_MissingEntries.Clear();
            CurrentLocale = null;
            FallbackLocale = null;
            LocaleChanged = null;
        }

        public void SetFallbackLocale(string locale)
        {
            ValidateText(locale, nameof(locale), "Locale cannot be empty.");
            FallbackLocale = locale;
        }

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

        public UniTask<LocalizationPack> LoadPackAsync(string locale, string location)
        {
            return LoadPackInternalAsync(locale, location);
        }

        public bool HasText(string key)
        {
            ValidateText(key, nameof(key), "Localization key cannot be empty.");
            if (TryGetPackText(CurrentLocale, key, out _))
            {
                return true;
            }

            return TryGetPackText(FallbackLocale, key, out _);
        }

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

        public LocalizationSnapshot Snapshot()
        {
            return new LocalizationSnapshot(
                CurrentLocale,
                FallbackLocale,
                new List<string>(m_Packs.Keys),
                new List<MissingLocalizationEntry>(m_MissingEntries));
        }

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

        private bool TryGetPackText(string locale, string key, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(locale))
            {
                return false;
            }

            return m_Packs.TryGetValue(locale, out var pack) && pack.TryGetText(key, out text);
        }

        private void RecordMissing(string locale, string key)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return;
            }

            m_MissingEntries.Add(new MissingLocalizationEntry(locale, key));
        }

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

        private static LocalizationPack ParsePack(string locale, string location, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new GameException($"Localization pack JSON is empty: {location}");
            }

            try
            {
                var root = JToken.Parse(json);
                if (root.Type != JTokenType.Object)
                {
                    throw new GameException($"Localization pack JSON must be an object: {location}");
                }

                var rootObject = (JObject)root;
                var entriesToken = rootObject["entries"];
                var entriesObject = entriesToken == null ? rootObject : entriesToken as JObject;
                if (entriesObject == null)
                {
                    throw new GameException($"Localization pack entries must be an object: {location}");
                }

                var packLocale = locale;
                if (entriesToken != null && rootObject.TryGetValue("locale", out var localeToken) && localeToken.Type != JTokenType.Null)
                {
                    var jsonLocale = localeToken.Value<string>();
                    if (!string.IsNullOrWhiteSpace(jsonLocale))
                    {
                        packLocale = jsonLocale;
                    }
                }

                var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in entriesObject.Properties())
                {
                    if (string.Equals(property.Name, "locale", StringComparison.Ordinal) ||
                        string.Equals(property.Name, "entries", StringComparison.Ordinal))
                    {
                        if (entriesToken == null)
                        {
                            entries.Add(property.Name, property.Value.Type == JTokenType.Null ? string.Empty : property.Value.ToString());
                        }

                        continue;
                    }

                    if (property.Value.Type == JTokenType.Object || property.Value.Type == JTokenType.Array)
                    {
                        throw new GameException($"Localization value must be scalar: {property.Name}");
                    }

                    entries.Add(property.Name, property.Value.Type == JTokenType.Null ? string.Empty : property.Value.ToString());
                }

                return new LocalizationPack(packLocale, entries);
            }
            catch (Exception exception) when (exception is not GameException)
            {
                throw new GameException($"Failed to parse localization pack JSON from '{location}'.", exception);
            }
        }
    }
}

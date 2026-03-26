using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 本地化模块，提供多语言文本和资源本地化支持。
    /// 支持语言包加载、文本格式化和资源路径本地化。
    /// </summary>
    public sealed partial class LocalizationModule : IGameFrameworkLifecycleModule
    {
        /// <summary>
        /// 语言切换时广播的事件名称。
        /// </summary>
        public const string LanguageChangedEventName = "GameDeveloperKit.Localization.LanguageChanged";

        private const string LanguagePreferenceKey = "GameDeveloperKit/Localization/CurrentLanguage";
        private readonly Dictionary<string, Dictionary<string, string>> _texts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, ResourceLocation>> _assets = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Action<string>> _refreshListeners = new();
        private GameFrameworkModuleStatus _status = GameFrameworkModuleStatus.Created;
        private bool _diagnosticsRegistered;

        /// <summary>
        /// 初始化 LocalizationModule 的新实例。
        /// </summary>
        public LocalizationModule()
        {
            CurrentLanguage = NormalizeLanguage(null);
            FallbackLanguage = CurrentLanguage;
        }

        /// <summary>
        /// 获取或设置当前语言。
        /// </summary>
        public string CurrentLanguage { get; private set; }

        /// <summary>
        /// 获取或设置回退语言。
        /// </summary>
        public string FallbackLanguage { get; private set; }

        /// <summary>
        /// 获取已注册的语言数量。
        /// </summary>
        public int LanguageCount => _texts.Count;

        /// <summary>
        /// 获取刷新监听器数量。
        /// </summary>
        public int RefreshListenerCount => _refreshListeners.Count;

        /// <summary>
        /// 获取模块状态。
        /// </summary>
        public GameFrameworkModuleStatus Status => _status;

        /// <summary>
        /// 当语言改变时触发。
        /// </summary>
        public event Action<string> LanguageChanged;

        /// <summary>
        /// 异步初始化本地化模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>初始化任务。</returns>
        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterInitialization(nameof(LocalizationModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            try
            {
                Game.EnsureModuleReady<DataModule>();
                CurrentLanguage = LoadSavedLanguage();
                FallbackLanguage = NormalizeLanguage(FallbackLanguage);
                RegisterDiagnosticsSnapshotProviders();
                GameFrameworkModuleLifecycleUtility.CompleteInitialization(ref _status);
                return UniTask.CompletedTask;
            }
            catch
            {
                GameFrameworkModuleLifecycleUtility.FailInitialization(ref _status);
                throw;
            }
        }

        /// <summary>
        /// 异步关闭本地化模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>关闭任务。</returns>
        public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterShutdown(nameof(LocalizationModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 设置回退语言。
        /// </summary>
        /// <param name="language">语言代码。</param>
        public void SetFallbackLanguage(string language)
        {
            FallbackLanguage = NormalizeLanguage(language);
        }

        /// <summary>
        /// 设置当前语言。
        /// </summary>
        /// <param name="language">系统语言。</param>
        /// <param name="persist">是否持久化保存。</param>
        /// <returns>如果语言改变则返回 true，否则返回 false。</returns>
        public bool SetLanguage(SystemLanguage language, bool persist = true)
        {
            return SetLanguage(language.ToString(), persist);
        }

        /// <summary>
        /// 设置当前语言。
        /// </summary>
        /// <param name="language">语言代码。</param>
        /// <param name="persist">是否持久化保存。</param>
        /// <returns>如果语言改变则返回 true，否则返回 false。</returns>
        public bool SetLanguage(string language, bool persist = true)
        {
            var normalized = NormalizeLanguage(language);
            if (string.Equals(CurrentLanguage, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            CurrentLanguage = normalized;

            if (persist)
            {
                SaveLanguagePreference(CurrentLanguage);
            }

            LanguageChanged?.Invoke(CurrentLanguage);
            NotifyRefreshListeners();
            if (Game.TryGetModule<EventModule>(out var eventModule))
            {
                eventModule.Raise(LanguageChangedEventName, this, CurrentLanguage);
            }

            return true;
        }

        /// <summary>
        /// 注册本地化文本。
        /// </summary>
        /// <param name="language">语言代码。</param>
        /// <param name="key">文本键。</param>
        /// <param name="value">文本值。</param>
        /// <exception cref="ArgumentException">当文本键为空时抛出。</exception>
        public void RegisterText(string language, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Text key can not be empty.", nameof(key));
            }

            GetOrCreateTexts(language)[key] = value ?? string.Empty;
        }

        /// <summary>
        /// 批量注册本地化文本。
        /// </summary>
        /// <param name="language">语言代码。</param>
        /// <param name="entries">文本条目集合。</param>
        /// <param name="clearExisting">是否清除现有文本。</param>
        /// <exception cref="ArgumentNullException">当条目集合为 null 时抛出。</exception>
        public void RegisterTexts(string language, IEnumerable<KeyValuePair<string, string>> entries, bool clearExisting = false)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var table = GetOrCreateTexts(language);
            if (clearExisting)
            {
                table.Clear();
            }

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                table[entry.Key] = entry.Value ?? string.Empty;
            }
        }

        /// <summary>
        /// 注册本地化资源位置。
        /// </summary>
        /// <param name="language">语言代码。</param>
        /// <param name="key">资源键。</param>
        /// <param name="location">资源位置。</param>
        /// <exception cref="ArgumentException">当资源键为空时抛出。</exception>
        /// <exception cref="ArgumentNullException">当资源位置为 null 时抛出。</exception>
        public void RegisterAsset(string language, string key, ResourceLocation location)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Asset key can not be empty.", nameof(key));
            }

            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            GetOrCreateAssets(language)[key] = location.Clone();
        }

        /// <summary>
        /// 检查是否存在指定语言的数据。
        /// </summary>
        /// <param name="language">语言代码。</param>
        /// <returns>如果存在则返回 true，否则返回 false。</returns>
        public bool HasLanguage(string language)
        {
            return _texts.ContainsKey(NormalizeLanguage(language)) || _assets.ContainsKey(NormalizeLanguage(language));
        }

        /// <summary>
        /// 尝试获取本地化文本。
        /// </summary>
        /// <param name="key">文本键。</param>
        /// <param name="value">输出文本值。</param>
        /// <returns>如果获取成功则返回 true，否则返回 false。</returns>
        public bool TryGetText(string key, out string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = null;
                return false;
            }

            if (TryGetText(CurrentLanguage, key, out value))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(FallbackLanguage)
                && !string.Equals(CurrentLanguage, FallbackLanguage, StringComparison.OrdinalIgnoreCase)
                && TryGetText(FallbackLanguage, key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// 获取本地化文本。
        /// </summary>
        /// <param name="key">文本键。</param>
        /// <param name="fallbackValue">回退值（当文本不存在时）。</param>
        /// <returns>本地化文本。</returns>
        public string GetText(string key, string fallbackValue = null)
        {
            return TryGetText(key, out var value) ? value : fallbackValue ?? key;
        }

        /// <summary>
        /// 获取格式化的本地化文本。
        /// </summary>
        /// <param name="key">文本键。</param>
        /// <param name="formatArgs">格式化参数。</param>
        /// <returns>格式化的本地化文本。</returns>
        public string LocalizeText(string key, params object[] formatArgs)
        {
            var value = GetText(key);
            if (formatArgs == null || formatArgs.Length == 0)
            {
                return value;
            }

            return string.Format(value, formatArgs);
        }

        /// <summary>
        /// 尝试获取本地化资源位置。
        /// </summary>
        /// <param name="key">资源键。</param>
        /// <param name="location">输出资源位置。</param>
        /// <returns>如果获取成功则返回 true，否则返回 false。</returns>
        public bool TryGetAssetLocation(string key, out ResourceLocation location)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                location = null;
                return false;
            }

            if (TryGetAssetLocation(CurrentLanguage, key, out location))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(FallbackLanguage)
                && !string.Equals(CurrentLanguage, FallbackLanguage, StringComparison.OrdinalIgnoreCase)
                && TryGetAssetLocation(FallbackLanguage, key, out location))
            {
                return true;
            }

            location = null;
            return false;
        }

        /// <summary>
        /// 获取本地化资源位置。
        /// </summary>
        /// <param name="key">资源键。</param>
        /// <returns>资源位置。</returns>
        /// <exception cref="InvalidOperationException">当资源不存在时抛出。</exception>
        public ResourceLocation GetAssetLocation(string key)
        {
            if (!TryGetAssetLocation(key, out var location))
            {
                throw new InvalidOperationException($"Localized asset '{key}' is not registered.");
            }

            return location;
        }

        /// <summary>
        /// 获取本地化资源位置（别名）。
        /// </summary>
        /// <param name="key">资源键。</param>
        /// <returns>资源位置。</returns>
        public ResourceLocation LocalizeAsset(string key)
        {
            return GetAssetLocation(key);
        }

        /// <summary>
        /// 异步加载本地化资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <param name="key">资源键。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>加载的资源。</returns>
        /// <exception cref="InvalidOperationException">当资源模块不可用时抛出。</exception>
        public async UniTask<TAsset> LoadLocalizedAssetAsync<TAsset>(string key, CancellationToken cancellationToken = default)
            where TAsset : UnityEngine.Object
        {
            var location = GetAssetLocation(key);
            if (!Game.HasModule<ResourceModule>())
            {
                throw new InvalidOperationException("Resource module is not available.");
            }

            var assetName = !string.IsNullOrWhiteSpace(location.FullPath) ? location.FullPath : location.Name;
            var handle = await Game.Resource.Provider.LoadAssetAsync<TAsset>(assetName, cancellationToken);
            return handle.GetAsset<TAsset>();
        }

        /// <summary>
        /// 加载语言包。
        /// </summary>
        /// <param name="pack">语言包数据。</param>
        /// <param name="clearExisting">是否清除现有数据。</param>
        /// <exception cref="ArgumentNullException">当语言包为 null 时抛出。</exception>
        public void LoadLanguagePack(LocalizationLanguagePack pack, bool clearExisting = false)
        {
            if (pack == null)
            {
                throw new ArgumentNullException(nameof(pack));
            }

            var language = NormalizeLanguage(pack.Language);
            if (clearExisting)
            {
                _texts.Remove(language);
                _assets.Remove(language);
            }

            if (pack.Texts != null)
            {
                var table = GetOrCreateTexts(language);
                for (var i = 0; i < pack.Texts.Length; i++)
                {
                    var entry = pack.Texts[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    table[entry.Key] = entry.Value ?? string.Empty;
                }
            }

            if (pack.Assets != null)
            {
                var assets = GetOrCreateAssets(language);
                for (var i = 0; i < pack.Assets.Length; i++)
                {
                    var entry = pack.Assets[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    assets[entry.Key] = new ResourceLocation
                    {
                        Name = entry.Name,
                        FullPath = entry.FullPath
                    };
                }
            }
        }

        /// <summary>
        /// 从 JSON 加载语言包。
        /// </summary>
        /// <param name="json">JSON 字符串。</param>
        /// <param name="clearExisting">是否清除现有数据。</param>
        /// <exception cref="ArgumentException">当 JSON 为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">当反序列化失败时抛出。</exception>
        public void LoadLanguagePackJson(string json, bool clearExisting = false)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Json can not be empty.", nameof(json));
            }

            var pack = JsonUtility.FromJson<LocalizationLanguagePack>(json);
            if (pack == null)
            {
                throw new InvalidOperationException("Failed to deserialize localization language pack.");
            }

            LoadLanguagePack(pack, clearExisting);
        }

        /// <summary>
        /// 从 TextAsset 加载语言包。
        /// </summary>
        /// <param name="textAsset">文本资源。</param>
        /// <param name="clearExisting">是否清除现有数据。</param>
        /// <exception cref="ArgumentNullException">当文本资源为 null 时抛出。</exception>
        public void LoadLanguagePackTextAsset(TextAsset textAsset, bool clearExisting = false)
        {
            if (textAsset == null)
            {
                throw new ArgumentNullException(nameof(textAsset));
            }

            LoadLanguagePackJson(textAsset.text, clearExisting);
        }

        /// <summary>
        /// 移除指定语言的数据。
        /// </summary>
        /// <param name="language">语言代码。</param>
        /// <returns>如果移除成功则返回 true，否则返回 false。</returns>
        public bool RemoveLanguage(string language)
        {
            var normalized = NormalizeLanguage(language);
            var removedText = _texts.Remove(normalized);
            var removedAssets = _assets.Remove(normalized);
            return removedText || removedAssets;
        }

        /// <summary>
        /// 清除所有本地化数据。
        /// </summary>
        public void Clear()
        {
            _texts.Clear();
            _assets.Clear();
        }

        /// <summary>
        /// 释放本地化模块资源。
        /// </summary>
        public void Dispose()
        {
            Clear();
            _refreshListeners.Clear();
            LanguageChanged = null;
            RemoveDiagnosticsSnapshotProviders();
            _status = GameFrameworkModuleStatus.Disposed;
        }

        /// <summary>
        /// 注册语言刷新监听器。
        /// </summary>
        /// <param name="listener">监听器回调。</param>
        /// <returns>监听器注册令牌。</returns>
        /// <exception cref="ArgumentNullException">当监听器为 null 时抛出。</exception>
        public IDisposable RegisterRefreshListener(Action<string> listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            if (!_refreshListeners.Contains(listener))
            {
                _refreshListeners.Add(listener);
            }

            return new RefreshListenerRegistration(this, listener);
        }

        private bool TryGetText(string language, string key, out string value)
        {
            if (_texts.TryGetValue(NormalizeLanguage(language), out var table))
            {
                return table.TryGetValue(key, out value);
            }

            value = null;
            return false;
        }

        private bool TryGetAssetLocation(string language, string key, out ResourceLocation location)
        {
            if (_assets.TryGetValue(NormalizeLanguage(language), out var table)
                && table.TryGetValue(key, out var resourceLocation))
            {
                location = resourceLocation.Clone();
                return true;
            }

            location = null;
            return false;
        }

        private Dictionary<string, string> GetOrCreateTexts(string language)
        {
            var normalized = NormalizeLanguage(language);
            if (!_texts.TryGetValue(normalized, out var table))
            {
                table = new Dictionary<string, string>(StringComparer.Ordinal);
                _texts.Add(normalized, table);
            }

            return table;
        }

        private Dictionary<string, ResourceLocation> GetOrCreateAssets(string language)
        {
            var normalized = NormalizeLanguage(language);
            if (!_assets.TryGetValue(normalized, out var table))
            {
                table = new Dictionary<string, ResourceLocation>(StringComparer.Ordinal);
                _assets.Add(normalized, table);
            }

            return table;
        }

        private void UnregisterRefreshListener(Action<string> listener)
        {
            _refreshListeners.Remove(listener);
        }

        private void NotifyRefreshListeners()
        {
            for (var i = 0; i < _refreshListeners.Count; i++)
            {
                _refreshListeners[i]?.Invoke(CurrentLanguage);
            }
        }

        private void RegisterDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Localization.CurrentLanguage", () => CurrentLanguage ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Localization.FallbackLanguage", () => FallbackLanguage ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Localization.LanguageCount", () => LanguageCount.ToString());
            diagnostics.RegisterSnapshotProvider("Localization.RefreshListenerCount", () => RefreshListenerCount.ToString());
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Localization.CurrentLanguage");
            diagnostics.RemoveSnapshotProvider("Localization.FallbackLanguage");
            diagnostics.RemoveSnapshotProvider("Localization.LanguageCount");
            diagnostics.RemoveSnapshotProvider("Localization.RefreshListenerCount");
            _diagnosticsRegistered = false;
        }

        private static string NormalizeLanguage(string language)
        {
            return string.IsNullOrWhiteSpace(language) ? Application.systemLanguage.ToString() : language.Trim();
        }

        private static string LoadSavedLanguage()
        {
            try
            {
                var preference = Game.Data.LoadConfigJson("Localization/Preference", true, new LocalizationPreferenceData());
                if (!string.IsNullOrWhiteSpace(preference?.CurrentLanguage))
                {
                    return NormalizeLanguage(preference.CurrentLanguage);
                }

                var legacyLanguage = Game.Data.LoadText(LanguagePreferenceKey, string.Empty);
                return NormalizeLanguage(legacyLanguage);
            }
            catch
            {
                return NormalizeLanguage(null);
            }
        }

        private static void SaveLanguagePreference(string language)
        {
            var preference = new LocalizationPreferenceData
            {
                CurrentLanguage = language
            };

            Game.Data.SaveConfigJson("Localization/Preference", preference, true);
            Game.Data.SaveText(LanguagePreferenceKey, language);
        }
    }
}

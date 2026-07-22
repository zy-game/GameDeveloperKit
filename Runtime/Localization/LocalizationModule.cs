using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Localization
{
    public sealed class LocalizationModule : GameModuleBase
    {
        private readonly ILocalizationAssetLoader m_AssetLoader;
        private readonly HashSet<MissingLocalizationEntry> m_MissingEntries =
            new HashSet<MissingLocalizationEntry>();
        private readonly object m_StateGate = new object();

        private LocalizationRuntimeState m_State;
        private long m_RequestVersion;

        public LocalizationModule() : this(new ResourceLocalizationAssetLoader())
        {
        }

        internal LocalizationModule(ILocalizationAssetLoader assetLoader)
        {
            m_AssetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
        }

        public event Action<LocalizationChangedEventArgs> LocaleChanged;

        public string CatalogLocation => m_State?.CatalogLocation;

        public string CurrentLocale => m_State?.CurrentLocale;

        public string FallbackLocale => m_State?.FallbackLocale;

        public override void Startup()
        {
            Interlocked.Increment(ref m_RequestVersion);
            var previous = ExchangeState(null);
            m_MissingEntries.Clear();
            ReleaseState(previous).Forget(UnityEngine.Debug.LogException);
        }

        public override void Shutdown()
        {
            Interlocked.Increment(ref m_RequestVersion);
            var previous = ExchangeState(null);
            m_MissingEntries.Clear();
            LocaleChanged = null;
            ReleaseState(previous).Forget(UnityEngine.Debug.LogException);
        }

        public UniTask InitializeAsync(
            string catalogLocation,
            string locale,
            CancellationToken cancellationToken = default)
        {
            ValidateText(catalogLocation, nameof(catalogLocation), "Catalog location cannot be empty.");
            ValidateText(locale, nameof(locale), "Locale cannot be empty.");
            return SwitchStateAsync(catalogLocation, locale, true, cancellationToken);
        }

        public UniTask SetLocaleAsync(string locale, CancellationToken cancellationToken = default)
        {
            ValidateText(locale, nameof(locale), "Locale cannot be empty.");
            var state = m_State ?? throw new GameException("Localization module is not initialized.");
            if (string.Equals(state.CurrentLocale, locale, StringComparison.OrdinalIgnoreCase))
            {
                return UniTask.CompletedTask;
            }

            return SwitchStateAsync(state.CatalogLocation, locale, false, cancellationToken);
        }

        public UniTask ReloadAsync(CancellationToken cancellationToken = default)
        {
            var state = m_State ?? throw new GameException("Localization module is not initialized.");
            return SwitchStateAsync(state.CatalogLocation, state.CurrentLocale, true, cancellationToken);
        }

        public bool HasText(string key)
        {
            ValidateText(key, nameof(key), "Localization key cannot be empty.");
            var state = m_State;
            if (state == null || state.TryGetKeyId(key, out var keyId) is false)
            {
                return false;
            }

            return state.LocaleOrder.Any(locale => state.TryGetText(locale, keyId, out _));
        }

        public string GetText(string key)
        {
            ValidateText(key, nameof(key), "Localization key cannot be empty.");
            var state = m_State;
            if (state == null)
            {
                return key;
            }

            if (state.TryGetKeyId(key, out var keyId) is false)
            {
                foreach (var locale in state.LocaleOrder)
                {
                    RecordMissing(locale, key);
                }

                return key;
            }

            foreach (var locale in state.LocaleOrder)
            {
                if (state.TryGetText(locale, keyId, out var text))
                {
                    return text;
                }

                RecordMissing(locale, key);
            }

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
            var state = m_State;
            return new LocalizationSnapshot(
                state?.CurrentLocale,
                state?.FallbackLocale,
                state?.LocaleOrder.ToArray() ?? Array.Empty<string>(),
                new List<MissingLocalizationEntry>(m_MissingEntries));
        }

        private async UniTask SwitchStateAsync(
            string catalogLocation,
            string locale,
            bool notifyWhenLocaleIsUnchanged,
            CancellationToken cancellationToken)
        {
            var requestVersion = Interlocked.Increment(ref m_RequestVersion);
            LocalizationRuntimeState candidate = null;
            try
            {
                candidate = await LoadStateAsync(catalogLocation, locale, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                LocalizationRuntimeState previous;
                lock (m_StateGate)
                {
                    if (requestVersion != Volatile.Read(ref m_RequestVersion))
                    {
                        throw new OperationCanceledException("Localization request was superseded.");
                    }

                    previous = m_State;
                    m_State = candidate;
                    candidate = null;
                    m_MissingEntries.Clear();
                }

                try
                {
                    var previousLocale = previous?.CurrentLocale;
                    if (notifyWhenLocaleIsUnchanged ||
                        string.Equals(previousLocale, m_State.CurrentLocale, StringComparison.OrdinalIgnoreCase) is false)
                    {
                        LocaleChanged?.Invoke(new LocalizationChangedEventArgs(previousLocale, m_State.CurrentLocale));
                    }
                }
                finally
                {
                    await ReleaseState(previous);
                }
            }
            finally
            {
                await ReleaseState(candidate);
            }
        }

        private async UniTask<LocalizationRuntimeState> LoadStateAsync(
            string catalogLocation,
            string locale,
            CancellationToken cancellationToken)
        {
            var leases = new List<LocalizationAssetLease>();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var catalogLease = await m_AssetLoader.LoadAsync(catalogLocation);
                leases.Add(catalogLease);
                cancellationToken.ThrowIfCancellationRequested();
                if (catalogLease.Asset is not LocalizationCatalogAsset catalog)
                {
                    throw new GameException($"Localization catalog asset type is invalid: {catalogLocation}");
                }

                ThrowIfInvalid(LocalizationAssetValidator.ValidateCatalog(catalog), catalogLocation);
                var localeOrder = BuildLocaleOrder(catalog, locale);
                var localeAssets = new Dictionary<string, LocalizationLocaleAsset>(StringComparer.OrdinalIgnoreCase);
                foreach (var localeId in localeOrder)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    catalog.TryGetLocale(localeId, out var descriptor);
                    var localeLease = await m_AssetLoader.LoadAsync(descriptor.ResourceLocation);
                    leases.Add(localeLease);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (localeLease.Asset is not LocalizationLocaleAsset localeAsset)
                    {
                        throw new GameException(
                            $"Localization locale asset type is invalid: {descriptor.ResourceLocation}");
                    }

                    ThrowIfInvalid(
                        LocalizationAssetValidator.ValidateLocale(catalog, localeAsset, localeId),
                        descriptor.ResourceLocation);
                    localeAssets.Add(localeId, localeAsset);
                }

                return new LocalizationRuntimeState(
                    catalogLocation,
                    catalog,
                    localeOrder[0],
                    localeOrder,
                    localeAssets,
                    leases);
            }
            catch (Exception exception) when (exception is not ArgumentNullException &&
                                              exception is not ArgumentException &&
                                              exception is not OperationCanceledException)
            {
                await ReleaseLeases(leases);
                if (exception is GameException)
                {
                    throw;
                }

                throw new GameException(
                    $"Failed to load localization state '{locale}' from '{catalogLocation}'.",
                    exception);
            }
            catch
            {
                await ReleaseLeases(leases);
                throw;
            }
        }

        private LocalizationRuntimeState ExchangeState(LocalizationRuntimeState value)
        {
            lock (m_StateGate)
            {
                var previous = m_State;
                m_State = value;
                return previous;
            }
        }

        private static IReadOnlyList<string> BuildLocaleOrder(LocalizationCatalogAsset catalog, string requestedLocale)
        {
            if (catalog.TryGetLocale(requestedLocale, out _) is false)
            {
                throw new GameException($"Localization locale is not registered: {requestedLocale}");
            }

            var order = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AppendLocaleChain(catalog, requestedLocale, order, visited);
            AppendLocaleChain(catalog, catalog.DefaultLocale, order, visited);
            return order;
        }

        private static void AppendLocaleChain(
            LocalizationCatalogAsset catalog,
            string locale,
            ICollection<string> order,
            ISet<string> visited)
        {
            while (string.IsNullOrWhiteSpace(locale) is false && visited.Add(locale))
            {
                if (catalog.TryGetLocale(locale, out var descriptor) is false)
                {
                    throw new GameException($"Localization fallback locale is not registered: {locale}");
                }

                order.Add(descriptor.Locale);
                locale = descriptor.FallbackLocale;
            }
        }

        private static void ThrowIfInvalid(LocalizationAssetValidationResult result, string location)
        {
            if (result.IsValid)
            {
                return;
            }

            var errors = string.Join(
                ", ",
                result.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == LocalizationAssetDiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.Code));
            throw new GameException($"Localization asset validation failed at '{location}': {errors}");
        }

        private static async UniTask ReleaseLeases(IReadOnlyList<LocalizationAssetLease> leases)
        {
            for (var i = leases.Count - 1; i >= 0; i--)
            {
                await leases[i].ReleaseAsync();
            }
        }

        private static UniTask ReleaseState(LocalizationRuntimeState state)
        {
            return state == null ? UniTask.CompletedTask : state.ReleaseAsync();
        }

        private void RecordMissing(string locale, string key)
        {
            if (string.IsNullOrWhiteSpace(locale) is false)
            {
                m_MissingEntries.Add(new MissingLocalizationEntry(locale, key));
            }
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
    }
}

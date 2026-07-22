using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.Localization
{
    public enum LocalizationAssetDiagnosticSeverity
    {
        Warning,
        Error
    }

    public sealed class LocalizationAssetDiagnostic
    {
        public LocalizationAssetDiagnostic(
            LocalizationAssetDiagnosticSeverity severity,
            string code,
            string message,
            string locale = null,
            long keyId = 0)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Locale = locale ?? string.Empty;
            KeyId = keyId;
        }

        public LocalizationAssetDiagnosticSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public string Locale { get; }

        public long KeyId { get; }
    }

    public sealed class LocalizationAssetValidationResult
    {
        public LocalizationAssetValidationResult(IReadOnlyList<LocalizationAssetDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics ?? Array.Empty<LocalizationAssetDiagnostic>();
        }

        public IReadOnlyList<LocalizationAssetDiagnostic> Diagnostics { get; }

        public bool IsValid => Diagnostics.All(diagnostic =>
            diagnostic.Severity is not LocalizationAssetDiagnosticSeverity.Error);
    }

    public static class LocalizationAssetValidator
    {
        public static LocalizationAssetValidationResult ValidateCatalog(LocalizationCatalogAsset catalog)
        {
            var diagnostics = new List<LocalizationAssetDiagnostic>();
            ValidateCatalog(catalog, diagnostics);
            return new LocalizationAssetValidationResult(diagnostics);
        }

        public static LocalizationAssetValidationResult ValidateLocale(
            LocalizationCatalogAsset catalog,
            LocalizationLocaleAsset localeAsset,
            string expectedLocale = null)
        {
            var diagnostics = new List<LocalizationAssetDiagnostic>();
            var keyIds = ValidateCatalog(catalog, diagnostics);
            ValidateLocaleAsset(catalog, localeAsset, expectedLocale, keyIds, diagnostics);
            return new LocalizationAssetValidationResult(diagnostics);
        }

        public static LocalizationAssetValidationResult Validate(
            LocalizationCatalogAsset catalog,
            IEnumerable<LocalizationLocaleAsset> localeAssets)
        {
            var diagnostics = new List<LocalizationAssetDiagnostic>();
            var keyIds = ValidateCatalog(catalog, diagnostics);
            var assets = localeAssets?.ToArray() ?? Array.Empty<LocalizationLocaleAsset>();
            var assetsByLocale = new Dictionary<string, LocalizationLocaleAsset>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in assets)
            {
                if (asset == null)
                {
                    AddError(diagnostics, "locale_asset_null", "Localization locale asset cannot be null.");
                    continue;
                }

                var locale = asset.Locale?.Trim();
                if (string.IsNullOrEmpty(locale) is false && assetsByLocale.ContainsKey(locale))
                {
                    AddError(
                        diagnostics,
                        "locale_asset_duplicate",
                        $"Localization locale asset is duplicated: {locale}",
                        locale);
                }
                else if (string.IsNullOrEmpty(locale) is false)
                {
                    assetsByLocale.Add(locale, asset);
                }

                ValidateLocaleAsset(catalog, asset, locale, keyIds, diagnostics);
            }

            if (catalog != null)
            {
                foreach (var descriptor in catalog.Locales.Where(descriptor => descriptor != null))
                {
                    if (string.IsNullOrWhiteSpace(descriptor.Locale) is false &&
                        assetsByLocale.ContainsKey(descriptor.Locale) is false)
                    {
                        AddError(
                            diagnostics,
                            "locale_asset_missing",
                            $"Localization locale asset is missing: {descriptor.Locale}",
                            descriptor.Locale);
                    }
                }
            }

            return new LocalizationAssetValidationResult(diagnostics);
        }

        private static HashSet<long> ValidateCatalog(
            LocalizationCatalogAsset catalog,
            ICollection<LocalizationAssetDiagnostic> diagnostics)
        {
            var keyIds = new HashSet<long>();
            if (catalog == null)
            {
                AddError(diagnostics, "catalog_null", "Localization catalog cannot be null.");
                return keyIds;
            }

            if (catalog.SchemaVersion != LocalizationCatalogAsset.CurrentSchemaVersion)
            {
                AddError(
                    diagnostics,
                    "catalog_schema_unsupported",
                    $"Unsupported localization catalog schema: {catalog.SchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(catalog.CatalogId))
            {
                AddError(diagnostics, "catalog_id_empty", "Localization catalog ID cannot be empty.");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in catalog.Keys)
            {
                if (entry == null)
                {
                    AddError(diagnostics, "key_entry_null", "Localization key entry cannot be null.");
                    continue;
                }

                if (entry.Id <= 0 || keyIds.Add(entry.Id) is false)
                {
                    AddError(
                        diagnostics,
                        "key_id_invalid",
                        $"Localization key ID must be positive and unique: {entry.Id}.",
                        keyId: entry.Id);
                }

                var key = entry.Key?.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    AddError(diagnostics, "key_empty", "Localization key cannot be empty.", keyId: entry.Id);
                }
                else if (keys.Add(key) is false)
                {
                    AddError(
                        diagnostics,
                        "key_duplicate",
                        $"Localization key is duplicated: {key}",
                        keyId: entry.Id);
                }
            }

            ValidateLocales(catalog, diagnostics);
            return keyIds;
        }

        private static void ValidateLocales(
            LocalizationCatalogAsset catalog,
            ICollection<LocalizationAssetDiagnostic> diagnostics)
        {
            var descriptors = new Dictionary<string, LocalizationLocaleDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptor in catalog.Locales)
            {
                if (descriptor == null)
                {
                    AddError(diagnostics, "locale_descriptor_null", "Localization locale descriptor cannot be null.");
                    continue;
                }

                var locale = descriptor.Locale?.Trim();
                if (string.IsNullOrEmpty(locale))
                {
                    AddError(diagnostics, "locale_empty", "Localization locale cannot be empty.");
                    continue;
                }

                if (descriptors.ContainsKey(locale))
                {
                    AddError(
                        diagnostics,
                        "locale_duplicate",
                        $"Localization locale is duplicated: {locale}",
                        locale);
                }
                else
                {
                    descriptors.Add(locale, descriptor);
                }

                if (string.IsNullOrWhiteSpace(descriptor.ResourceLocation))
                {
                    AddError(
                        diagnostics,
                        "locale_location_empty",
                        $"Localization resource location cannot be empty: {locale}",
                        locale);
                }
            }

            if (string.IsNullOrWhiteSpace(catalog.DefaultLocale) ||
                descriptors.ContainsKey(catalog.DefaultLocale) is false)
            {
                AddError(
                    diagnostics,
                    "default_locale_invalid",
                    $"Localization default locale is not registered: {catalog.DefaultLocale}",
                    catalog.DefaultLocale);
            }

            foreach (var pair in descriptors)
            {
                var fallback = pair.Value.FallbackLocale?.Trim();
                if (string.IsNullOrEmpty(fallback))
                {
                    continue;
                }

                if (descriptors.ContainsKey(fallback) is false)
                {
                    AddError(
                        diagnostics,
                        "fallback_locale_missing",
                        $"Localization fallback locale is not registered: {pair.Key} -> {fallback}",
                        pair.Key);
                }
            }

            foreach (var locale in descriptors.Keys)
            {
                ValidateFallbackChain(locale, descriptors, diagnostics);
            }
        }

        private static void ValidateFallbackChain(
            string origin,
            IReadOnlyDictionary<string, LocalizationLocaleDescriptor> descriptors,
            ICollection<LocalizationAssetDiagnostic> diagnostics)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = origin;
            while (descriptors.TryGetValue(current, out var descriptor))
            {
                if (visited.Add(current) is false)
                {
                    AddError(
                        diagnostics,
                        "fallback_cycle",
                        $"Localization fallback cycle detected from locale: {origin}",
                        origin);
                    return;
                }

                current = descriptor.FallbackLocale?.Trim();
                if (string.IsNullOrEmpty(current))
                {
                    return;
                }
            }
        }

        private static void ValidateLocaleAsset(
            LocalizationCatalogAsset catalog,
            LocalizationLocaleAsset localeAsset,
            string expectedLocale,
            ISet<long> keyIds,
            ICollection<LocalizationAssetDiagnostic> diagnostics)
        {
            if (localeAsset == null)
            {
                AddError(diagnostics, "locale_asset_null", "Localization locale asset cannot be null.");
                return;
            }

            if (localeAsset.SchemaVersion != LocalizationLocaleAsset.CurrentSchemaVersion)
            {
                AddError(
                    diagnostics,
                    "locale_schema_unsupported",
                    $"Unsupported localization locale schema: {localeAsset.SchemaVersion}.",
                    localeAsset.Locale);
            }

            var locale = localeAsset.Locale?.Trim();
            if (string.IsNullOrEmpty(locale))
            {
                AddError(diagnostics, "locale_asset_locale_empty", "Localization locale asset locale cannot be empty.");
            }
            else
            {
                if (catalog != null && catalog.TryGetLocale(locale, out _) is false)
                {
                    AddError(
                        diagnostics,
                        "locale_asset_unregistered",
                        $"Localization locale asset is not registered in the catalog: {locale}",
                        locale);
                }

                if (string.IsNullOrWhiteSpace(expectedLocale) is false &&
                    string.Equals(locale, expectedLocale, StringComparison.OrdinalIgnoreCase) is false)
                {
                    AddError(
                        diagnostics,
                        "locale_asset_mismatch",
                        $"Localization locale asset mismatch. Expected {expectedLocale}, actual {locale}.",
                        locale);
                }
            }

            var entryIds = new HashSet<long>();
            foreach (var entry in localeAsset.Entries)
            {
                if (entry == null)
                {
                    AddError(
                        diagnostics,
                        "locale_value_null",
                        $"Localization value entry cannot be null: {locale}",
                        locale);
                    continue;
                }

                if (entry.KeyId <= 0 || entryIds.Add(entry.KeyId) is false)
                {
                    AddError(
                        diagnostics,
                        "locale_key_id_invalid",
                        $"Localization locale KeyId must be positive and unique: {entry.KeyId}.",
                        locale,
                        entry.KeyId);
                }

                if (keyIds.Contains(entry.KeyId) is false)
                {
                    AddError(
                        diagnostics,
                        "locale_key_id_unknown",
                        $"Localization locale references an unknown KeyId: {entry.KeyId}.",
                        locale,
                        entry.KeyId);
                }
            }
        }

        private static void AddError(
            ICollection<LocalizationAssetDiagnostic> diagnostics,
            string code,
            string message,
            string locale = null,
            long keyId = 0)
        {
            diagnostics.Add(new LocalizationAssetDiagnostic(
                LocalizationAssetDiagnosticSeverity.Error,
                code,
                message,
                locale,
                keyId));
        }
    }
}

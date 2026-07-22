using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameDeveloperKit.Localization;

namespace GameDeveloperKit.LocalizationEditor
{
    public enum LocalizationAuthoringDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class LocalizationAuthoringDiagnostic
    {
        public LocalizationAuthoringDiagnostic(
            LocalizationAuthoringDiagnosticSeverity severity,
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

        public LocalizationAuthoringDiagnosticSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public string Locale { get; }

        public long KeyId { get; }
    }

    public sealed class LocalizationAuthoringLocale
    {
        private readonly IReadOnlyDictionary<long, string> m_Values;

        public LocalizationAuthoringLocale(
            LocalizationLocaleDescriptor descriptor,
            LocalizationLocaleAsset asset,
            string assetPath)
        {
            Descriptor = descriptor;
            Asset = asset;
            AssetPath = assetPath ?? string.Empty;
            m_Values = new ReadOnlyDictionary<long, string>(
                (asset?.Entries ?? Array.Empty<LocalizationValueEntry>())
                .Where(entry => entry != null)
                .GroupBy(entry => entry.KeyId)
                .ToDictionary(group => group.Key, group => group.First().Value));
        }

        public LocalizationLocaleDescriptor Descriptor { get; }

        public LocalizationLocaleAsset Asset { get; }

        public string AssetPath { get; }

        public IReadOnlyDictionary<long, string> Values => m_Values;

        public bool TryGetValue(long keyId, out string value)
        {
            return m_Values.TryGetValue(keyId, out value);
        }
    }

    public sealed class LocalizationAuthoringEntry
    {
        public LocalizationAuthoringEntry(long keyId, string key)
        {
            KeyId = keyId;
            Key = key ?? string.Empty;
        }

        public long KeyId { get; }

        public string Key { get; }
    }

    public sealed class LocalizationAuthoringSnapshot
    {
        private readonly IReadOnlyDictionary<string, LocalizationAuthoringLocale> m_Locales;

        public LocalizationAuthoringSnapshot(
            long revision,
            LocalizationCatalogAsset catalog,
            string catalogPath,
            string previewLocale,
            IEnumerable<LocalizationAuthoringLocale> locales,
            IEnumerable<LocalizationAuthoringDiagnostic> diagnostics)
        {
            Revision = revision;
            Catalog = catalog;
            CatalogPath = catalogPath ?? string.Empty;
            PreviewLocale = previewLocale ?? string.Empty;
            Entries = (catalog?.Keys ?? Array.Empty<LocalizationKeyEntry>())
                .Where(entry => entry != null)
                .Select(entry => new LocalizationAuthoringEntry(entry.Id, entry.Key))
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToArray();
            m_Locales = new ReadOnlyDictionary<string, LocalizationAuthoringLocale>(
                (locales ?? Array.Empty<LocalizationAuthoringLocale>())
                .Where(locale => locale?.Descriptor != null)
                .GroupBy(locale => locale.Descriptor.Locale, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase));
            Diagnostics = (diagnostics ?? Array.Empty<LocalizationAuthoringDiagnostic>()).ToArray();
        }

        public long Revision { get; }

        public LocalizationCatalogAsset Catalog { get; }

        public string CatalogPath { get; }

        public string PreviewLocale { get; }

        public IReadOnlyList<LocalizationAuthoringEntry> Entries { get; }

        public IReadOnlyDictionary<string, LocalizationAuthoringLocale> Locales => m_Locales;

        public IReadOnlyList<LocalizationAuthoringDiagnostic> Diagnostics { get; }

        public bool IsValid => Catalog != null &&
                               Diagnostics.All(item => item.Severity != LocalizationAuthoringDiagnosticSeverity.Error);

        public bool TryGetLocale(string locale, out LocalizationAuthoringLocale value)
        {
            return m_Locales.TryGetValue(locale ?? string.Empty, out value);
        }

        public bool TryGetText(long keyId, string locale, out string value)
        {
            value = null;
            return TryGetLocale(locale, out var authoringLocale) &&
                   authoringLocale.TryGetValue(keyId, out value);
        }
    }

    public sealed class LocalizationMutationResult
    {
        private LocalizationMutationResult(bool succeeded, string message, long keyId, LocalizationAuthoringSnapshot snapshot)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            KeyId = keyId;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }

        public string Message { get; }

        public long KeyId { get; }

        public LocalizationAuthoringSnapshot Snapshot { get; }

        public static LocalizationMutationResult Success(
            LocalizationAuthoringSnapshot snapshot,
            string message = null,
            long keyId = 0)
        {
            return new LocalizationMutationResult(true, message, keyId, snapshot);
        }

        public static LocalizationMutationResult Failure(string message, LocalizationAuthoringSnapshot snapshot = null)
        {
            return new LocalizationMutationResult(false, message, 0, snapshot);
        }
    }

    public sealed class LocalizationLocaleDraft
    {
        public LocalizationLocaleDraft(
            string locale,
            string assetPath,
            string resourceLocation,
            string fallbackLocale = null)
        {
            Locale = locale;
            AssetPath = assetPath;
            ResourceLocation = resourceLocation;
            FallbackLocale = fallbackLocale;
        }

        public string Locale { get; }

        public string AssetPath { get; }

        public string ResourceLocation { get; }

        public string FallbackLocale { get; }
    }
}

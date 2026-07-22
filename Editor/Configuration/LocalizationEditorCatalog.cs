using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameDeveloperKit.LocalizationEditor
{
    public interface ILocalizationEditorCatalog
    {
        LocalizationCatalogSnapshot Refresh();

        bool TryGetText(string key, out string text);

        bool TryGetText(string key, string locale, out string text);

        IReadOnlyList<LocalizationSearchResult> Search(string query, int limit = 100);

        IReadOnlyList<LocalizationSearchResult> Search(string query, string previewLocale, int limit = 100);
    }

    public enum LocalizationCatalogDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class LocalizationCatalogDiagnostic
    {
        public LocalizationCatalogDiagnostic(
            LocalizationCatalogDiagnosticSeverity severity,
            string message,
            string sourceId = null,
            int sourceRow = 0)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            SourceRow = sourceRow;
        }

        public LocalizationCatalogDiagnosticSeverity Severity { get; }

        public string Message { get; }

        public string SourceId { get; }

        public int SourceRow { get; }
    }

    public sealed class LocalizationEditorEntry
    {
        public LocalizationEditorEntry(long keyId, string key, string previewText, bool isMissing)
        {
            KeyId = keyId;
            Key = key ?? string.Empty;
            PreviewText = previewText ?? string.Empty;
            IsMissing = isMissing;
        }

        public long KeyId { get; }

        public string Key { get; }

        public string PreviewText { get; }

        public bool IsMissing { get; }

        public bool IsEmpty => IsMissing is false && PreviewText.Length == 0;
    }

    public sealed class LocalizationSearchResult
    {
        public LocalizationSearchResult(long keyId, string key, string text, bool isMissing)
        {
            KeyId = keyId;
            Key = key ?? string.Empty;
            Text = text ?? string.Empty;
            IsMissing = isMissing;
        }

        public long KeyId { get; }

        public string Key { get; }

        public string Text { get; }

        public bool IsMissing { get; }

        public bool IsEmpty => IsMissing is false && Text.Length == 0;
    }

    public sealed class LocalizationCatalogSnapshot
    {
        public LocalizationCatalogSnapshot(
            long sourceRevision,
            string previewLocale,
            IDictionary<string, LocalizationEditorEntry> entries,
            IReadOnlyList<LocalizationCatalogDiagnostic> diagnostics)
        {
            SourceRevision = sourceRevision;
            PreviewLocale = previewLocale ?? string.Empty;
            Entries = new ReadOnlyDictionary<string, LocalizationEditorEntry>(
                new Dictionary<string, LocalizationEditorEntry>(
                    entries ?? new Dictionary<string, LocalizationEditorEntry>(),
                    StringComparer.Ordinal));
            Diagnostics = (diagnostics ?? Array.Empty<LocalizationCatalogDiagnostic>()).ToArray();
        }

        public long SourceRevision { get; }

        public string PreviewLocale { get; }

        public string PreviewField => PreviewLocale;

        public IReadOnlyDictionary<string, LocalizationEditorEntry> Entries { get; }

        public IReadOnlyList<LocalizationCatalogDiagnostic> Diagnostics { get; }
    }

    public sealed class LocalizationEditorCatalog : ILocalizationEditorCatalog
    {
        private readonly ILocalizationAuthoringService m_AuthoringService;
        private LocalizationCatalogSnapshot m_Current;
        private LocalizationAuthoringSnapshot m_AuthoringSnapshot;

        public LocalizationEditorCatalog(ILocalizationAuthoringService authoringService)
        {
            m_AuthoringService = authoringService ?? throw new ArgumentNullException(nameof(authoringService));
        }

        public static LocalizationEditorCatalog Shared { get; } = new LocalizationEditorCatalog(
            LocalizationAuthoringService.Shared);

        public LocalizationCatalogSnapshot Refresh()
        {
            m_AuthoringSnapshot = m_AuthoringService.Refresh();
            var diagnostics = m_AuthoringSnapshot.Diagnostics.Select(diagnostic =>
                new LocalizationCatalogDiagnostic(
                    diagnostic.Severity switch
                    {
                        LocalizationAuthoringDiagnosticSeverity.Info => LocalizationCatalogDiagnosticSeverity.Info,
                        LocalizationAuthoringDiagnosticSeverity.Warning => LocalizationCatalogDiagnosticSeverity.Warning,
                        _ => LocalizationCatalogDiagnosticSeverity.Error
                    },
                    diagnostic.Message,
                    m_AuthoringSnapshot.CatalogPath)).ToArray();
            m_Current = new LocalizationCatalogSnapshot(
                m_AuthoringSnapshot.Revision,
                m_AuthoringSnapshot.PreviewLocale,
                BuildEntries(m_AuthoringSnapshot, m_AuthoringSnapshot.PreviewLocale),
                diagnostics);
            return m_Current;
        }

        public bool TryGetText(string key, out string text)
        {
            var snapshot = m_AuthoringSnapshot ?? m_AuthoringService.Refresh();
            return TryGetText(snapshot, key, snapshot.PreviewLocale, out text);
        }

        public bool TryGetText(string key, string locale, out string text)
        {
            return TryGetText(m_AuthoringSnapshot ?? m_AuthoringService.Refresh(), key, locale, out text);
        }

        public IReadOnlyList<LocalizationSearchResult> Search(string query, int limit = 100)
        {
            var snapshot = m_AuthoringSnapshot ?? m_AuthoringService.Refresh();
            return Search(query, snapshot.PreviewLocale, limit);
        }

        public IReadOnlyList<LocalizationSearchResult> Search(string query, string previewLocale, int limit = 100)
        {
            if (limit <= 0)
            {
                return Array.Empty<LocalizationSearchResult>();
            }

            var snapshot = m_AuthoringSnapshot ?? m_AuthoringService.Refresh();
            var normalizedQuery = query?.Trim() ?? string.Empty;
            return BuildEntries(snapshot, previewLocale).Values
                .Select(entry => new
                {
                    Entry = entry,
                    Rank = MatchRank(entry.Key, entry.PreviewText, normalizedQuery)
                })
                .Where(match => normalizedQuery.Length == 0 || match.Rank < 5)
                .OrderBy(match => match.Rank)
                .ThenBy(match => match.Entry.Key, StringComparer.Ordinal)
                .Take(limit)
                .Select(match => new LocalizationSearchResult(
                    match.Entry.KeyId,
                    match.Entry.Key,
                    match.Entry.PreviewText,
                    match.Entry.IsMissing))
                .ToArray();
        }

        private static bool TryGetText(
            LocalizationAuthoringSnapshot snapshot,
            string key,
            string locale,
            out string text)
        {
            text = null;
            if (snapshot.Catalog == null || string.IsNullOrWhiteSpace(key) ||
                snapshot.Catalog.TryGetKey(key.Trim(), out var entry) is false)
            {
                return false;
            }

            return snapshot.TryGetText(entry.Id, locale, out text);
        }

        private static Dictionary<string, LocalizationEditorEntry> BuildEntries(
            LocalizationAuthoringSnapshot snapshot,
            string previewLocale)
        {
            var entries = new Dictionary<string, LocalizationEditorEntry>(StringComparer.Ordinal);
            foreach (var entry in snapshot.Entries)
            {
                var hasText = snapshot.TryGetText(entry.KeyId, previewLocale, out var text);
                entries.Add(entry.Key, new LocalizationEditorEntry(entry.KeyId, entry.Key, text, hasText is false));
            }

            return entries;
        }

        private static int MatchRank(string key, string text, string query)
        {
            if (query.Length == 0 || string.Equals(key, query, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (key.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2;
            }

            if ((text ?? string.Empty).StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            return (text ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ? 4 : 5;
        }
    }
}

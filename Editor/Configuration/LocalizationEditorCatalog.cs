using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.LubanConfigEditor;

namespace GameDeveloperKit.LocalizationEditor
{
    public interface ILocalizationEditorCatalog
    {
        LocalizationCatalogSnapshot Refresh();

        bool TryGetText(string key, out string text);

        IReadOnlyList<LocalizationSearchResult> Search(string query, int limit = 100);
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
        public LocalizationEditorEntry(string key, int sourceRow, string previewText)
        {
            Key = key ?? string.Empty;
            SourceRow = sourceRow;
            PreviewText = previewText ?? string.Empty;
        }

        public string Key { get; }

        public int SourceRow { get; }

        public string PreviewText { get; }

        public bool IsEmpty => PreviewText.Length == 0;
    }

    public sealed class LocalizationSearchResult
    {
        public LocalizationSearchResult(string key, string text, bool isEmpty)
        {
            Key = key ?? string.Empty;
            Text = text ?? string.Empty;
            IsEmpty = isEmpty;
        }

        public string Key { get; }

        public string Text { get; }

        public bool IsEmpty { get; }
    }

    public sealed class LocalizationCatalogSnapshot
    {
        public LocalizationCatalogSnapshot(
            long sourceRevision,
            string previewField,
            IDictionary<string, LocalizationEditorEntry> entries,
            IReadOnlyList<LocalizationCatalogDiagnostic> diagnostics)
        {
            SourceRevision = sourceRevision;
            PreviewField = previewField ?? string.Empty;
            Entries = new ReadOnlyDictionary<string, LocalizationEditorEntry>(
                new Dictionary<string, LocalizationEditorEntry>(
                    entries ?? new Dictionary<string, LocalizationEditorEntry>(),
                    StringComparer.Ordinal));
            Diagnostics = (diagnostics ?? Array.Empty<LocalizationCatalogDiagnostic>()).ToArray();
        }

        public long SourceRevision { get; }

        public string PreviewField { get; }

        public IReadOnlyDictionary<string, LocalizationEditorEntry> Entries { get; }

        public IReadOnlyList<LocalizationCatalogDiagnostic> Diagnostics { get; }
    }

    public sealed class LocalizationEditorCatalog : ILocalizationEditorCatalog
    {
        private readonly ILubanSourceCatalog m_SourceCatalog;
        private readonly Func<LubanProjectConfig> m_LubanConfigProvider;
        private readonly Func<LocalizationProjectConfig> m_LocalizationConfigProvider;
        private LocalizationCatalogSnapshot m_Current;

        public LocalizationEditorCatalog(
            ILubanSourceCatalog sourceCatalog,
            Func<LubanProjectConfig> lubanConfigProvider,
            Func<LocalizationProjectConfig> localizationConfigProvider)
        {
            m_SourceCatalog = sourceCatalog ?? throw new ArgumentNullException(nameof(sourceCatalog));
            m_LubanConfigProvider = lubanConfigProvider ?? throw new ArgumentNullException(nameof(lubanConfigProvider));
            m_LocalizationConfigProvider = localizationConfigProvider ??
                                           throw new ArgumentNullException(nameof(localizationConfigProvider));
        }

        public static LocalizationEditorCatalog Shared { get; } = new LocalizationEditorCatalog(
            LubanSourceCatalog.Shared,
            () => EditorGlobalConfig.LoadOrCreate().Luban,
            () => EditorGlobalConfig.LoadOrCreate().Localization);

        public LocalizationCatalogSnapshot Refresh()
        {
            var diagnostics = new List<LocalizationCatalogDiagnostic>();
            var sourceRevision = 0L;
            var previewField = string.Empty;
            try
            {
                var lubanConfig = m_LubanConfigProvider() ??
                                  throw new InvalidOperationException("Luban 项目配置不可用。");
                var localizationConfig = m_LocalizationConfigProvider() ??
                                         throw new InvalidOperationException("本地化项目配置不可用。");
                previewField = localizationConfig.PreviewField?.Trim() ?? string.Empty;
                var sourceSnapshot = m_SourceCatalog.Refresh(lubanConfig);
                sourceRevision = sourceSnapshot.Revision;
                var contract = LocalizationTableContractValidator.Validate(
                    sourceSnapshot,
                    m_SourceCatalog,
                    localizationConfig);
                AppendSourceDiagnostics(sourceSnapshot, contract.Table?.SourceId, diagnostics);
                AppendContractDiagnostics(contract, diagnostics);
                if (contract.IsValid is false)
                {
                    return SetCurrent(sourceRevision, previewField, null, diagnostics);
                }

                return SetCurrent(
                    sourceRevision,
                    previewField,
                    BuildEntries(contract.Data, localizationConfig),
                    diagnostics);
            }
            catch (Exception exception)
            {
                diagnostics.Add(new LocalizationCatalogDiagnostic(
                    LocalizationCatalogDiagnosticSeverity.Error,
                    $"刷新本地化 Editor Catalog 失败：{exception.Message}"));
                return SetCurrent(sourceRevision, previewField, null, diagnostics);
            }
        }

        public bool TryGetText(string key, out string text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var snapshot = m_Current ?? Refresh();
            if (snapshot.Entries.TryGetValue(key.Trim(), out var entry) is false)
            {
                return false;
            }

            text = entry.PreviewText;
            return true;
        }

        public IReadOnlyList<LocalizationSearchResult> Search(string query, int limit = 100)
        {
            if (limit <= 0)
            {
                return Array.Empty<LocalizationSearchResult>();
            }

            var snapshot = m_Current ?? Refresh();
            var normalizedQuery = query?.Trim() ?? string.Empty;
            var matches = new List<(LocalizationSearchResult Result, int Rank)>();
            foreach (var entry in snapshot.Entries.Values)
            {
                var rank = MatchRank(entry.Key, entry.PreviewText, normalizedQuery);
                if (normalizedQuery.Length > 0 && rank >= 5)
                {
                    continue;
                }

                matches.Add((
                    new LocalizationSearchResult(entry.Key, entry.PreviewText, entry.IsEmpty),
                    rank));
            }

            return matches
                .OrderBy(match => match.Rank)
                .ThenBy(match => match.Result.Key, StringComparer.Ordinal)
                .Take(limit)
                .Select(match => match.Result)
                .ToArray();
        }

        private LocalizationCatalogSnapshot SetCurrent(
            long sourceRevision,
            string previewField,
            IDictionary<string, LocalizationEditorEntry> entries,
            IReadOnlyList<LocalizationCatalogDiagnostic> diagnostics)
        {
            m_Current = new LocalizationCatalogSnapshot(
                sourceRevision,
                previewField,
                entries ?? new Dictionary<string, LocalizationEditorEntry>(StringComparer.Ordinal),
                diagnostics);
            return m_Current;
        }

        private static Dictionary<string, LocalizationEditorEntry> BuildEntries(
            LubanTableData data,
            LocalizationProjectConfig config)
        {
            var entries = new Dictionary<string, LocalizationEditorEntry>(StringComparer.Ordinal);
            var keyField = config.KeyField.Trim();
            var previewField = config.PreviewField.Trim();
            foreach (var row in data.Rows)
            {
                var key = row.Values[keyField].Trim();
                row.Values.TryGetValue(previewField, out var previewText);
                entries.Add(key, new LocalizationEditorEntry(key, row.SourceRow, previewText));
            }

            return entries;
        }

        private static void AppendSourceDiagnostics(
            LubanSourceSnapshot snapshot,
            string selectedSourceId,
            ICollection<LocalizationCatalogDiagnostic> diagnostics)
        {
            foreach (var sourceDiagnostic in snapshot.Diagnostics)
            {
                var severity = sourceDiagnostic.Severity switch
                {
                    LubanDiagnosticSeverity.Info => LocalizationCatalogDiagnosticSeverity.Info,
                    LubanDiagnosticSeverity.Warning => LocalizationCatalogDiagnosticSeverity.Warning,
                    _ when string.IsNullOrWhiteSpace(sourceDiagnostic.SourceId) ||
                           string.Equals(sourceDiagnostic.SourceId, selectedSourceId, StringComparison.Ordinal) =>
                        LocalizationCatalogDiagnosticSeverity.Error,
                    _ => LocalizationCatalogDiagnosticSeverity.Warning
                };
                diagnostics.Add(new LocalizationCatalogDiagnostic(
                    severity,
                    sourceDiagnostic.Message,
                    sourceDiagnostic.SourceId));
            }
        }

        private static void AppendContractDiagnostics(
            LocalizationTableContractValidationResult contract,
            ICollection<LocalizationCatalogDiagnostic> diagnostics)
        {
            foreach (var contractDiagnostic in contract.Diagnostics)
            {
                var severity = contractDiagnostic.Severity switch
                {
                    LocalizationContractDiagnosticSeverity.Info => LocalizationCatalogDiagnosticSeverity.Info,
                    LocalizationContractDiagnosticSeverity.Warning => LocalizationCatalogDiagnosticSeverity.Warning,
                    _ => LocalizationCatalogDiagnosticSeverity.Error
                };
                diagnostics.Add(new LocalizationCatalogDiagnostic(
                    severity,
                    contractDiagnostic.Message,
                    contract.Table?.SourceId,
                    contractDiagnostic.SourceRow));
            }
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

            if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            return text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ? 4 : 5;
        }
    }
}

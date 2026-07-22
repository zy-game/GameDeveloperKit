using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameDeveloperKit.Localization;

namespace GameDeveloperKit.LocalizationEditor
{
    public sealed class LocalizationImportRequest
    {
        public LocalizationImportRequest(
            string catalogId,
            string tableId,
            string keyField,
            long sourceRevision,
            IEnumerable<LocalizationImportColumn> columns)
        {
            CatalogId = catalogId?.Trim() ?? string.Empty;
            TableId = tableId?.Trim() ?? string.Empty;
            KeyField = keyField?.Trim() ?? string.Empty;
            SourceRevision = sourceRevision;
            Columns = (columns ?? Array.Empty<LocalizationImportColumn>()).ToArray();
        }

        public string CatalogId { get; }

        public string TableId { get; }

        public string KeyField { get; }

        public long SourceRevision { get; }

        public IReadOnlyList<LocalizationImportColumn> Columns { get; }
    }

    public sealed class LocalizationImportColumn
    {
        public LocalizationImportColumn(string targetLocale, string sourceField)
        {
            TargetLocale = LocalizationAuthoringService.NormalizeLocale(targetLocale);
            SourceField = sourceField?.Trim() ?? string.Empty;
        }

        public string TargetLocale { get; }

        public string SourceField { get; }
    }

    public enum LocalizationMergeKind
    {
        Unchanged,
        Add,
        UpdateFromSource,
        KeepAsset,
        Conflict,
        DeleteCandidate
    }

    public enum LocalizationConflictResolution
    {
        Unresolved,
        UseAsset,
        UseSource
    }

    public enum LocalizationImportDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class LocalizationImportDiagnostic
    {
        public LocalizationImportDiagnostic(
            LocalizationImportDiagnosticSeverity severity,
            string code,
            string message,
            int sourceRow = 0)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            SourceRow = sourceRow;
        }

        public LocalizationImportDiagnosticSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public int SourceRow { get; }
    }

    public readonly struct LocalizationImportValue : IEquatable<LocalizationImportValue>
    {
        public LocalizationImportValue(bool exists, string value)
        {
            Exists = exists;
            Value = exists ? value ?? string.Empty : string.Empty;
        }

        public bool Exists { get; }

        public string Value { get; }

        public static LocalizationImportValue Missing => new LocalizationImportValue(false, null);

        public static LocalizationImportValue From(string value) => new LocalizationImportValue(true, value);

        public bool Equals(LocalizationImportValue other)
        {
            return Exists == other.Exists &&
                   (Exists is false || string.Equals(Value, other.Value, StringComparison.Ordinal));
        }

        public override bool Equals(object obj)
        {
            return obj is LocalizationImportValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Exists.GetHashCode() * 397) ^ (Exists ? StringComparer.Ordinal.GetHashCode(Value) : 0);
            }
        }

        public static bool operator ==(LocalizationImportValue left, LocalizationImportValue right) =>
            left.Equals(right);

        public static bool operator !=(LocalizationImportValue left, LocalizationImportValue right) =>
            left.Equals(right) is false;
    }

    public sealed class LocalizationImportMergeEntry
    {
        internal LocalizationImportMergeEntry(
            long keyId,
            string baseKey,
            string assetKey,
            string sourceKey,
            string targetLocale,
            string sourceField,
            LocalizationImportValue baseValue,
            LocalizationImportValue assetValue,
            LocalizationImportValue sourceValue,
            LocalizationMergeKind kind,
            LocalizationMergeKind keyKind,
            LocalizationMergeKind valueKind,
            int sourceRow)
        {
            KeyId = keyId;
            BaseKey = baseKey ?? string.Empty;
            AssetKey = assetKey ?? string.Empty;
            SourceKey = sourceKey ?? string.Empty;
            TargetLocale = targetLocale ?? string.Empty;
            SourceField = sourceField ?? string.Empty;
            BaseValue = baseValue;
            AssetValue = assetValue;
            SourceValue = sourceValue;
            Kind = kind;
            KeyKind = keyKind;
            ValueKind = valueKind;
            SourceRow = sourceRow;
        }

        public long KeyId { get; }

        public string BaseKey { get; }

        public string AssetKey { get; }

        public string SourceKey { get; }

        public string DisplayKey => SourceKey.Length > 0
            ? SourceKey
            : AssetKey.Length > 0 ? AssetKey : BaseKey;

        public string TargetLocale { get; }

        public string SourceField { get; }

        public LocalizationImportValue BaseValue { get; }

        public LocalizationImportValue AssetValue { get; }

        public LocalizationImportValue SourceValue { get; }

        public LocalizationMergeKind Kind { get; }

        public LocalizationMergeKind KeyKind { get; }

        public LocalizationMergeKind ValueKind { get; }

        public int SourceRow { get; }

        public LocalizationConflictResolution Resolution { get; internal set; }

        public bool RequiresResolution => KeyKind == LocalizationMergeKind.Conflict ||
                                          ValueKind is LocalizationMergeKind.Conflict or LocalizationMergeKind.DeleteCandidate;

        public bool HasKeyConflict => KeyKind == LocalizationMergeKind.Conflict;
    }

    public sealed class LocalizationImportPlan
    {
        private readonly IReadOnlyList<LocalizationImportMergeEntry> m_Entries;
        private readonly IReadOnlyList<LocalizationImportDiagnostic> m_Diagnostics;

        internal LocalizationImportPlan(
            LocalizationImportRequest request,
            string sourceId,
            long authoringRevision,
            string sourceFingerprint,
            IEnumerable<LocalizationImportMergeEntry> entries,
            IEnumerable<LocalizationImportDiagnostic> diagnostics,
            LocalizationImportBaselineDocument baseline)
        {
            Request = request;
            SourceId = sourceId ?? string.Empty;
            AuthoringRevision = authoringRevision;
            SourceFingerprint = sourceFingerprint ?? string.Empty;
            m_Entries = (entries ?? Array.Empty<LocalizationImportMergeEntry>())
                .OrderBy(entry => entry.DisplayKey, StringComparer.Ordinal)
                .ThenBy(entry => entry.TargetLocale, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.SourceField, StringComparer.Ordinal)
                .ToArray();
            m_Diagnostics = (diagnostics ?? Array.Empty<LocalizationImportDiagnostic>()).ToArray();
            Baseline = baseline;
        }

        public LocalizationImportRequest Request { get; }

        public string SourceId { get; }

        public long AuthoringRevision { get; }

        public IReadOnlyList<LocalizationImportMergeEntry> Entries => m_Entries;

        public IReadOnlyList<LocalizationImportDiagnostic> Diagnostics => m_Diagnostics;

        public int UnresolvedCount => m_Entries.Count(entry =>
            entry.RequiresResolution && entry.Resolution == LocalizationConflictResolution.Unresolved);

        public bool CanApply => IsApplied is false &&
                                m_Diagnostics.All(diagnostic =>
                                    diagnostic.Severity != LocalizationImportDiagnosticSeverity.Error) &&
                                TryValidateResolutions(out _);

        internal string SourceFingerprint { get; }

        internal LocalizationImportBaselineDocument Baseline { get; }

        internal bool IsApplied { get; set; }

        public void Resolve(long keyId, string targetLocale, LocalizationConflictResolution resolution)
        {
            if (resolution == LocalizationConflictResolution.Unresolved)
            {
                throw new ArgumentException("请选择资产版本或配置表版本。", nameof(resolution));
            }

            var entry = m_Entries.FirstOrDefault(candidate =>
                candidate.KeyId == keyId &&
                string.Equals(candidate.TargetLocale, targetLocale, StringComparison.OrdinalIgnoreCase) &&
                candidate.RequiresResolution);
            if (entry == null)
            {
                throw new ArgumentException("找不到需要解决的导入项。", nameof(keyId));
            }

            entry.Resolution = resolution;
        }

        public void ResolveKey(long keyId, LocalizationConflictResolution resolution)
        {
            if (resolution == LocalizationConflictResolution.Unresolved)
            {
                throw new ArgumentException("请选择资产版本或配置表版本。", nameof(resolution));
            }

            foreach (var entry in m_Entries.Where(candidate =>
                         candidate.KeyId == keyId && candidate.RequiresResolution))
            {
                entry.Resolution = resolution;
            }
        }

        public void ResolveAll(LocalizationConflictResolution resolution)
        {
            if (resolution == LocalizationConflictResolution.Unresolved)
            {
                throw new ArgumentException("请选择资产版本或配置表版本。", nameof(resolution));
            }

            foreach (var entry in m_Entries.Where(candidate => candidate.RequiresResolution))
            {
                entry.Resolution = resolution;
            }
        }

        internal bool TryValidateResolutions(out string message)
        {
            if (IsApplied)
            {
                message = "该导入计划已经应用。";
                return false;
            }

            if (m_Entries.Any(entry =>
                    entry.RequiresResolution && entry.Resolution == LocalizationConflictResolution.Unresolved))
            {
                message = "仍有冲突或删除候选未解决。";
                return false;
            }

            foreach (var group in m_Entries.Where(entry => entry.HasKeyConflict).GroupBy(entry => entry.KeyId))
            {
                if (group.Select(entry => entry.Resolution).Distinct().Count() > 1)
                {
                    message = $"同一 KeyId 的名称冲突必须选择同一版本：{group.Key}";
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }
    }

    public sealed class LocalizationImportAssetMutation
    {
        public LocalizationImportAssetMutation(
            string expectedCatalogId,
            long expectedAuthoringRevision,
            IEnumerable<LocalizationKeyEntry> keys,
            IDictionary<string, IReadOnlyList<LocalizationValueEntry>> localeValues,
            string baselinePath,
            string baselineJson)
        {
            ExpectedCatalogId = expectedCatalogId?.Trim() ?? string.Empty;
            ExpectedAuthoringRevision = expectedAuthoringRevision;
            Keys = (keys ?? Array.Empty<LocalizationKeyEntry>())
                .Select(entry => entry == null ? null : new LocalizationKeyEntry(entry.Id, entry.Key))
                .ToArray();
            LocaleValues = new ReadOnlyDictionary<string, IReadOnlyList<LocalizationValueEntry>>(
                (localeValues ?? new Dictionary<string, IReadOnlyList<LocalizationValueEntry>>())
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<LocalizationValueEntry>)(pair.Value ??
                            Array.Empty<LocalizationValueEntry>())
                        .Select(entry => entry == null
                            ? null
                            : new LocalizationValueEntry(entry.KeyId, entry.Value))
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase));
            BaselinePath = baselinePath ?? string.Empty;
            BaselineJson = baselineJson ?? string.Empty;
        }

        public string ExpectedCatalogId { get; }

        public long ExpectedAuthoringRevision { get; }

        public IReadOnlyList<LocalizationKeyEntry> Keys { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<LocalizationValueEntry>> LocaleValues { get; }

        public string BaselinePath { get; }

        public string BaselineJson { get; }
    }
}

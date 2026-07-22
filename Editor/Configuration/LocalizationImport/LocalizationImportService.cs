using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Localization;
using GameDeveloperKit.LubanConfigEditor;

namespace GameDeveloperKit.LocalizationEditor
{
    public interface ILocalizationImportService
    {
        LubanSourceSnapshot RefreshSource();

        LocalizationImportPlan CreatePlan(LocalizationImportRequest request);

        LocalizationMutationResult Apply(LocalizationImportPlan plan);
    }

    public sealed class LocalizationImportService : ILocalizationImportService
    {
        private readonly ILubanSourceCatalog m_SourceCatalog;
        private readonly Func<LubanProjectConfig> m_ProjectConfig;
        private readonly ILocalizationAuthoringService m_Authoring;
        private readonly ILocalizationImportBaselineStore m_Baselines;
        private LubanSourceSnapshot m_SourceSnapshot;

        public LocalizationImportService(
            ILubanSourceCatalog sourceCatalog,
            Func<LubanProjectConfig> projectConfig,
            ILocalizationAuthoringService authoring)
            : this(sourceCatalog, projectConfig, authoring, LocalizationImportBaselineStore.Shared)
        {
        }

        internal LocalizationImportService(
            ILubanSourceCatalog sourceCatalog,
            Func<LubanProjectConfig> projectConfig,
            ILocalizationAuthoringService authoring,
            ILocalizationImportBaselineStore baselines)
        {
            m_SourceCatalog = sourceCatalog ?? throw new ArgumentNullException(nameof(sourceCatalog));
            m_ProjectConfig = projectConfig ?? throw new ArgumentNullException(nameof(projectConfig));
            m_Authoring = authoring ?? throw new ArgumentNullException(nameof(authoring));
            m_Baselines = baselines ?? LocalizationImportBaselineStore.Shared;
        }

        public static LocalizationImportService Shared { get; } = new LocalizationImportService(
            LubanSourceCatalog.Shared,
            () => EditorGlobalConfig.LoadOrCreate().Luban,
            LocalizationAuthoringService.Shared);

        public LubanSourceSnapshot RefreshSource()
        {
            m_SourceSnapshot = m_SourceCatalog.Refresh(m_ProjectConfig());
            return m_SourceSnapshot;
        }

        public LocalizationImportPlan CreatePlan(LocalizationImportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var diagnostics = new List<LocalizationImportDiagnostic>();
            var authoring = m_Authoring.Refresh();
            var source = GetRequestedSourceSnapshot(request, diagnostics);
            ValidateAuthoring(request, authoring, diagnostics);
            var contract = LocalizationTableContractValidator.Validate(source, m_SourceCatalog, request);
            diagnostics.AddRange(contract.Diagnostics.Select(diagnostic => new LocalizationImportDiagnostic(
                LocalizationImportDiagnosticSeverity.Error,
                "table_contract",
                diagnostic.Message,
                diagnostic.SourceRow)));
            var pendingLocales = CollectPendingLocales(request, authoring, diagnostics);

            var baseline = m_Baselines.Load(request.CatalogId);
            diagnostics.AddRange(baseline.Diagnostics);
            if (diagnostics.Any(diagnostic =>
                    diagnostic.Severity == LocalizationImportDiagnosticSeverity.Error) ||
                contract.IsValid is false || authoring.IsValid is false || baseline.IsValid is false)
            {
                return new LocalizationImportPlan(
                    request,
                    contract.Table?.SourceId,
                    authoring.Revision,
                    string.Empty,
                    Array.Empty<LocalizationImportMergeEntry>(),
                    diagnostics,
                    baseline.Document,
                    pendingLocales);
            }

            var entries = BuildEntries(
                request,
                contract.Table,
                contract.Data,
                authoring,
                baseline.Document,
                diagnostics);
            return new LocalizationImportPlan(
                request,
                contract.Table.SourceId,
                authoring.Revision,
                CreateFingerprint(request, contract.Data),
                entries,
                diagnostics,
                baseline.Document,
                pendingLocales);
        }

        public LocalizationMutationResult Apply(LocalizationImportPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (plan.Diagnostics.Any(diagnostic =>
                    diagnostic.Severity == LocalizationImportDiagnosticSeverity.Error))
            {
                return LocalizationMutationResult.Failure("导入计划包含阻断错误。", m_Authoring.Refresh());
            }

            if (plan.TryValidateResolutions(out var resolutionError) is false)
            {
                return LocalizationMutationResult.Failure(resolutionError, m_Authoring.Refresh());
            }

            var authoring = m_Authoring.Refresh();
            if (authoring.Revision != plan.AuthoringRevision ||
                string.Equals(authoring.Catalog?.CatalogId, plan.Request.CatalogId, StringComparison.Ordinal) is false)
            {
                return LocalizationMutationResult.Failure("本地化资产已变化，请重新生成导入预览。", authoring);
            }

            var currentSource = RefreshSource();
            if (plan.Request.SourceRevision > 0 &&
                currentSource.Revision != plan.Request.SourceRevision)
            {
                return LocalizationMutationResult.Failure("配置表目录已刷新，请重新生成导入预览。", authoring);
            }

            var contract = LocalizationTableContractValidator.Validate(
                currentSource,
                m_SourceCatalog,
                new LocalizationImportRequest(
                    plan.Request.CatalogId,
                    plan.Request.TableId,
                    plan.Request.KeyField,
                    currentSource.Revision,
                    plan.Request.Columns));
            if (contract.IsValid is false ||
                string.Equals(CreateFingerprint(plan.Request, contract.Data), plan.SourceFingerprint,
                    StringComparison.Ordinal) is false)
            {
                return LocalizationMutationResult.Failure("配置表内容已变化，请重新生成导入预览。", authoring);
            }

            var mutation = CreateMutation(plan, authoring, currentSource.Revision);
            var result = m_Authoring.ApplyImport(mutation);
            if (result.Succeeded)
            {
                plan.IsApplied = true;
            }

            return result;
        }

        private LubanSourceSnapshot GetRequestedSourceSnapshot(
            LocalizationImportRequest request,
            ICollection<LocalizationImportDiagnostic> diagnostics)
        {
            if (m_SourceSnapshot == null)
            {
                RefreshSource();
            }

            if (request.SourceRevision > 0 && request.SourceRevision != m_SourceSnapshot.Revision)
            {
                diagnostics.Add(Error("source_revision_stale", "配置表目录已刷新，请重新选择导入字段。"));
            }

            return m_SourceSnapshot;
        }

        private static void ValidateAuthoring(
            LocalizationImportRequest request,
            LocalizationAuthoringSnapshot authoring,
            ICollection<LocalizationImportDiagnostic> diagnostics)
        {
            if (authoring?.IsValid != true)
            {
                diagnostics.Add(Error("catalog_invalid", "本地化资产不可用，无法生成导入预览。"));
                return;
            }

            if (string.Equals(authoring.Catalog.CatalogId, request.CatalogId, StringComparison.Ordinal) is false)
            {
                diagnostics.Add(Error(
                    "catalog_mismatch",
                    $"导入请求 CatalogId 与当前资产不匹配：{request.CatalogId}"));
            }
        }

        private static IReadOnlyList<string> CollectPendingLocales(
            LocalizationImportRequest request,
            LocalizationAuthoringSnapshot authoring,
            ICollection<LocalizationImportDiagnostic> diagnostics)
        {
            if (authoring?.Catalog == null)
            {
                return Array.Empty<string>();
            }

            var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in request.Columns)
            {
                if (column == null)
                {
                    continue;
                }

                if (authoring.TryGetLocale(column.TargetLocale, out _) is false)
                {
                    pending.Add(column.TargetLocale);
                }
            }

            foreach (var locale in pending.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(new LocalizationImportDiagnostic(
                    LocalizationImportDiagnosticSeverity.Info,
                    "target_locale_pending",
                    $"目标语言将在应用导入时创建：{locale}"));
            }

            return pending.ToArray();
        }

        private static IReadOnlyList<LocalizationImportMergeEntry> BuildEntries(
            LocalizationImportRequest request,
            LubanTableDescriptor table,
            LubanTableData data,
            LocalizationAuthoringSnapshot authoring,
            LocalizationImportBaselineDocument baseline,
            ICollection<LocalizationImportDiagnostic> diagnostics)
        {
            var sourceRows = data.Rows.ToDictionary(
                row => (row.Values.TryGetValue(request.KeyField, out var value) ? value : string.Empty).Trim(),
                row => row,
                StringComparer.Ordinal);
            var sourceKeysClaimed = new HashSet<string>(StringComparer.Ordinal);
            var assetById = authoring.Entries.ToDictionary(entry => entry.KeyId);
            var assetByKey = authoring.Entries.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
            var selectedMappings = new HashSet<string>(request.Columns.Select(ColumnIdentity), StringComparer.Ordinal);
            var selectedBaseline = baseline.Entries
                .Where(entry => string.Equals(entry.SourceId, table.SourceId, StringComparison.Ordinal) &&
                                string.Equals(entry.TableId, request.TableId, StringComparison.Ordinal) &&
                                selectedMappings.Contains(ColumnIdentity(entry.TargetLocale, entry.SourceField)))
                .ToArray();
            var result = new List<LocalizationImportMergeEntry>();
            foreach (var group in selectedBaseline.GroupBy(entry => entry.KeyId).OrderBy(group => group.Key))
            {
                AddBaselineGroup(
                    group.Key,
                    group.ToArray(),
                    request,
                    authoring,
                    assetById,
                    sourceRows,
                    sourceKeysClaimed,
                    result,
                    diagnostics);
            }

            var usedKeyIds = new HashSet<long>(authoring.Entries.Select(entry => entry.KeyId));
            foreach (var row in sourceRows.Where(pair => sourceKeysClaimed.Contains(pair.Key) is false)
                         .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (assetByKey.TryGetValue(row.Key, out var asset))
                {
                    foreach (var column in request.Columns)
                    {
                        var sourceValue = LocalizationImportValue.From(row.Value.Values[column.SourceField]);
                        var assetValue = GetAssetValue(authoring, asset.KeyId, column.TargetLocale);
                        var valueKind = assetValue == sourceValue
                            ? LocalizationMergeKind.Unchanged
                            : LocalizationMergeKind.Conflict;
                        result.Add(CreateEntry(
                            asset.KeyId,
                            string.Empty,
                            asset.Key,
                            row.Key,
                            column,
                            LocalizationImportValue.Missing,
                            assetValue,
                            sourceValue,
                            LocalizationMergeKind.Unchanged,
                            valueKind,
                            row.Value.SourceRow));
                    }
                }
                else
                {
                    var keyId = CreateKeyId(usedKeyIds);
                    usedKeyIds.Add(keyId);
                    foreach (var column in request.Columns)
                    {
                        result.Add(CreateEntry(
                            keyId,
                            string.Empty,
                            string.Empty,
                            row.Key,
                            column,
                            LocalizationImportValue.Missing,
                            LocalizationImportValue.Missing,
                            LocalizationImportValue.From(row.Value.Values[column.SourceField]),
                            LocalizationMergeKind.Add,
                            LocalizationMergeKind.Add,
                            row.Value.SourceRow));
                    }
                }
            }

            return result;
        }

        private static void AddBaselineGroup(
            long keyId,
            IReadOnlyList<LocalizationImportBaselineEntry> baselineEntries,
            LocalizationImportRequest request,
            LocalizationAuthoringSnapshot authoring,
            IReadOnlyDictionary<long, LocalizationAuthoringEntry> assetById,
            IReadOnlyDictionary<string, LubanTableRow> sourceRows,
            ISet<string> sourceKeysClaimed,
            ICollection<LocalizationImportMergeEntry> target,
            ICollection<LocalizationImportDiagnostic> diagnostics)
        {
            var baseKeys = baselineEntries.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).ToArray();
            if (baseKeys.Length != 1)
            {
                diagnostics.Add(Error("baseline_key_mismatch", $"Baseline 中同一 KeyId 对应了多个 Key：{keyId}"));
                return;
            }

            var baseKey = baseKeys[0];
            assetById.TryGetValue(keyId, out var asset);
            var hasBaseRow = sourceRows.TryGetValue(baseKey, out var sourceRow);
            LubanTableRow assetNamedRow = null;
            var hasAssetRow = asset != null && sourceRows.TryGetValue(asset.Key, out assetNamedRow);
            if (hasBaseRow && hasAssetRow && ReferenceEquals(sourceRow, assetNamedRow) is false)
            {
                diagnostics.Add(Error(
                    "source_key_ambiguous",
                    $"配置表同时包含 Baseline Key 和资产重命名 Key，无法判断身份：{baseKey}/{asset.Key}"));
                return;
            }

            sourceRow ??= assetNamedRow;
            if (sourceRow != null)
            {
                var sourceKey = GetSourceKey(sourceRow, request.KeyField);
                sourceKeysClaimed.Add(sourceKey);
            }

            foreach (var column in request.Columns)
            {
                var baseline = baselineEntries.FirstOrDefault(entry =>
                    string.Equals(entry.TargetLocale, column.TargetLocale, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.SourceField, column.SourceField, StringComparison.Ordinal));
                if (baseline == null && sourceRow == null)
                {
                    continue;
                }

                var assetValue = asset == null
                    ? LocalizationImportValue.Missing
                    : GetAssetValue(authoring, keyId, column.TargetLocale);
                var sourceValue = sourceRow == null
                    ? LocalizationImportValue.Missing
                    : LocalizationImportValue.From(sourceRow.Values[column.SourceField]);
                var baseValue = baseline == null
                    ? LocalizationImportValue.Missing
                    : LocalizationImportValue.From(baseline.BaseValue);
                LocalizationMergeKind keyKind;
                LocalizationMergeKind valueKind;
                if (sourceRow == null)
                {
                    keyKind = LocalizationMergeKind.KeepAsset;
                    valueKind = assetValue.Exists || asset != null
                        ? LocalizationMergeKind.DeleteCandidate
                        : LocalizationMergeKind.Unchanged;
                }
                else if (baseline == null)
                {
                    keyKind = string.Equals(asset?.Key, GetSourceKey(sourceRow, request.KeyField), StringComparison.Ordinal)
                        ? LocalizationMergeKind.Unchanged
                        : LocalizationMergeKind.Conflict;
                    valueKind = assetValue == sourceValue
                        ? LocalizationMergeKind.Unchanged
                        : LocalizationMergeKind.Conflict;
                }
                else
                {
                    keyKind = Classify(
                        LocalizationImportValue.From(baseKey),
                        asset == null ? LocalizationImportValue.Missing : LocalizationImportValue.From(asset.Key),
                        LocalizationImportValue.From(GetSourceKey(sourceRow, request.KeyField)));
                    valueKind = Classify(baseValue, assetValue, sourceValue);
                }

                target.Add(CreateEntry(
                    keyId,
                    baseline == null ? string.Empty : baseKey,
                    asset?.Key,
                    sourceRow == null ? string.Empty : GetSourceKey(sourceRow, request.KeyField),
                    column,
                    baseValue,
                    assetValue,
                    sourceValue,
                    keyKind,
                    valueKind,
                    sourceRow?.SourceRow ?? 0));
            }
        }

        private static LocalizationImportMergeEntry CreateEntry(
            long keyId,
            string baseKey,
            string assetKey,
            string sourceKey,
            LocalizationImportColumn column,
            LocalizationImportValue baseValue,
            LocalizationImportValue assetValue,
            LocalizationImportValue sourceValue,
            LocalizationMergeKind keyKind,
            LocalizationMergeKind valueKind,
            int sourceRow)
        {
            return new LocalizationImportMergeEntry(
                keyId,
                baseKey,
                assetKey,
                sourceKey,
                column.TargetLocale,
                column.SourceField,
                baseValue,
                assetValue,
                sourceValue,
                EffectiveKind(keyKind, valueKind),
                keyKind,
                valueKind,
                sourceRow);
        }

        private LocalizationImportAssetMutation CreateMutation(
            LocalizationImportPlan plan,
            LocalizationAuthoringSnapshot snapshot,
            long currentSourceRevision)
        {
            var keys = snapshot.Catalog.Keys
                .Where(entry => entry != null)
                .ToDictionary(entry => entry.Id, entry => new LocalizationKeyEntry(entry.Id, entry.Key));
            foreach (var group in plan.Entries.GroupBy(entry => entry.KeyId))
            {
                var finalKey = ResolveKey(group);
                if (finalKey.Length == 0)
                {
                    continue;
                }

                keys[group.Key] = new LocalizationKeyEntry(group.Key, finalKey);
            }

            var localeValues = new Dictionary<string, IReadOnlyList<LocalizationValueEntry>>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var column in plan.Request.Columns)
            {
                var values = snapshot.TryGetLocale(column.TargetLocale, out var locale)
                    ? locale.Asset.Entries
                        .Where(entry => entry != null)
                        .ToDictionary(entry => entry.KeyId, entry => entry.Value)
                    : new Dictionary<long, string>();
                foreach (var entry in plan.Entries.Where(entry =>
                             string.Equals(entry.TargetLocale, column.TargetLocale,
                                 StringComparison.OrdinalIgnoreCase)))
                {
                    if (keys.ContainsKey(entry.KeyId) is false)
                    {
                        continue;
                    }

                    switch (entry.ValueKind)
                    {
                        case LocalizationMergeKind.Add:
                        case LocalizationMergeKind.UpdateFromSource:
                            values[entry.KeyId] = entry.SourceValue.Value;
                            break;
                        case LocalizationMergeKind.Conflict:
                            if (entry.Resolution == LocalizationConflictResolution.UseSource)
                            {
                                values[entry.KeyId] = entry.SourceValue.Value;
                            }
                            break;
                        case LocalizationMergeKind.DeleteCandidate:
                            if (entry.Resolution == LocalizationConflictResolution.UseSource)
                            {
                                values.Remove(entry.KeyId);
                            }
                            break;
                    }
                }

                localeValues[column.TargetLocale] = values
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new LocalizationValueEntry(pair.Key, pair.Value))
                    .ToArray();
            }

            var baseline = CloneBaseline(plan.Baseline);
            var selectedMappings = new HashSet<string>(plan.Request.Columns.Select(ColumnIdentity), StringComparer.Ordinal);
            baseline.Entries.RemoveAll(entry =>
                string.Equals(entry.SourceId, plan.SourceId, StringComparison.Ordinal) &&
                string.Equals(entry.TableId, plan.Request.TableId, StringComparison.Ordinal) &&
                selectedMappings.Contains(ColumnIdentity(entry.TargetLocale, entry.SourceField)));
            foreach (var entry in plan.Entries.Where(entry => entry.SourceValue.Exists))
            {
                baseline.Entries.Add(new LocalizationImportBaselineEntry
                {
                    SourceId = plan.SourceId,
                    TableId = plan.Request.TableId,
                    SourceField = entry.SourceField,
                    TargetLocale = entry.TargetLocale,
                    KeyId = entry.KeyId,
                    Key = entry.SourceKey,
                    BaseValue = entry.SourceValue.Value,
                    SourceRevision = currentSourceRevision
                });
            }

            var catalogFolder = Path.GetDirectoryName(snapshot.CatalogPath)?.Replace('\\', '/') ?? "Assets";
            var catalogName = Path.GetFileNameWithoutExtension(snapshot.CatalogPath);
            var newLocales = plan.PendingLocales.Select(locale =>
            {
                var assetPath = $"{catalogFolder}/{catalogName}.{locale}.asset";
                return new LocalizationLocaleDraft(locale, assetPath, assetPath);
            });
            return new LocalizationImportAssetMutation(
                plan.Request.CatalogId,
                plan.AuthoringRevision,
                keys.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal),
                localeValues,
                m_Baselines.GetPath(plan.Request.CatalogId),
                m_Baselines.Serialize(baseline),
                newLocales);
        }

        private static string ResolveKey(IEnumerable<LocalizationImportMergeEntry> entries)
        {
            var group = entries.ToArray();
            var keyConflict = group.FirstOrDefault(entry => entry.HasKeyConflict);
            if (keyConflict != null)
            {
                return keyConflict.Resolution == LocalizationConflictResolution.UseSource
                    ? keyConflict.SourceKey
                    : keyConflict.AssetKey;
            }

            var sourceEntry = group.FirstOrDefault(entry =>
                entry.KeyKind is LocalizationMergeKind.Add or LocalizationMergeKind.UpdateFromSource);
            return sourceEntry?.SourceKey ?? group.First().AssetKey;
        }

        internal static LocalizationMergeKind Classify(
            LocalizationImportValue baseValue,
            LocalizationImportValue assetValue,
            LocalizationImportValue sourceValue)
        {
            if (assetValue == sourceValue)
            {
                return LocalizationMergeKind.Unchanged;
            }

            if (assetValue == baseValue)
            {
                return LocalizationMergeKind.UpdateFromSource;
            }

            if (sourceValue == baseValue)
            {
                return LocalizationMergeKind.KeepAsset;
            }

            return LocalizationMergeKind.Conflict;
        }

        private static LocalizationMergeKind EffectiveKind(
            LocalizationMergeKind keyKind,
            LocalizationMergeKind valueKind)
        {
            if (keyKind == LocalizationMergeKind.Conflict || valueKind == LocalizationMergeKind.Conflict)
            {
                return LocalizationMergeKind.Conflict;
            }

            if (valueKind == LocalizationMergeKind.DeleteCandidate)
            {
                return LocalizationMergeKind.DeleteCandidate;
            }

            if (keyKind == LocalizationMergeKind.Add || valueKind == LocalizationMergeKind.Add)
            {
                return LocalizationMergeKind.Add;
            }

            if (keyKind == LocalizationMergeKind.UpdateFromSource ||
                valueKind == LocalizationMergeKind.UpdateFromSource)
            {
                return LocalizationMergeKind.UpdateFromSource;
            }

            if (keyKind == LocalizationMergeKind.KeepAsset || valueKind == LocalizationMergeKind.KeepAsset)
            {
                return LocalizationMergeKind.KeepAsset;
            }

            return LocalizationMergeKind.Unchanged;
        }

        private static LocalizationImportValue GetAssetValue(
            LocalizationAuthoringSnapshot snapshot,
            long keyId,
            string locale)
        {
            return snapshot.TryGetText(keyId, locale, out var value)
                ? LocalizationImportValue.From(value)
                : LocalizationImportValue.Missing;
        }

        private static string GetSourceKey(LubanTableRow row, string keyField)
        {
            return (row.Values.TryGetValue(keyField, out var value) ? value : string.Empty).Trim();
        }

        private static long CreateKeyId(ISet<long> existing)
        {
            long candidate;
            do
            {
                candidate = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & long.MaxValue;
            } while (candidate == 0 || existing.Contains(candidate));

            return candidate;
        }

        private static string CreateFingerprint(LocalizationImportRequest request, LubanTableData data)
        {
            var builder = new StringBuilder();
            builder.Append(request.TableId).Append('\n').Append(request.KeyField).Append('\n');
            foreach (var column in request.Columns.OrderBy(column => column.TargetLocale, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append(column.TargetLocale).Append('\0').Append(column.SourceField).Append('\n');
            }

            foreach (var row in data.Rows)
            {
                builder.Append(row.SourceRow).Append('\0');
                builder.Append(GetSourceKey(row, request.KeyField)).Append('\0');
                foreach (var column in request.Columns)
                {
                    builder.Append(row.Values[column.SourceField]).Append('\0');
                }

                builder.Append('\n');
            }

            using (var hash = SHA256.Create())
            {
                return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))
                    .Select(value => value.ToString("x2")));
            }
        }

        private static LocalizationImportBaselineDocument CloneBaseline(
            LocalizationImportBaselineDocument source)
        {
            return new LocalizationImportBaselineDocument
            {
                SchemaVersion = LocalizationImportBaselineDocument.CurrentSchemaVersion,
                CatalogId = source.CatalogId,
                Entries = (source.Entries ?? new List<LocalizationImportBaselineEntry>())
                    .Select(entry => new LocalizationImportBaselineEntry
                    {
                        SourceId = entry.SourceId,
                        TableId = entry.TableId,
                        SourceField = entry.SourceField,
                        TargetLocale = entry.TargetLocale,
                        KeyId = entry.KeyId,
                        Key = entry.Key,
                        BaseValue = entry.BaseValue,
                        SourceRevision = entry.SourceRevision
                    })
                    .ToList()
            };
        }

        private static string ColumnIdentity(LocalizationImportColumn column)
        {
            return ColumnIdentity(column.TargetLocale, column.SourceField);
        }

        private static string ColumnIdentity(string targetLocale, string sourceField)
        {
            return LocalizationAuthoringService.NormalizeLocale(targetLocale) + "\n" + (sourceField ?? string.Empty);
        }

        private static LocalizationImportDiagnostic Error(string code, string message)
        {
            return new LocalizationImportDiagnostic(LocalizationImportDiagnosticSeverity.Error, code, message);
        }
    }
}

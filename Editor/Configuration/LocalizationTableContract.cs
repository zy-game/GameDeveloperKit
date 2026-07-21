using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.LubanConfigEditor;

namespace GameDeveloperKit.LocalizationEditor
{
    public enum LocalizationContractDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class LocalizationContractDiagnostic
    {
        public LocalizationContractDiagnostic(
            LocalizationContractDiagnosticSeverity severity,
            string message,
            int sourceRow = 0)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            SourceRow = sourceRow;
        }

        public LocalizationContractDiagnosticSeverity Severity { get; }

        public string Message { get; }

        public int SourceRow { get; }
    }

    public sealed class LocalizationTableContractValidationResult
    {
        public LocalizationTableContractValidationResult(
            LubanTableDescriptor table,
            LubanTableData data,
            IReadOnlyList<LocalizationContractDiagnostic> diagnostics)
        {
            Table = table;
            Data = data;
            Diagnostics = diagnostics ?? Array.Empty<LocalizationContractDiagnostic>();
        }

        public bool IsValid =>
            Table != null &&
            Data != null &&
            Diagnostics.All(diagnostic => diagnostic.Severity != LocalizationContractDiagnosticSeverity.Error);

        public LubanTableDescriptor Table { get; }

        public LubanTableData Data { get; }

        public IReadOnlyList<LocalizationContractDiagnostic> Diagnostics { get; }
    }

    public static class LocalizationTableContractValidator
    {
        public static LocalizationTableContractValidationResult Validate(
            LubanSourceSnapshot snapshot,
            ILubanSourceCatalog catalog,
            LocalizationProjectConfig config)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var diagnostics = new List<LocalizationContractDiagnostic>();
            var tableId = config.TableId?.Trim() ?? string.Empty;
            if (tableId.Length == 0)
            {
                diagnostics.Add(Error("尚未配置本地化 TableId。"));
                return new LocalizationTableContractValidationResult(null, null, diagnostics);
            }

            var table = snapshot.Tables.FirstOrDefault(candidate =>
                string.Equals(candidate.TableId, tableId, StringComparison.Ordinal));
            if (table == null)
            {
                diagnostics.Add(Error($"本地化表不存在：{tableId}"));
                return new LocalizationTableContractValidationResult(null, null, diagnostics);
            }

            ValidateFields(table, config, diagnostics);
            if (catalog.TryReadTable(tableId, out var data, out var readDiagnostic) is false)
            {
                diagnostics.Add(Error(readDiagnostic?.Message ?? $"无法读取本地化表：{tableId}"));
                return new LocalizationTableContractValidationResult(table, null, diagnostics);
            }

            ValidateKeys(data, config.KeyField, diagnostics);
            return new LocalizationTableContractValidationResult(table, data, diagnostics);
        }

        private static void ValidateFields(
            LubanTableDescriptor table,
            LocalizationProjectConfig config,
            ICollection<LocalizationContractDiagnostic> diagnostics)
        {
            var fields = new HashSet<string>(table.Fields.Select(field => field.Name), StringComparer.Ordinal);
            var keyField = config.KeyField?.Trim() ?? string.Empty;
            if (keyField.Length == 0 || fields.Contains(keyField) is false)
            {
                diagnostics.Add(Error($"本地化 Key 字段不存在：{keyField}"));
            }

            var previewField = config.PreviewField?.Trim() ?? string.Empty;
            if (previewField.Length == 0 || fields.Contains(previewField) is false)
            {
                diagnostics.Add(Error($"本地化预览字段不存在：{previewField}"));
            }
        }

        private static void ValidateKeys(
            LubanTableData data,
            string keyField,
            ICollection<LocalizationContractDiagnostic> diagnostics)
        {
            var normalizedKeyField = keyField?.Trim() ?? string.Empty;
            var sourceRowsByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in data.Rows)
            {
                row.Values.TryGetValue(normalizedKeyField, out var rawKey);
                var key = rawKey?.Trim() ?? string.Empty;
                if (key.Length == 0)
                {
                    diagnostics.Add(Error($"本地化 Key 不能为空，源行：{row.SourceRow}", row.SourceRow));
                    continue;
                }

                if (sourceRowsByKey.TryGetValue(key, out var firstRow))
                {
                    diagnostics.Add(Error(
                        $"本地化 Key 重复：{key}，源行：{firstRow}、{row.SourceRow}",
                        row.SourceRow));
                    continue;
                }

                sourceRowsByKey.Add(key, row.SourceRow);
            }
        }

        private static LocalizationContractDiagnostic Error(string message, int sourceRow = 0)
        {
            return new LocalizationContractDiagnostic(
                LocalizationContractDiagnosticSeverity.Error,
                message,
                sourceRow);
        }
    }
}

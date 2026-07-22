using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.LubanConfigEditor;

namespace GameDeveloperKit.LocalizationEditor
{
    public sealed class LocalizationTableImportConfig
    {
        public string TableId { get; set; }

        public string KeyField { get; set; } = "key";

        public string PreviewField { get; set; }
    }

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
            LocalizationImportRequest request)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var diagnostics = new List<LocalizationContractDiagnostic>();
            var table = FindTable(snapshot, request.TableId, diagnostics);
            if (table == null)
            {
                return new LocalizationTableContractValidationResult(null, null, diagnostics);
            }

            var fields = new HashSet<string>(table.Fields.Select(field => field.Name), StringComparer.Ordinal);
            if (request.KeyField.Length == 0 || fields.Contains(request.KeyField) is false)
            {
                diagnostics.Add(Error($"本地化 Key 字段不存在：{request.KeyField}"));
            }

            if (request.Columns.Count == 0)
            {
                diagnostics.Add(Error("至少需要选择一个本地化目标语言字段。"));
            }

            var targetLocales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in request.Columns)
            {
                if (column == null || string.IsNullOrWhiteSpace(column.TargetLocale) ||
                    string.IsNullOrWhiteSpace(column.SourceField))
                {
                    diagnostics.Add(Error("本地化导入字段映射不能为空。"));
                    continue;
                }

                if (targetLocales.Add(column.TargetLocale) is false)
                {
                    diagnostics.Add(Error($"目标语言重复映射：{column.TargetLocale}"));
                }

                if (fields.Contains(column.SourceField) is false)
                {
                    diagnostics.Add(Error($"本地化源字段不存在：{column.SourceField}"));
                }
            }

            if (catalog.TryReadTable(request.TableId, out var data, out var readDiagnostic) is false)
            {
                diagnostics.Add(Error(readDiagnostic?.Message ?? $"无法读取本地化表：{request.TableId}"));
                return new LocalizationTableContractValidationResult(table, null, diagnostics);
            }

            ValidateKeys(data, request.KeyField, diagnostics);
            return new LocalizationTableContractValidationResult(table, data, diagnostics);
        }

        public static LocalizationTableContractValidationResult Validate(
            LubanSourceSnapshot snapshot,
            ILubanSourceCatalog catalog,
            LocalizationTableImportConfig config)
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
            var table = FindTable(snapshot, config.TableId, diagnostics);
            if (table == null)
            {
                return new LocalizationTableContractValidationResult(null, null, diagnostics);
            }

            ValidateFields(table, config, diagnostics);
            if (catalog.TryReadTable(table.TableId, out var data, out var readDiagnostic) is false)
            {
                diagnostics.Add(Error(readDiagnostic?.Message ?? $"无法读取本地化表：{table.TableId}"));
                return new LocalizationTableContractValidationResult(table, null, diagnostics);
            }

            ValidateKeys(data, config.KeyField, diagnostics);
            return new LocalizationTableContractValidationResult(table, data, diagnostics);
        }

        private static LubanTableDescriptor FindTable(
            LubanSourceSnapshot snapshot,
            string requestedTableId,
            ICollection<LocalizationContractDiagnostic> diagnostics)
        {
            var tableId = requestedTableId?.Trim() ?? string.Empty;
            if (tableId.Length == 0)
            {
                diagnostics.Add(Error("尚未配置本地化 TableId。"));
                return null;
            }

            var table = snapshot.Tables.FirstOrDefault(candidate =>
                string.Equals(candidate.TableId, tableId, StringComparison.Ordinal));
            if (table == null)
            {
                diagnostics.Add(Error($"本地化表不存在：{tableId}"));
            }

            return table;
        }

        private static void ValidateFields(
            LubanTableDescriptor table,
            LocalizationTableImportConfig config,
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

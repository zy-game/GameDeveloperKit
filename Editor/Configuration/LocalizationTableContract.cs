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

            var locales = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in config.LocaleFields ?? new List<LocalizationLocaleField>())
            {
                if (mapping == null)
                {
                    diagnostics.Add(Error("本地化语言映射不能为空。"));
                    continue;
                }

                var locale = mapping.Locale?.Trim() ?? string.Empty;
                var fieldName = mapping.FieldName?.Trim() ?? string.Empty;
                if (locale.Length == 0 || fieldName.Length == 0)
                {
                    diagnostics.Add(Error("本地化语言映射必须同时填写 Locale 和字段名。"));
                    continue;
                }

                if (locales.Add(locale) is false)
                {
                    diagnostics.Add(Error($"本地化语言重复：{locale}"));
                }

                if (fields.Contains(fieldName) is false)
                {
                    diagnostics.Add(Error($"本地化语言 {locale} 对应字段不存在：{fieldName}"));
                }
            }

            var previewLocale = config.PreviewLocale?.Trim() ?? string.Empty;
            if (locales.Contains(previewLocale) is false)
            {
                diagnostics.Add(Error($"预览语言尚未配置字段映射：{previewLocale}"));
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

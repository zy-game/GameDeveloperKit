using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Localization;
using GameDeveloperKit.LubanConfigEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.LocalizationEditor
{
    public interface ILocalizationPackExporter
    {
        LocalizationPackExportResult Export(
            LubanTableData table,
            LocalizationProjectConfig config,
            string stagingDataDirectory);
    }

    public sealed class LocalizationPackExportResult
    {
        public LocalizationPackExportResult(
            bool success,
            IReadOnlyList<string> files,
            IReadOnlyList<LocalizationCatalogDiagnostic> diagnostics)
        {
            Success = success;
            Files = files ?? Array.Empty<string>();
            Diagnostics = diagnostics ?? Array.Empty<LocalizationCatalogDiagnostic>();
        }

        public bool Success { get; }

        public IReadOnlyList<string> Files { get; }

        public IReadOnlyList<LocalizationCatalogDiagnostic> Diagnostics { get; }
    }

    public sealed class LocalizationPackExporter : ILocalizationPackExporter
    {
        public static LocalizationPackExporter Shared { get; } = new LocalizationPackExporter();

        public LocalizationPackExportResult Export(
            LubanTableData table,
            LocalizationProjectConfig config,
            string stagingDataDirectory)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrWhiteSpace(stagingDataDirectory))
            {
                throw new ArgumentException("Staging data directory cannot be empty.", nameof(stagingDataDirectory));
            }

            var outputRoot = IOPath.Combine(IOPath.GetFullPath(stagingDataDirectory), "Localization");
            var temporaryRoot = IOPath.Combine(
                IOPath.GetFullPath(stagingDataDirectory),
                $".Localization.gdk-staging-{Guid.NewGuid():N}");
            var files = new List<string>();
            var diagnostics = new List<LocalizationCatalogDiagnostic>();
            try
            {
                IODirectory.CreateDirectory(temporaryRoot);
                ValidateMappings(config);
                var keys = ReadKeys(table, config.KeyField);
                foreach (var mapping in config.LocaleFields)
                {
                    var locale = mapping.Locale.Trim();
                    var entries = new JObject();
                    foreach (var row in table.Rows)
                    {
                        if (row.Values.TryGetValue(mapping.FieldName.Trim(), out var text) is false)
                        {
                            throw new InvalidDataException(
                                $"本地化语言 {locale} 对应字段不存在：{mapping.FieldName}");
                        }

                        entries.Add(keys[row.SourceRow], text ?? string.Empty);
                    }

                    var root = new JObject { ["entries"] = entries };
                    var json = root.ToString(Formatting.Indented);
                    var temporaryPath = IOPath.Combine(temporaryRoot, $"{locale}.json");
                    var pack = LocalizationPack.Parse(locale, json, temporaryPath);
                    pack.Release();
                    IOFile.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                    files.Add(IOPath.Combine(outputRoot, $"{locale}.json"));
                }

                if (IODirectory.Exists(outputRoot))
                {
                    IODirectory.Delete(outputRoot, true);
                }

                IODirectory.Move(temporaryRoot, outputRoot);
                return new LocalizationPackExportResult(true, files, diagnostics);
            }
            catch (Exception exception)
            {
                diagnostics.Add(new LocalizationCatalogDiagnostic(
                    LocalizationCatalogDiagnosticSeverity.Error,
                    $"生成 Runtime 本地化语言包失败：{exception.Message}"));
                DeleteDirectory(temporaryRoot);
                return new LocalizationPackExportResult(false, Array.Empty<string>(), diagnostics);
            }
        }

        private static Dictionary<int, string> ReadKeys(LubanTableData table, string keyField)
        {
            var normalizedKeyField = keyField?.Trim() ?? string.Empty;
            var keys = new Dictionary<int, string>();
            var sourceRows = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in table.Rows)
            {
                if (row.Values.TryGetValue(normalizedKeyField, out var rawKey) is false)
                {
                    throw new InvalidDataException($"本地化 Key 字段不存在：{normalizedKeyField}");
                }

                var key = rawKey?.Trim() ?? string.Empty;
                if (key.Length == 0)
                {
                    throw new InvalidDataException($"本地化 Key 不能为空，源行：{row.SourceRow}");
                }

                if (sourceRows.TryGetValue(key, out var firstRow))
                {
                    throw new InvalidDataException(
                        $"本地化 Key 重复：{key}，源行：{firstRow}、{row.SourceRow}");
                }

                sourceRows.Add(key, row.SourceRow);
                keys.Add(row.SourceRow, key);
            }

            return keys;
        }

        private static void ValidateMappings(LocalizationProjectConfig config)
        {
            if (config.LocaleFields == null || config.LocaleFields.Count == 0)
            {
                throw new InvalidDataException("本地化语言字段映射不能为空。");
            }

            var locales = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in config.LocaleFields)
            {
                if (mapping == null ||
                    string.IsNullOrWhiteSpace(mapping.Locale) ||
                    string.IsNullOrWhiteSpace(mapping.FieldName))
                {
                    throw new InvalidDataException("本地化语言映射必须同时填写 Locale 和字段名。");
                }

                var locale = mapping.Locale.Trim();
                if (locale.IndexOfAny(IOPath.GetInvalidFileNameChars()) >= 0 || locales.Add(locale) is false)
                {
                    throw new InvalidDataException($"本地化语言无效或重复：{locale}");
                }
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (IODirectory.Exists(path))
            {
                IODirectory.Delete(path, true);
            }
        }
    }
}

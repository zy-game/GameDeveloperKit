using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using IOFile = System.IO.File;

namespace GameDeveloperKit.LocalizationEditor
{
    internal sealed class LocalizationImportBaselineEntry
    {
        public string SourceId { get; set; }

        public string TableId { get; set; }

        public string SourceField { get; set; }

        public string TargetLocale { get; set; }

        public long KeyId { get; set; }

        public string Key { get; set; }

        public string BaseValue { get; set; }

        public long SourceRevision { get; set; }
    }

    internal sealed class LocalizationImportBaselineDocument
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string CatalogId { get; set; }

        public List<LocalizationImportBaselineEntry> Entries { get; set; } =
            new List<LocalizationImportBaselineEntry>();
    }

    internal sealed class LocalizationImportBaselineLoadResult
    {
        public LocalizationImportBaselineLoadResult(
            LocalizationImportBaselineDocument document,
            IEnumerable<LocalizationImportDiagnostic> diagnostics)
        {
            Document = document;
            Diagnostics = (diagnostics ?? Array.Empty<LocalizationImportDiagnostic>()).ToArray();
        }

        public LocalizationImportBaselineDocument Document { get; }

        public IReadOnlyList<LocalizationImportDiagnostic> Diagnostics { get; }

        public bool IsValid => Diagnostics.All(diagnostic =>
            diagnostic.Severity != LocalizationImportDiagnosticSeverity.Error);
    }

    internal interface ILocalizationImportBaselineStore
    {
        string GetPath(string catalogId);

        LocalizationImportBaselineLoadResult Load(string catalogId);

        string Serialize(LocalizationImportBaselineDocument document);

        LocalizationImportBaselineFileBackup Capture(string path);

        void Write(string path, string content);

        void Restore(string path, LocalizationImportBaselineFileBackup backup);
    }

    internal sealed class LocalizationImportBaselineStore : ILocalizationImportBaselineStore
    {
        internal const string RelativeRoot = "ProjectSettings/GameDeveloperKit/LocalizationImports";

        public static LocalizationImportBaselineStore Shared { get; } = new LocalizationImportBaselineStore();

        public string GetPath(string catalogId)
        {
            var name = SanitizeFileName(catalogId);
            if (name.Length == 0)
            {
                name = "catalog";
            }

            return $"{RelativeRoot}/{name}.json";
        }

        public LocalizationImportBaselineLoadResult Load(string catalogId)
        {
            var path = GetPath(catalogId);
            if (IOFile.Exists(path) is false)
            {
                return new LocalizationImportBaselineLoadResult(
                    new LocalizationImportBaselineDocument { CatalogId = catalogId },
                    Array.Empty<LocalizationImportDiagnostic>());
            }

            try
            {
                var document = JsonConvert.DeserializeObject<LocalizationImportBaselineDocument>(
                    IOFile.ReadAllText(path, Encoding.UTF8));
                return Validate(document, catalogId);
            }
            catch (Exception exception)
            {
                return Failure(catalogId, $"读取本地化导入 Baseline 失败：{exception.Message}");
            }
        }

        public string Serialize(LocalizationImportBaselineDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            document.Entries = (document.Entries ?? new List<LocalizationImportBaselineEntry>())
                .OrderBy(entry => entry.SourceId, StringComparer.Ordinal)
                .ThenBy(entry => entry.TableId, StringComparer.Ordinal)
                .ThenBy(entry => entry.TargetLocale, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.SourceField, StringComparer.Ordinal)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .ToList();
            return JsonConvert.SerializeObject(document, Formatting.Indented);
        }

        public LocalizationImportBaselineFileBackup Capture(string path)
        {
            ValidatePath(path);
            return IOFile.Exists(path)
                ? new LocalizationImportBaselineFileBackup(true, IOFile.ReadAllBytes(path))
                : new LocalizationImportBaselineFileBackup(false, null);
        }

        public void Write(string path, string content)
        {
            ValidatePath(path);
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath) ??
                            throw new InvalidOperationException("Baseline 路径缺少父目录。");
            Directory.CreateDirectory(directory);
            var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                IOFile.WriteAllText(tempPath, content ?? string.Empty, new UTF8Encoding(false));
                if (IOFile.Exists(fullPath))
                {
                    IOFile.Replace(tempPath, fullPath, null);
                }
                else
                {
                    IOFile.Move(tempPath, fullPath);
                }
            }
            finally
            {
                if (IOFile.Exists(tempPath))
                {
                    IOFile.Delete(tempPath);
                }
            }
        }

        public void Restore(string path, LocalizationImportBaselineFileBackup backup)
        {
            ValidatePath(path);
            if (backup.Existed)
            {
                var fullPath = Path.GetFullPath(path);
                var directory = Path.GetDirectoryName(fullPath) ??
                                throw new InvalidOperationException("Baseline 路径缺少父目录。");
                Directory.CreateDirectory(directory);
                var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.restore");
                try
                {
                    IOFile.WriteAllBytes(tempPath, backup.Bytes ?? Array.Empty<byte>());
                    if (IOFile.Exists(fullPath))
                    {
                        IOFile.Replace(tempPath, fullPath, null);
                    }
                    else
                    {
                        IOFile.Move(tempPath, fullPath);
                    }
                }
                finally
                {
                    if (IOFile.Exists(tempPath))
                    {
                        IOFile.Delete(tempPath);
                    }
                }

                return;
            }

            if (IOFile.Exists(path))
            {
                IOFile.Delete(path);
            }
        }

        private static LocalizationImportBaselineLoadResult Validate(
            LocalizationImportBaselineDocument document,
            string catalogId)
        {
            var diagnostics = new List<LocalizationImportDiagnostic>();
            if (document == null)
            {
                return Failure(catalogId, "本地化导入 Baseline 内容为空。");
            }

            if (document.SchemaVersion != LocalizationImportBaselineDocument.CurrentSchemaVersion)
            {
                diagnostics.Add(Error($"本地化导入 Baseline 版本不受支持：{document.SchemaVersion}"));
            }

            if (string.Equals(document.CatalogId, catalogId, StringComparison.Ordinal) is false)
            {
                diagnostics.Add(Error($"本地化导入 Baseline CatalogId 不匹配：{document.CatalogId}"));
            }

            document.Entries ??= new List<LocalizationImportBaselineEntry>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in document.Entries)
            {
                if (entry == null || entry.KeyId <= 0 || string.IsNullOrWhiteSpace(entry.Key) ||
                    string.IsNullOrWhiteSpace(entry.SourceId) || string.IsNullOrWhiteSpace(entry.TableId) ||
                    string.IsNullOrWhiteSpace(entry.SourceField) || string.IsNullOrWhiteSpace(entry.TargetLocale))
                {
                    diagnostics.Add(Error("本地化导入 Baseline 存在无效条目。"));
                    continue;
                }

                entry.TargetLocale = LocalizationAuthoringService.NormalizeLocale(entry.TargetLocale);
                entry.BaseValue ??= string.Empty;
                var identity = string.Join("\n", entry.SourceId, entry.TableId, entry.SourceField,
                    entry.TargetLocale, entry.KeyId.ToString());
                if (identities.Add(identity) is false)
                {
                    diagnostics.Add(Error($"本地化导入 Baseline 条目重复：{entry.Key}/{entry.TargetLocale}"));
                }
            }

            return new LocalizationImportBaselineLoadResult(document, diagnostics);
        }

        private static LocalizationImportBaselineLoadResult Failure(string catalogId, string message)
        {
            return new LocalizationImportBaselineLoadResult(
                new LocalizationImportBaselineDocument { CatalogId = catalogId },
                new[] { Error(message) });
        }

        private static LocalizationImportDiagnostic Error(string message)
        {
            return new LocalizationImportDiagnostic(
                LocalizationImportDiagnosticSeverity.Error,
                "baseline_invalid",
                message);
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Baseline 路径不能为空。", nameof(path));
            }

            var root = Path.GetFullPath(RelativeRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) is false ||
                string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new ArgumentException("Baseline 路径必须位于项目 LocalizationImports 目录。", nameof(path));
            }
        }

        private static string SanitizeFileName(string value)
        {
            var result = value?.Trim() ?? string.Empty;
            foreach (var character in Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }))
            {
                result = result.Replace(character, '_');
            }

            return result;
        }
    }

    internal readonly struct LocalizationImportBaselineFileBackup
    {
        public LocalizationImportBaselineFileBackup(bool existed, byte[] bytes)
        {
            Existed = existed;
            Bytes = bytes;
        }

        public bool Existed { get; }

        public byte[] Bytes { get; }
    }
}

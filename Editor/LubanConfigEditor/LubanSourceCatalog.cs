using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using GameDeveloperKit.EditorConfiguration;
using IOFile = System.IO.File;

namespace GameDeveloperKit.LubanConfigEditor
{
    public interface ILubanSourceCatalog
    {
        LubanSourceSnapshot Refresh(LubanProjectConfig config);

        bool TryReadTable(string tableId, out LubanTableData data, out LubanDiagnostic diagnostic);
    }

    public enum LubanDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class LubanDiagnostic
    {
        public LubanDiagnostic(
            LubanDiagnosticSeverity severity,
            string message,
            string sourceId = null,
            string tableId = null)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            TableId = tableId ?? string.Empty;
        }

        public LubanDiagnosticSeverity Severity { get; }

        public string Message { get; }

        public string SourceId { get; }

        public string TableId { get; }
    }

    public sealed class LubanSourceSnapshot
    {
        public LubanSourceSnapshot(
            long revision,
            IReadOnlyList<LubanSourceDescriptor> sources,
            IReadOnlyList<LubanDiagnostic> diagnostics)
        {
            Revision = revision;
            Sources = sources ?? Array.Empty<LubanSourceDescriptor>();
            Diagnostics = diagnostics ?? Array.Empty<LubanDiagnostic>();
        }

        public long Revision { get; }

        public IReadOnlyList<LubanSourceDescriptor> Sources { get; }

        public IReadOnlyList<LubanDiagnostic> Diagnostics { get; }

        public IEnumerable<LubanTableDescriptor> Tables => Sources.SelectMany(source => source.Tables);
    }

    public sealed class LubanSourceDescriptor
    {
        public LubanSourceDescriptor(
            string sourceId,
            string displayName,
            long lastWriteUtcTicks,
            IReadOnlyList<LubanTableDescriptor> tables)
        {
            SourceId = sourceId;
            DisplayName = displayName;
            LastWriteUtcTicks = lastWriteUtcTicks;
            Tables = tables ?? Array.Empty<LubanTableDescriptor>();
        }

        public string SourceId { get; }

        public string DisplayName { get; }

        public long LastWriteUtcTicks { get; }

        public IReadOnlyList<LubanTableDescriptor> Tables { get; }
    }

    public sealed class LubanTableDescriptor
    {
        public LubanTableDescriptor(
            string tableId,
            string sourceId,
            string sheetName,
            string tableName,
            IReadOnlyList<LubanFieldDescriptor> fields)
        {
            TableId = tableId;
            SourceId = sourceId;
            SheetName = sheetName;
            TableName = tableName;
            Fields = fields ?? Array.Empty<LubanFieldDescriptor>();
        }

        public string TableId { get; }

        public string SourceId { get; }

        public string SheetName { get; }

        public string TableName { get; }

        public IReadOnlyList<LubanFieldDescriptor> Fields { get; }
    }

    public sealed class LubanFieldDescriptor
    {
        public LubanFieldDescriptor(string name, string type, string comment, int sourceColumn)
        {
            Name = name;
            Type = type ?? string.Empty;
            Comment = comment ?? string.Empty;
            SourceColumn = sourceColumn;
        }

        public string Name { get; }

        public string Type { get; }

        public string Comment { get; }

        public int SourceColumn { get; }
    }

    public sealed class LubanTableData
    {
        public LubanTableData(string tableId, IReadOnlyList<LubanTableRow> rows)
        {
            TableId = tableId;
            Rows = rows ?? Array.Empty<LubanTableRow>();
        }

        public string TableId { get; }

        public IReadOnlyList<LubanTableRow> Rows { get; }
    }

    public sealed class LubanTableRow
    {
        public LubanTableRow(int sourceRow, IReadOnlyDictionary<string, string> values)
        {
            SourceRow = sourceRow;
            Values = values ?? new Dictionary<string, string>();
        }

        public int SourceRow { get; }

        public IReadOnlyDictionary<string, string> Values { get; }
    }

    public sealed class LubanSourceCatalog : ILubanSourceCatalog
    {
        private static readonly XNamespace s_SpreadsheetNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace s_RelationshipsNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace s_OfficeRelationshipsNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        private static long s_Revision;
        private readonly Dictionary<string, LubanTableData> m_TableData =
            new Dictionary<string, LubanTableData>(StringComparer.Ordinal);

        public static LubanSourceCatalog Shared { get; } = new LubanSourceCatalog();

        public LubanSourceSnapshot Refresh(LubanProjectConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var sources = new List<LubanSourceDescriptor>();
            var diagnostics = new List<LubanDiagnostic>();
            var tableData = new Dictionary<string, LubanTableData>(StringComparer.Ordinal);
            var tableRoot = LubanCommandRunner.GetAbsoluteProjectPath(config.TableDirectory);
            var dataRoot = ResolveDataRoot(tableRoot, diagnostics);
            if (Directory.Exists(dataRoot) is false)
            {
                diagnostics.Add(new LubanDiagnostic(
                    LubanDiagnosticSeverity.Error,
                    $"配置表数据目录不存在：{LubanCommandRunner.ToProjectRelativePath(dataRoot)}"));
                return CommitSnapshot(sources, diagnostics, tableData);
            }

            var sourcePaths = Directory.GetFiles(dataRoot, "*.xlsx", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).StartsWith("~$", StringComparison.OrdinalIgnoreCase) is false)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var sourcePath in sourcePaths)
            {
                var sourceId = LubanCommandRunner.ToProjectRelativePath(sourcePath).Replace('\\', '/');
                try
                {
                    sources.Add(ReadSource(sourcePath, sourceId, tableData, diagnostics));
                }
                catch (Exception exception)
                {
                    diagnostics.Add(new LubanDiagnostic(
                        LubanDiagnosticSeverity.Error,
                        $"读取 Excel 失败：{exception.Message}",
                        sourceId));
                    sources.Add(new LubanSourceDescriptor(
                        sourceId,
                        Path.GetFileName(sourcePath),
                        IOFile.GetLastWriteTimeUtc(sourcePath).Ticks,
                        Array.Empty<LubanTableDescriptor>()));
                }
            }

            if (sourcePaths.Length == 0)
            {
                diagnostics.Add(new LubanDiagnostic(
                    LubanDiagnosticSeverity.Warning,
                    $"配置表数据目录中没有 xlsx：{LubanCommandRunner.ToProjectRelativePath(dataRoot)}"));
            }

            return CommitSnapshot(sources, diagnostics, tableData);
        }

        public bool TryReadTable(string tableId, out LubanTableData data, out LubanDiagnostic diagnostic)
        {
            data = null;
            diagnostic = null;
            if (string.IsNullOrWhiteSpace(tableId))
            {
                diagnostic = new LubanDiagnostic(LubanDiagnosticSeverity.Error, "TableId 不能为空。");
                return false;
            }

            if (m_TableData.TryGetValue(tableId.Trim(), out data))
            {
                return true;
            }

            diagnostic = new LubanDiagnostic(
                LubanDiagnosticSeverity.Error,
                $"当前 Source Catalog 中不存在配置表：{tableId}",
                tableId: tableId);
            return false;
        }

        private static string ResolveDataRoot(string tableRoot, ICollection<LubanDiagnostic> diagnostics)
        {
            var confPath = Path.Combine(tableRoot, "luban.conf");
            if (IOFile.Exists(confPath) is false)
            {
                diagnostics.Add(new LubanDiagnostic(
                    LubanDiagnosticSeverity.Error,
                    $"缺少 luban.conf：{LubanCommandRunner.ToProjectRelativePath(confPath)}"));
                return Path.Combine(tableRoot, "Datas");
            }

            try
            {
                var conf = LubanConfModel.Load(confPath);
                return Path.IsPathRooted(conf.DataDirectory)
                    ? Path.GetFullPath(conf.DataDirectory)
                    : Path.GetFullPath(Path.Combine(conf.WorkspaceRoot, conf.DataDirectory));
            }
            catch (Exception exception)
            {
                diagnostics.Add(new LubanDiagnostic(
                    LubanDiagnosticSeverity.Error,
                    $"解析 luban.conf 失败：{exception.Message}"));
                return Path.Combine(tableRoot, "Datas");
            }
        }

        private LubanSourceSnapshot CommitSnapshot(
            IReadOnlyList<LubanSourceDescriptor> sources,
            IReadOnlyList<LubanDiagnostic> diagnostics,
            IReadOnlyDictionary<string, LubanTableData> tableData)
        {
            m_TableData.Clear();
            foreach (var pair in tableData)
            {
                m_TableData.Add(pair.Key, pair.Value);
            }

            return new LubanSourceSnapshot(
                Interlocked.Increment(ref s_Revision),
                sources,
                diagnostics);
        }

        private static LubanSourceDescriptor ReadSource(
            string sourcePath,
            string sourceId,
            IDictionary<string, LubanTableData> tableData,
            ICollection<LubanDiagnostic> diagnostics)
        {
            var tables = new List<LubanTableDescriptor>();
            using (var archive = ZipFile.OpenRead(sourcePath))
            {
                var sharedStrings = ReadSharedStrings(archive);
                var workbook = LoadXml(archive, "xl/workbook.xml");
                var relationships = ReadWorkbookRelationships(archive);
                foreach (var sheetElement in workbook.Descendants(s_SpreadsheetNamespace + "sheet"))
                {
                    var sheetName = sheetElement.Attribute("name")?.Value;
                    var relationshipId = sheetElement.Attribute(s_OfficeRelationshipsNamespace + "id")?.Value;
                    if (string.IsNullOrWhiteSpace(sheetName) ||
                        string.IsNullOrWhiteSpace(relationshipId) ||
                        relationships.TryGetValue(relationshipId, out var target) is false)
                    {
                        continue;
                    }

                    var sheetPath = target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                        ? target
                        : $"xl/{target.TrimStart('/')}";
                    var rows = ReadRows(LoadXml(archive, sheetPath), sharedStrings);
                    if (TryBuildTable(sourceId, sheetName, rows, out var descriptor, out var data) is false)
                    {
                        diagnostics.Add(new LubanDiagnostic(
                            LubanDiagnosticSeverity.Warning,
                            $"Sheet '{sheetName}' 缺少有效 ##var 字段声明。",
                            sourceId));
                        continue;
                    }

                    if (tableData.ContainsKey(descriptor.TableId))
                    {
                        diagnostics.Add(new LubanDiagnostic(
                            LubanDiagnosticSeverity.Error,
                            $"TableId 重复：{descriptor.TableId}",
                            sourceId,
                            descriptor.TableId));
                        continue;
                    }

                    tables.Add(descriptor);
                    tableData.Add(descriptor.TableId, data);
                }
            }

            return new LubanSourceDescriptor(
                sourceId,
                Path.GetFileName(sourcePath),
                IOFile.GetLastWriteTimeUtc(sourcePath).Ticks,
                tables);
        }

        private static bool TryBuildTable(
            string sourceId,
            string sheetName,
            IReadOnlyList<WorkbookRow> rows,
            out LubanTableDescriptor descriptor,
            out LubanTableData data)
        {
            descriptor = null;
            data = null;
            var variableRow = rows.FirstOrDefault(row =>
                row.Cells.TryGetValue(1, out var marker) &&
                string.Equals(marker, "##var", StringComparison.OrdinalIgnoreCase));
            if (variableRow == null)
            {
                return false;
            }

            var typeRow = FindMarkerRow(rows, "##type");
            var commentRow = FindMarkerRow(rows, "##");
            var fields = variableRow.Cells
                .Where(pair => pair.Key > 1 && string.IsNullOrWhiteSpace(pair.Value) is false &&
                               pair.Value.StartsWith("##", StringComparison.Ordinal) is false)
                .OrderBy(pair => pair.Key)
                .Select(pair => new LubanFieldDescriptor(
                    pair.Value.Trim(),
                    GetCell(typeRow, pair.Key),
                    GetCell(commentRow, pair.Key),
                    pair.Key))
                .ToArray();
            if (fields.Length == 0)
            {
                return false;
            }

            var tableName = $"Tb{ToPascalCase(sheetName)}";
            var tableId = $"{sourceId}#{sheetName}#{tableName}";
            var tableRows = new List<LubanTableRow>();
            foreach (var row in rows.Where(row => row.Number > variableRow.Number))
            {
                if (row.Cells.TryGetValue(1, out var marker) && marker.StartsWith("##", StringComparison.Ordinal))
                {
                    continue;
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                var hasValue = false;
                foreach (var field in fields)
                {
                    var value = GetCell(row, field.SourceColumn);
                    values.Add(field.Name, value);
                    hasValue |= string.IsNullOrWhiteSpace(value) is false;
                }

                if (hasValue)
                {
                    tableRows.Add(new LubanTableRow(row.Number, values));
                }
            }

            descriptor = new LubanTableDescriptor(tableId, sourceId, sheetName, tableName, fields);
            data = new LubanTableData(tableId, tableRows);
            return true;
        }

        private static WorkbookRow FindMarkerRow(IEnumerable<WorkbookRow> rows, string marker)
        {
            return rows.FirstOrDefault(row =>
                row.Cells.TryGetValue(1, out var value) &&
                string.Equals(value, marker, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetCell(WorkbookRow row, int column)
        {
            return row != null && row.Cells.TryGetValue(column, out var value) ? value : string.Empty;
        }

        private static IReadOnlyList<WorkbookRow> ReadRows(
            XDocument worksheet,
            IReadOnlyList<string> sharedStrings)
        {
            var rows = new List<WorkbookRow>();
            foreach (var rowElement in worksheet.Descendants(s_SpreadsheetNamespace + "row"))
            {
                var number = int.TryParse(rowElement.Attribute("r")?.Value, out var parsedNumber)
                    ? parsedNumber
                    : rows.Count + 1;
                var cells = new Dictionary<int, string>();
                foreach (var cell in rowElement.Elements(s_SpreadsheetNamespace + "c"))
                {
                    var column = GetColumnIndex(cell.Attribute("r")?.Value);
                    if (column > 0)
                    {
                        cells[column] = ReadCellValue(cell, sharedStrings);
                    }
                }

                rows.Add(new WorkbookRow(number, cells));
            }

            return rows;
        }

        private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return Array.Empty<string>();
            }

            var document = LoadXml(entry);
            return document.Descendants(s_SpreadsheetNamespace + "si")
                .Select(item => string.Concat(item.Descendants(s_SpreadsheetNamespace + "t").Select(text => text.Value)))
                .ToArray();
        }

        private static IReadOnlyDictionary<string, string> ReadWorkbookRelationships(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (entry == null)
            {
                return new Dictionary<string, string>();
            }

            return LoadXml(entry)
                .Descendants(s_RelationshipsNamespace + "Relationship")
                .Where(element => string.IsNullOrWhiteSpace(element.Attribute("Id")?.Value) is false)
                .ToDictionary(
                    element => element.Attribute("Id")!.Value,
                    element => element.Attribute("Target")?.Value ?? string.Empty,
                    StringComparer.Ordinal);
        }

        private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
        {
            var type = cell.Attribute("t")?.Value;
            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(cell.Descendants(s_SpreadsheetNamespace + "t").Select(element => element.Value));
            }

            var value = cell.Element(s_SpreadsheetNamespace + "v")?.Value ?? string.Empty;
            if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value, out var index) &&
                index >= 0 &&
                index < sharedStrings.Count)
            {
                return sharedStrings[index];
            }

            return value;
        }

        private static int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return 0;
            }

            var result = 0;
            foreach (var character in cellReference)
            {
                if (char.IsLetter(character) is false)
                {
                    break;
                }

                result = result * 26 + char.ToUpperInvariant(character) - 'A' + 1;
            }

            return result;
        }

        private static XDocument LoadXml(ZipArchive archive, string entryName)
        {
            var entry = archive.GetEntry(entryName) ??
                        throw new FileNotFoundException($"Excel 内缺少条目：{entryName}");
            return LoadXml(entry);
        }

        private static XDocument LoadXml(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static string ToPascalCase(string value)
        {
            var parts = (value ?? string.Empty)
                .Split(new[] { '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(part =>
                part.Length == 0 ? string.Empty : char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        private sealed class WorkbookRow
        {
            public WorkbookRow(int number, IReadOnlyDictionary<int, string> cells)
            {
                Number = number;
                Cells = cells;
            }

            public int Number { get; }

            public IReadOnlyDictionary<int, string> Cells { get; }
        }
    }
}

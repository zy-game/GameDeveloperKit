using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GameDeveloperKit.LubanConfigEditor
{
    /// <summary>
    /// 定义 Luban Table Index 类型。
    /// </summary>
    public sealed class LubanTableIndex
    {
        private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static readonly XNamespace RelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

        private static readonly XNamespace OfficeRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        private readonly List<LubanTableDefinition> m_Tables = new List<LubanTableDefinition>();

        private LubanTableIndex()
        {
        }

        public string Target { get; private set; }

        public string TopModule { get; private set; }

        public string Manager { get; private set; }

        /// <summary>
        /// 存储 Tables。
        /// </summary>
        public IReadOnlyList<LubanTableDefinition> Tables => m_Tables;

        /// <summary>
        /// 扫描。
        /// </summary>
        /// <param name="confModel">conf Model 参数。</param>
        /// <param name="workspace">workspace 参数。</param>
        /// <param name="profile">profile 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanTableIndex Scan(LubanConfModel confModel, LubanWorkspaceProfile workspace, LubanGenerationProfile profile)
        {
            if (confModel == null)
            {
                throw new ArgumentNullException(nameof(confModel));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var target = string.IsNullOrWhiteSpace(profile.Target) ? workspace.DefaultTarget : profile.Target.Trim();
            var index = new LubanTableIndex
            {
                Target = target,
                TopModule = confModel.GetTargetTopModule(target),
                Manager = confModel.GetTargetManager(target)
            };
            var generatedTables = LoadGeneratedTables(profile, index.TopModule);
            foreach (var sourcePath in EnumerateTableSourcePaths(confModel))
            {
                index.m_Tables.AddRange(ReadExcelInlineTables(sourcePath, confModel, profile, index.TopModule, generatedTables));
            }

            index.ReconcileOnlyGeneratedTables(generatedTables, profile);
            return index;
        }

        /// <summary>
        /// 枚举 Table Source Paths。
        /// </summary>
        /// <param name="confModel">conf Model 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<string> EnumerateTableSourcePaths(LubanConfModel confModel)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var schemaFile in confModel.SchemaFiles)
            {
                foreach (var path in EnumerateSourcePath(confModel.WorkspaceRoot, schemaFile))
                {
                    paths.Add(path);
                }
            }

            foreach (var path in EnumerateSourcePath(confModel.WorkspaceRoot, confModel.DataDirectory))
            {
                paths.Add(path);
            }

            return paths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 枚举 Source Path。
        /// </summary>
        /// <param name="workspaceRoot">workspace Root 参数。</param>
        /// <param name="path">path 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<string> EnumerateSourcePath(string workspaceRoot, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                yield break;
            }

            var absolutePath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(workspaceRoot, path));
            if (System.IO.File.Exists(absolutePath))
            {
                if (IsExcelInlineSource(absolutePath))
                {
                    yield return absolutePath;
                }

                yield break;
            }

            if (Directory.Exists(absolutePath) is false)
            {
                yield break;
            }

            foreach (var filePath in Directory.GetFiles(absolutePath, "*.xlsx", SearchOption.AllDirectories))
            {
                if (IsExcelInlineSource(filePath))
                {
                    yield return Path.GetFullPath(filePath);
                }
            }
        }

        /// <summary>
        /// 是否 Excel Inline Source。
        /// </summary>
        /// <param name="path">path 参数。</param>
        /// <returns>执行结果。</returns>
        private static bool IsExcelInlineSource(string path)
        {
            var fileName = Path.GetFileName(path);
            return fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase) is false
                && fileName.StartsWith("#", StringComparison.Ordinal);
        }

        /// <summary>
        /// 读取 Excel Inline Tables。
        /// </summary>
        /// <param name="sourcePath">source Path 参数。</param>
        /// <param name="confModel">conf Model 参数。</param>
        /// <param name="profile">profile 参数。</param>
        /// <param name="topModule">top Module 参数。</param>
        /// <param name="generatedTables">generated Tables 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<LubanTableDefinition> ReadExcelInlineTables(
            string sourcePath,
            LubanConfModel confModel,
            LubanGenerationProfile profile,
            string topModule,
            IReadOnlyDictionary<string, GeneratedTableInfo> generatedTables)
        {
            foreach (var sheet in ReadWorkbookSheets(sourcePath))
            {
                var fields = ReadFields(sheet.Rows);
                if (fields.Count == 0)
                {
                    continue;
                }

                var rowLocalName = sheet.Name;
                var rowTypeName = $"{topModule}.{rowLocalName}";
                var generatedTable = generatedTables.Values.FirstOrDefault(x =>
                    string.Equals(x.RowTypeName, rowTypeName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.RowLocalName, rowLocalName, StringComparison.OrdinalIgnoreCase));
                var tableName = generatedTable?.TableName ?? $"Tb{ToPascalCase(rowLocalName)}";
                var dataKey = generatedTable?.DataKey ?? ToDataKey(tableName);
                yield return new LubanTableDefinition
                {
                    TableName = tableName,
                    DataKey = dataKey,
                    RowTypeName = generatedTable?.RowTypeName ?? rowTypeName,
                    SourcePath = LubanCommandRunner.ToProjectRelativePath(sourcePath),
                    SourceKind = "ExcelInline",
                    InputName = rowLocalName,
                    Groups = fields.SelectMany(x => x.Groups).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray(),
                    KeyOrIndex = generatedTable?.KeyOrIndex ?? GuessKey(fields),
                    Fields = fields,
                    ConfigPathCandidate = GetConfigPathCandidate(profile, dataKey)
                };
            }
        }

        /// <summary>
        /// 读取 Fields。
        /// </summary>
        /// <param name="rows">rows 参数。</param>
        /// <returns>执行结果。</returns>
        private static IReadOnlyList<LubanFieldDefinition> ReadFields(IReadOnlyDictionary<string, Dictionary<int, string>> rows)
        {
            if (rows.TryGetValue("##var", out var varRow) is false)
            {
                return Array.Empty<LubanFieldDefinition>();
            }

            rows.TryGetValue("##type", out var typeRow);
            rows.TryGetValue("##group", out var groupRow);
            rows.TryGetValue("##", out var commentRow);

            var fields = new List<LubanFieldDefinition>();
            foreach (var column in varRow.Keys.Where(x => x > 1).OrderBy(x => x))
            {
                var variableName = varRow.TryGetValue(column, out var name) ? name : string.Empty;
                if (string.IsNullOrWhiteSpace(variableName) || variableName.StartsWith("##", StringComparison.Ordinal))
                {
                    continue;
                }

                var type = typeRow != null && typeRow.TryGetValue(column, out var typeValue) ? typeValue : string.Empty;
                var group = groupRow != null && groupRow.TryGetValue(column, out var groupValue) ? groupValue : string.Empty;
                var comment = commentRow != null && commentRow.TryGetValue(column, out var commentValue) ? commentValue : string.Empty;
                fields.Add(new LubanFieldDefinition
                {
                    VariableName = variableName,
                    Type = type,
                    Groups = SplitGroups(group).ToArray(),
                    Comment = comment,
                    KeyParticipant = string.Equals(variableName, "id", StringComparison.OrdinalIgnoreCase)
                });
            }

            return fields;
        }

        /// <summary>
        /// 拆分 Groups。
        /// </summary>
        /// <param name="groups">groups 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<string> SplitGroups(string groups)
        {
            if (string.IsNullOrWhiteSpace(groups))
            {
                return Enumerable.Empty<string>();
            }

            return groups
                .Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => string.IsNullOrWhiteSpace(x) is false);
        }

        /// <summary>
        /// 读取 Workbook Sheets。
        /// </summary>
        /// <param name="sourcePath">source Path 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<WorkbookSheet> ReadWorkbookSheets(string sourcePath)
        {
            using (var archive = ZipFile.OpenRead(sourcePath))
            {
                var sharedStrings = ReadSharedStrings(archive);
                var workbook = LoadXml(archive, "xl/workbook.xml");
                var relationshipMap = ReadWorkbookRelationships(archive);
                foreach (var sheetElement in workbook.Descendants(SpreadsheetNamespace + "sheet"))
                {
                    var sheetName = sheetElement.Attribute("name")?.Value;
                    var relationshipId = sheetElement.Attribute(OfficeRelationshipsNamespace + "id")?.Value;
                    if (string.IsNullOrWhiteSpace(sheetName)
                        || string.IsNullOrWhiteSpace(relationshipId)
                        || relationshipMap.TryGetValue(relationshipId, out var target) is false)
                    {
                        continue;
                    }

                    var sheetPath = target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                        ? target
                        : $"xl/{target.TrimStart('/')}";
                    var worksheet = LoadXml(archive, sheetPath);
                    yield return new WorkbookSheet(sheetName, ReadRows(worksheet, sharedStrings));
                }
            }
        }

        /// <summary>
        /// 读取 Shared Strings。
        /// </summary>
        /// <param name="archive">archive 参数。</param>
        /// <returns>执行结果。</returns>
        private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return Array.Empty<string>();
            }

            var document = LoadXml(entry);
            return document
                .Descendants(SpreadsheetNamespace + "si")
                .Select(x => string.Concat(x.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
                .ToArray();
        }

        /// <summary>
        /// 读取 Workbook Relationships。
        /// </summary>
        /// <param name="archive">archive 参数。</param>
        /// <returns>执行结果。</returns>
        private static IReadOnlyDictionary<string, string> ReadWorkbookRelationships(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (entry == null)
            {
                return new Dictionary<string, string>();
            }

            var document = LoadXml(entry);
            return document
                .Descendants(RelationshipsNamespace + "Relationship")
                .Where(x => string.IsNullOrWhiteSpace(x.Attribute("Id")?.Value) is false)
                .ToDictionary(x => x.Attribute("Id")!.Value, x => x.Attribute("Target")?.Value ?? string.Empty);
        }

        /// <summary>
        /// 读取 Rows。
        /// </summary>
        /// <param name="worksheet">worksheet 参数。</param>
        /// <param name="sharedStrings">shared Strings 参数。</param>
        /// <returns>执行结果。</returns>
        private static IReadOnlyDictionary<string, Dictionary<int, string>> ReadRows(XDocument worksheet, IReadOnlyList<string> sharedStrings)
        {
            var rows = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in worksheet.Descendants(SpreadsheetNamespace + "row"))
            {
                var cells = new Dictionary<int, string>();
                foreach (var cell in row.Elements(SpreadsheetNamespace + "c"))
                {
                    var cellReference = cell.Attribute("r")?.Value;
                    var column = GetColumnIndex(cellReference);
                    if (column <= 0)
                    {
                        continue;
                    }

                    cells[column] = ReadCellValue(cell, sharedStrings);
                }

                if (cells.TryGetValue(1, out var marker) && marker.StartsWith("##", StringComparison.Ordinal))
                {
                    rows[marker] = cells;
                }
            }

            return rows;
        }

        /// <summary>
        /// 读取 Cell Value。
        /// </summary>
        /// <param name="cell">cell 参数。</param>
        /// <param name="sharedStrings">shared Strings 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
        {
            var value = cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
            var type = cell.Attribute("t")?.Value;
            if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value, out var sharedStringIndex)
                && sharedStringIndex >= 0
                && sharedStringIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedStringIndex];
            }

            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(x => x.Value));
            }

            return value;
        }

        /// <summary>
        /// 获取 Column Index。
        /// </summary>
        /// <param name="cellReference">cell Reference 参数。</param>
        /// <returns>执行结果。</returns>
        private static int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return 0;
            }

            var column = 0;
            foreach (var character in cellReference)
            {
                if (char.IsLetter(character) is false)
                {
                    break;
                }

                column = column * 26 + char.ToUpperInvariant(character) - 'A' + 1;
            }

            return column;
        }

        /// <summary>
        /// 加载 Xml。
        /// </summary>
        /// <param name="archive">archive 参数。</param>
        /// <param name="entryName">entry Name 参数。</param>
        /// <returns>执行结果。</returns>
        private static XDocument LoadXml(ZipArchive archive, string entryName)
        {
            var entry = archive.GetEntry(entryName);
            if (entry == null)
            {
                throw new FileNotFoundException($"Missing workbook entry: {entryName}");
            }

            return LoadXml(entry);
        }

        /// <summary>
        /// 加载 Xml。
        /// </summary>
        /// <param name="entry">entry 参数。</param>
        /// <returns>执行结果。</returns>
        private static XDocument LoadXml(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        /// <summary>
        /// 加载 Generated Tables。
        /// </summary>
        /// <param name="profile">profile 参数。</param>
        /// <param name="topModule">top Module 参数。</param>
        /// <returns>执行结果。</returns>
        private static IReadOnlyDictionary<string, GeneratedTableInfo> LoadGeneratedTables(LubanGenerationProfile profile, string topModule)
        {
            var codeDirectory = LubanCommandRunner.GetAbsoluteProjectPath(profile.OutputCodeDirectory);
            var generatedTables = new Dictionary<string, GeneratedTableInfo>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(codeDirectory) is false)
            {
                return generatedTables;
            }

            var tableDataKeys = ReadTableDataKeys(Path.Combine(codeDirectory, "Tables.cs"));
            foreach (var filePath in Directory.GetFiles(codeDirectory, "*.cs"))
            {
                var text = System.IO.File.ReadAllText(filePath);
                var classMatch = Regex.Match(text, @"public\s+(?:sealed\s+)?partial\s+class\s+(?<class>[A-Za-z_][A-Za-z0-9_]*)");
                var rowMatch = Regex.Match(text, @"(?:IReadOnlyList|List|Dictionary)<[^>]*,\s*(?<row>[A-Za-z_][A-Za-z0-9_]*)>|(?:IReadOnlyList|List)<(?<row2>[A-Za-z_][A-Za-z0-9_]*)>");
                if (classMatch.Success is false || rowMatch.Success is false)
                {
                    continue;
                }

                var tableName = classMatch.Groups["class"].Value;
                var rowLocalName = rowMatch.Groups["row"].Success ? rowMatch.Groups["row"].Value : rowMatch.Groups["row2"].Value;
                tableDataKeys.TryGetValue(tableName, out var dataKey);
                var keyMatch = Regex.Match(text, @"_dataMap\.Add\(_v\.(?<key>[A-Za-z_][A-Za-z0-9_]*),\s*_v\)");
                generatedTables[tableName] = new GeneratedTableInfo
                {
                    TableName = tableName,
                    DataKey = string.IsNullOrWhiteSpace(dataKey) ? ToDataKey(tableName) : dataKey,
                    RowLocalName = rowLocalName,
                    RowTypeName = $"{topModule}.{rowLocalName}",
                    KeyOrIndex = keyMatch.Success ? keyMatch.Groups["key"].Value : string.Empty
                };
            }

            return generatedTables;
        }

        /// <summary>
        /// 读取 Table Data Keys。
        /// </summary>
        /// <param name="tablesPath">tables Path 参数。</param>
        /// <returns>执行结果。</returns>
        private static IReadOnlyDictionary<string, string> ReadTableDataKeys(string tablesPath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (System.IO.File.Exists(tablesPath) is false)
            {
                return result;
            }

            var text = System.IO.File.ReadAllText(tablesPath);
            foreach (Match match in Regex.Matches(text, @"new\s+(?<table>[A-Za-z_][A-Za-z0-9_]*)\s*\(\s*loader\(""(?<key>[^""]+)""\)\s*\)"))
            {
                result[match.Groups["table"].Value] = match.Groups["key"].Value;
            }

            return result;
        }

        /// <summary>
        /// Reconcile Only Generated Tables。
        /// </summary>
        /// <param name="generatedTables">generated Tables 参数。</param>
        /// <param name="profile">profile 参数。</param>
        private void ReconcileOnlyGeneratedTables(IReadOnlyDictionary<string, GeneratedTableInfo> generatedTables, LubanGenerationProfile profile)
        {
            foreach (var generatedTable in generatedTables.Values)
            {
                if (m_Tables.Any(x => string.Equals(x.TableName, generatedTable.TableName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                m_Tables.Add(new LubanTableDefinition
                {
                    TableName = generatedTable.TableName,
                    DataKey = generatedTable.DataKey,
                    RowTypeName = generatedTable.RowTypeName,
                    SourcePath = string.Empty,
                    SourceKind = "ReadOnly",
                    InputName = generatedTable.RowLocalName,
                    Groups = Array.Empty<string>(),
                    KeyOrIndex = generatedTable.KeyOrIndex,
                    Fields = Array.Empty<LubanFieldDefinition>(),
                    ConfigPathCandidate = GetConfigPathCandidate(profile, generatedTable.DataKey)
                });
            }
        }

        /// <summary>
        /// 获取 Config Path Candidate。
        /// </summary>
        /// <param name="profile">profile 参数。</param>
        /// <param name="dataKey">data Key 参数。</param>
        /// <returns>执行结果。</returns>
        private static string GetConfigPathCandidate(LubanGenerationProfile profile, string dataKey)
        {
            if (string.IsNullOrWhiteSpace(profile.OutputDataDirectory) || string.IsNullOrWhiteSpace(dataKey))
            {
                return string.Empty;
            }

            return $"{profile.OutputDataDirectory.TrimEnd('/', '\\')}/{dataKey}.json";
        }

        /// <summary>
        /// 猜测 Key。
        /// </summary>
        /// <param name="fields">fields 参数。</param>
        /// <returns>执行结果。</returns>
        private static string GuessKey(IReadOnlyList<LubanFieldDefinition> fields)
        {
            return fields.FirstOrDefault(x => string.Equals(x.VariableName, "id", StringComparison.OrdinalIgnoreCase))?.VariableName
                ?? fields.FirstOrDefault()?.VariableName
                ?? string.Empty;
        }

        /// <summary>
        /// 转换为 Data Key。
        /// </summary>
        /// <param name="tableName">table Name 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ToDataKey(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return string.Empty;
            }

            return char.ToLowerInvariant(tableName[0]) + tableName.Substring(1);
        }

        /// <summary>
        /// 转换为 Pascal Case。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ToPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private sealed class GeneratedTableInfo
        {
            public string TableName { get; set; }

            public string DataKey { get; set; }

            public string RowLocalName { get; set; }

            public string RowTypeName { get; set; }

            public string KeyOrIndex { get; set; }
        }

        private sealed class WorkbookSheet
        {
            public WorkbookSheet(string name, IReadOnlyDictionary<string, Dictionary<int, string>> rows)
            {
                Name = name;
                Rows = rows;
            }

            public string Name { get; }

            public IReadOnlyDictionary<string, Dictionary<int, string>> Rows { get; }
        }
    }

    /// <summary>
    /// 定义 Luban Table Definition 类型。
    /// </summary>
    public sealed class LubanTableDefinition
    {
        public string TableName { get; set; }

        public string DataKey { get; set; }

        public string RowTypeName { get; set; }

        public string SourcePath { get; set; }

        public string SourceKind { get; set; }

        public string InputName { get; set; }

        public IReadOnlyList<string> Groups { get; set; }

        public string KeyOrIndex { get; set; }

        public IReadOnlyList<LubanFieldDefinition> Fields { get; set; }

        public string ConfigPathCandidate { get; set; }
    }

    /// <summary>
    /// 定义 Luban Field Definition 类型。
    /// </summary>
    public sealed class LubanFieldDefinition
    {
        public string VariableName { get; set; }

        public string Type { get; set; }

        public IReadOnlyList<string> Groups { get; set; }

        public string Comment { get; set; }

        public bool KeyParticipant { get; set; }
    }
}

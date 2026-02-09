using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MiniExcelLibs;
using UnityEngine;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// Excel/CSV 导入器
    /// </summary>
    public static class ExcelImporter
    {
        // 有效的Sheet名称前缀
        private static readonly string[] ValidSheetPrefixes = { "c_", "d_", "db_" };
        
        /// <summary>
        /// 从文件导入配置（支持CSV和Excel）
        /// </summary>
        public static ConfigFileInfo ImportFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.LogError($"File not found: {filePath}");
                return null;
            }
            
            var extension = Path.GetExtension(filePath).ToLower();
            
            return extension switch
            {
                ".csv" => ImportFromCsv(filePath),
                ".xlsx" or ".xls" => ImportFromExcel(filePath),
                _ => throw new NotSupportedException($"Unsupported file format: {extension}")
            };
        }
        
        /// <summary>
        /// 从CSV文件导入（CSV只有字段名行，没有类型和注释）
        /// </summary>
        public static ConfigFileInfo ImportFromCsv(string filePath)
        {
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length < 1)
            {
                Debug.LogError("CSV file must have at least 1 row (field names)");
                return null;
            }
            
            var file = new ConfigFileInfo(filePath);
            
            var className = Path.GetFileNameWithoutExtension(filePath);
            var sheet = new ConfigSheetInfo("Sheet1", className) { parentFile = file };
            
            // 第一行是字段名
            var fieldNames = ParseCsvLine(lines[0]);
            
            // CSV没有类型和注释，全部默认为string
            for (int i = 0; i < fieldNames.Count; i++)
            {
                var fieldName = fieldNames[i].Trim();
                if (string.IsNullOrEmpty(fieldName)) continue;
                
                sheet.fields.Add(new ConfigFieldInfo(fieldName, "string", ""));
            }
            
            // 读取所有数据行（从第2行开始）
            sheet.previewData = new List<List<string>>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var row = ParseCsvLine(lines[i]);
                sheet.previewData.Add(row);
            }
            
            file.sheets.Add(sheet);
            Debug.Log($"Imported CSV: {className}, {sheet.fields.Count} fields, {sheet.previewData.Count} data rows");
            
            return file;
        }
        
        /// <summary>
        /// 从Excel文件导入所有有效的Sheet
        /// </summary>
        public static ConfigFileInfo ImportFromExcel(string filePath)
        {
            try
            {
                var file = new ConfigFileInfo(filePath);
                
                var sheetNames = MiniExcel.GetSheetNames(filePath);
                
                foreach (var sheetName in sheetNames)
                {
                    // 检查是否符合前缀要求
                    if (!IsValidSheetName(sheetName))
                        continue;
                    
                    var sheet = ImportSheet(filePath, sheetName);
                    if (sheet != null)
                    {
                        sheet.parentFile = file;
                        file.sheets.Add(sheet);
                    }
                }
                
                if (file.sheets.Count == 0)
                {
                    Debug.LogWarning($"No valid sheets found in {filePath}. Sheet names should start with: {string.Join(", ", ValidSheetPrefixes)}");
                }
                else
                {
                    Debug.Log($"Imported Excel: {file.fileName}, {file.sheets.Count} sheet(s)");
                }
                
                return file;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to import Excel: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
        
        /// <summary>
        /// 检查Sheet名称是否有效
        /// </summary>
        private static bool IsValidSheetName(string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName)) return false;
            
            var lowerName = sheetName.ToLower();
            foreach (var prefix in ValidSheetPrefixes)
            {
                if (lowerName.StartsWith(prefix))
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// 从Sheet名称提取类名（去掉前缀）
        /// </summary>
        private static string ExtractClassName(string sheetName)
        {
            var lowerName = sheetName.ToLower();
            foreach (var prefix in ValidSheetPrefixes)
            {
                if (lowerName.StartsWith(prefix))
                {
                    return sheetName.Substring(prefix.Length);
                }
            }
            return sheetName;
        }
        
        /// <summary>
        /// 解析字段作用域
        /// </summary>
        private static FieldScope ParseFieldScope(string scopeStr)
        {
            if (string.IsNullOrEmpty(scopeStr)) return FieldScope.Common;
            
            var lower = scopeStr.Trim().ToLower();
            return lower switch
            {
                "client" or "c" => FieldScope.Client,
                "server" or "s" => FieldScope.Server,
                _ => FieldScope.Common
            };
        }
        
        /// <summary>
        /// 将类型别名规范化为标准类型名
        /// </summary>
        private static string NormalizeTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "string";
            
            var lower = typeName.ToLower().Trim();
            
            // 整数类型
            if (lower == "int32" || lower == "short" || lower == "int16" || lower == "byte" || lower == "sbyte" 
                || lower == "uint" || lower == "uint32" || lower == "uint16")
                return "int";
            
            if (lower == "int64" || lower == "uint64")
                return "long";
            
            // 浮点类型
            if (lower == "single" || lower == "decimal")
                return "float";
            
            // 布尔类型
            if (lower == "boolean")
                return "bool";
            
            // 字符串类型
            if (lower == "text")
                return "string";
            
            // 数组类型
            if (lower.EndsWith("[]"))
            {
                var elementType = lower.Substring(0, lower.Length - 2);
                var normalizedElement = NormalizeTypeName(elementType);
                return normalizedElement + "[]";
            }
            
            return typeName;
        }
        
        /// <summary>
        /// 检查数据行是否应该跳过（空行或以#开头）
        /// </summary>
        private static bool ShouldSkipDataRow(List<string> row)
        {
            // 空行跳过
            if (row == null || row.Count == 0) return true;
            if (row.All(s => string.IsNullOrWhiteSpace(s))) return true;
            
            // 首列以#开头的行跳过
            if (row.Count > 0 && !string.IsNullOrEmpty(row[0]) && row[0].TrimStart().StartsWith("#"))
                return true;
            
            return false;
        }
        
        /// <summary>
        /// 导入单个Sheet
        /// 表头结构：
        /// 第1行：字段名
        /// 第2行：作用域（client/server/common）
        /// 第3行：字段类型
        /// 第4行：注释
        /// 第5行起：数据行
        /// </summary>
        private static ConfigSheetInfo ImportSheet(string filePath, string sheetName)
        {
            try
            {
                var className = ExtractClassName(sheetName);
                var sheet = new ConfigSheetInfo(sheetName, className);
                
                var rows = MiniExcel.Query(filePath, useHeaderRow: false, sheetName: sheetName).ToList();
                
                if (rows.Count < 4)
                {
                    Debug.LogWarning($"Sheet '{sheetName}' must have at least 4 rows (field names, scope, types, comments)");
                    return null;
                }
                
                // 转换为字符串列表
                var allRows = new List<List<string>>();
                foreach (var row in rows)
                {
                    var rowDict = row as IDictionary<string, object>;
                    if (rowDict != null)
                    {
                        var rowData = rowDict.Values.Select(v => v?.ToString() ?? "").ToList();
                        allRows.Add(rowData);
                    }
                }
                
                if (allRows.Count < 4)
                {
                    Debug.LogWarning($"Failed to parse rows in sheet '{sheetName}'");
                    return null;
                }
                
                var fieldNames = allRows[0];
                var fieldScopes = allRows[1];
                var fieldTypes = allRows[2];
                var comments = allRows[3];
                
                // 解析字段
                for (int i = 0; i < fieldNames.Count; i++)
                {
                    var fieldName = fieldNames[i].Trim();
                    if (string.IsNullOrEmpty(fieldName)) continue;
                    
                    var scopeStr = i < fieldScopes.Count ? fieldScopes[i] : "";
                    var scope = ParseFieldScope(scopeStr);
                    var fieldType = i < fieldTypes.Count ? fieldTypes[i].Trim() : "string";
                    var comment = i < comments.Count ? comments[i].Trim() : "";
                    
                    if (string.IsNullOrEmpty(fieldType)) fieldType = "string";
                    
                    // 规范化类型名
                    fieldType = NormalizeTypeName(fieldType);
                    
                    sheet.fields.Add(new ConfigFieldInfo(fieldName, fieldType, comment, scope));
                }
                
                // 读取所有数据行（从第5行开始，索引4）
                sheet.previewData = new List<List<string>>();
                for (int i = 4; i < allRows.Count; i++)
                {
                    var row = allRows[i];
                    // 跳过空行和注释行
                    if (ShouldSkipDataRow(row)) continue;
                    
                    sheet.previewData.Add(row);
                }
                
                Debug.Log($"  Sheet: {sheetName} -> {className}, {sheet.fields.Count} fields, {sheet.previewData.Count} rows");
                return sheet;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to import sheet '{sheetName}': {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 重新加载Sheet数据
        /// </summary>
        public static void ReloadSheetData(ConfigFileInfo file, ConfigSheetInfo sheet)
        {
            if (file == null || sheet == null) return;
            if (string.IsNullOrEmpty(file.filePath) || !File.Exists(file.filePath))
            {
                sheet.previewData = null;
                return;
            }
            
            if (file.isCsv)
            {
                ReloadCsvData(file, sheet);
            }
            else
            {
                ReloadExcelSheetData(file, sheet);
            }
        }
        
        private static void ReloadCsvData(ConfigFileInfo file, ConfigSheetInfo sheet)
        {
            var lines = File.ReadAllLines(file.filePath, Encoding.UTF8);
            sheet.previewData = new List<List<string>>();
            
            // CSV数据从第2行开始
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var row = ParseCsvLine(lines[i]);
                sheet.previewData.Add(row);
            }
        }
        
        private static void ReloadExcelSheetData(ConfigFileInfo file, ConfigSheetInfo sheet)
        {
            try
            {
                var rows = MiniExcel.Query(file.filePath, useHeaderRow: false, sheetName: sheet.sheetName).ToList();
                sheet.previewData = new List<List<string>>();
                
                // Excel数据从第5行开始（索引4）
                for (int i = 4; i < rows.Count; i++)
                {
                    var rowDict = rows[i] as IDictionary<string, object>;
                    if (rowDict != null)
                    {
                        var rowData = rowDict.Values.Select(v => v?.ToString() ?? "").ToList();
                        // 跳过空行和注释行
                        if (ShouldSkipDataRow(rowData)) continue;
                        sheet.previewData.Add(rowData);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to reload data: {ex.Message}");
                sheet.previewData = null;
            }
        }
        
        /// <summary>
        /// 读取所有数据行（用于生成JSON）
        /// </summary>
        public static List<List<string>> ReadAllDataRows(ConfigFileInfo file, ConfigSheetInfo sheet)
        {
            if (file == null || sheet == null) return null;
            if (string.IsNullOrEmpty(file.filePath) || !File.Exists(file.filePath))
            {
                return null;
            }
            
            if (file.isCsv)
            {
                return ReadCsvDataRows(file.filePath);
            }
            else
            {
                return ReadExcelDataRows(file.filePath, sheet.sheetName);
            }
        }
        
        private static List<List<string>> ReadCsvDataRows(string filePath)
        {
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            var result = new List<List<string>>();
            
            // CSV数据从第2行开始
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var row = ParseCsvLine(lines[i]);
                if (row.Count > 0 && !string.IsNullOrWhiteSpace(row[0]))
                {
                    result.Add(row);
                }
            }
            return result;
        }
        
        private static List<List<string>> ReadExcelDataRows(string filePath, string sheetName)
        {
            try
            {
                var rows = MiniExcel.Query(filePath, useHeaderRow: false, sheetName: sheetName).ToList();
                var result = new List<List<string>>();
                
                // Excel数据从第5行开始（索引4）
                for (int i = 4; i < rows.Count; i++)
                {
                    var rowDict = rows[i] as IDictionary<string, object>;
                    if (rowDict != null)
                    {
                        var rowData = rowDict.Values.Select(v => v?.ToString() ?? "").ToList();
                        // 跳过空行和注释行
                        if (ShouldSkipDataRow(rowData)) continue;
                        result.Add(rowData);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read data rows: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 解析CSV行
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            
            result.Add(sb.ToString());
            return result;
        }
    }
}

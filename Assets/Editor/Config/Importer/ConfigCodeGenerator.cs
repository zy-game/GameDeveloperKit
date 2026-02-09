using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// 配置代码生成器
    /// </summary>
    public static class ConfigCodeGenerator
    {
        /// <summary>
        /// 生成配置类代码
        /// </summary>
        public static void GenerateConfigClass(ConfigFileInfo file, ConfigSheetInfo sheet)
        {
            if (file == null || sheet == null || sheet.fields.Count == 0)
            {
                Debug.LogError("Invalid file/sheet or no fields defined");
                return;
            }
            
            var sb = new StringBuilder();
            
            sb.AppendLine("// 此代码由配置导入工具自动生成");
            sb.AppendLine($"// 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"// 源文件: {file.filePath}");
            if (!file.isCsv)
            {
                sb.AppendLine($"// Sheet: {sheet.sheetName}");
            }
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using GameFramework.Config;");
            sb.AppendLine();
            sb.AppendLine("[Serializable]");
            sb.AppendLine($"public class {sheet.className} : IConfigData");
            sb.AppendLine("{");
            
            // 只生成需要导出的字段（排除服务器专属字段）
            foreach (var field in sheet.fields)
            {
                if (!field.ShouldExport) continue;
                
                if (!string.IsNullOrEmpty(field.comment))
                {
                    sb.AppendLine($"    /// <summary>");
                    sb.AppendLine($"    /// {field.comment}");
                    sb.AppendLine($"    /// </summary>");
                }
                sb.AppendLine($"    public {field.GetCSharpType()} {field.fieldName};");
                sb.AppendLine();
            }
            
            // 检查是否有主键字段（Id、Key或isKey标记的字段，且需要导出）
            var keyField = sheet.fields.Find(f => f.isKey && f.ShouldExport) 
                ?? sheet.fields.Find(f => f.fieldName.ToLower() == "id" && f.ShouldExport)
                ?? sheet.fields.Find(f => f.fieldName.ToLower() == "key" && f.ShouldExport);
            
            if (keyField != null)
            {
                sb.AppendLine($"    object IConfigData.Key => {keyField.fieldName};");
            }
            else
            {
                sb.AppendLine("    object IConfigData.Key => null; // 警告: 未找到主键字段");
            }
            
            sb.AppendLine("}");
            
            var outputPath = Path.Combine(ConfigImportData.Instance.codeOutputPath, $"{sheet.className}.cs");
            var directory = Path.GetDirectoryName(outputPath);
            
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            
            Debug.Log($"Generated config class: {outputPath}");
        }
        
        /// <summary>
        /// 生成JSON数据文件
        /// </summary>
        public static void GenerateJsonData(ConfigFileInfo file, ConfigSheetInfo sheet)
        {
            if (file == null || sheet == null || sheet.fields.Count == 0)
            {
                Debug.LogError("Invalid file/sheet or no fields defined");
                return;
            }
            
            var dataRows = ExcelImporter.ReadAllDataRows(file, sheet);
            if (dataRows == null || dataRows.Count == 0)
            {
                Debug.LogError("No data rows found");
                return;
            }
            
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"Datas\": [");
            
            // 获取需要导出的字段索引
            var exportFields = new List<(int index, ConfigFieldInfo field)>();
            for (int i = 0; i < sheet.fields.Count; i++)
            {
                if (sheet.fields[i].ShouldExport)
                {
                    exportFields.Add((i, sheet.fields[i]));
                }
            }
            
            for (int rowIndex = 0; rowIndex < dataRows.Count; rowIndex++)
            {
                var row = dataRows[rowIndex];
                sb.Append("    {");
                
                bool first = true;
                foreach (var (fieldIndex, field) in exportFields)
                {
                    var value = fieldIndex < row.Count ? row[fieldIndex] : "";
                    
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append($"\"{field.fieldName}\": ");
                    sb.Append(FormatJsonValue(value, field));
                }
                
                sb.Append("}");
                if (rowIndex < dataRows.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            
            var outputPath = Path.Combine(ConfigImportData.Instance.jsonOutputPath, $"{sheet.className}.json");
            var directory = Path.GetDirectoryName(outputPath);
            
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            
            Debug.Log($"Generated JSON data: {outputPath} ({dataRows.Count} rows)");
        }
        
        /// <summary>
        /// 格式化JSON值
        /// </summary>
        private static string FormatJsonValue(string value, ConfigFieldInfo field)
        {
            var transport = field.GetTransport();
            
            if (string.IsNullOrEmpty(value))
            {
                return transport.GetDefaultJson();
            }
            
            return transport.ToJson(value);
        }
        
        /// <summary>
        /// 生成所有（代码+数据）
        /// </summary>
        public static void GenerateAll(ConfigFileInfo file, ConfigSheetInfo sheet)
        {
            GenerateConfigClass(file, sheet);
            GenerateJsonData(file, sheet);
        }
        
        /// <summary>
        /// 生成文件的所有Sheet
        /// </summary>
        public static void GenerateAllSheetsInFile(ConfigFileInfo file)
        {
            foreach (var sheet in file.sheets)
            {
                try
                {
                    sheet.parentFile = file;
                    GenerateAll(file, sheet);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to generate {sheet.className}: {ex.Message}");
                }
            }
            
            Debug.Log($"Generated {file.sheets.Count} sheet(s) from {file.fileName}");
        }
        
        /// <summary>
        /// 批量生成所有配置表
        /// </summary>
        public static void GenerateAllFiles()
        {
            var data = ConfigImportData.Instance;
            int totalSheets = 0;
            
            foreach (var file in data.files)
            {
                foreach (var sheet in file.sheets)
                {
                    try
                    {
                        sheet.parentFile = file;
                        GenerateAll(file, sheet);
                        totalSheets++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to generate {sheet.className}: {ex.Message}");
                    }
                }
            }
            
            Debug.Log($"Generated {totalSheets} config table(s) from {data.files.Count} file(s)");
        }
    }
}

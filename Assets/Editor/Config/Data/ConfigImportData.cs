using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// 配置表导入数据（保存所有导入的配置表元数据）
    /// </summary>
    public class ConfigImportData : ScriptableObject
    {
        private const string AssetPath = "ProjectSettings/ConfigImportData.asset";
        
        private static ConfigImportData _instance;
        
        [SerializeField]
        public List<ConfigFileInfo> files = new List<ConfigFileInfo>();
        
        [SerializeField]
        public string sourceDirectory = "";
        
        [SerializeField]
        public string codeOutputPath = "Assets/Scripts/Config";
        
        [SerializeField]
        public string jsonOutputPath = "Assets/Resources/Configs";
        
        public static ConfigImportData Instance
        {
            get
            {
                if (_instance == null)
                {
                    // ProjectSettings 目录需要使用特殊的序列化方法
                    if (File.Exists(AssetPath))
                    {
                        var objects = InternalEditorUtility.LoadSerializedFileAndForget(AssetPath);
                        if (objects != null && objects.Length > 0)
                        {
                            _instance = objects[0] as ConfigImportData;
                        }
                    }
                    
                    if (_instance == null)
                    {
                        _instance = CreateInstance<ConfigImportData>();
                        SaveInternal();
                    }
                }
                return _instance;
            }
        }
        
        public ConfigFileInfo FindFile(string id)
        {
            return files.Find(f => f.id == id);
        }
        
        public ConfigFileInfo FindFileByPath(string filePath)
        {
            return files.Find(f => f.filePath == filePath);
        }
        
        public void AddFile(ConfigFileInfo file)
        {
            // 检查是否已存在相同路径的文件
            var existing = FindFileByPath(file.filePath);
            if (existing != null)
            {
                // 更新已有的文件
                files.Remove(existing);
            }
            files.Add(file);
            Save();
        }
        
        public void RemoveFile(string id)
        {
            files.RemoveAll(f => f.id == id);
            Save();
        }
        
        public void Save()
        {
            SaveInternal();
        }
        
        private static void SaveInternal()
        {
            if (_instance == null) return;
            
            var directory = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            InternalEditorUtility.SaveToSerializedFileAndForget(
                new UnityEngine.Object[] { _instance }, 
                AssetPath, 
                true);
        }
        
        /// <summary>
        /// 获取所有配置表（用于批量生成）
        /// </summary>
        public List<ConfigSheetInfo> GetAllSheets()
        {
            var result = new List<ConfigSheetInfo>();
            foreach (var file in files)
            {
                result.AddRange(file.sheets);
            }
            return result;
        }
    }
    
    /// <summary>
    /// 配置文件信息（一个 Excel 或 CSV 文件）
    /// </summary>
    [Serializable]
    public class ConfigFileInfo
    {
        public string id;
        public string filePath;
        public string fileName;
        public bool isCsv;
        public List<ConfigSheetInfo> sheets = new List<ConfigSheetInfo>();
        
        public ConfigFileInfo()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
        }
        
        public ConfigFileInfo(string filePath) : this()
        {
            this.filePath = filePath;
            this.fileName = Path.GetFileName(filePath);
            this.isCsv = Path.GetExtension(filePath).ToLower() == ".csv";
        }
        
        public ConfigSheetInfo GetSheet(string sheetName)
        {
            return sheets.Find(s => s.sheetName == sheetName);
        }
        
        public ConfigSheetInfo GetFirstSheet()
        {
            return sheets.FirstOrDefault();
        }
    }
    
    /// <summary>
    /// Sheet 信息（对应 Excel 中的一个 Sheet，或 CSV 文件本身）
    /// </summary>
    [Serializable]
    public class ConfigSheetInfo
    {
        public string sheetName;
        public string className;
        public List<ConfigFieldInfo> fields = new List<ConfigFieldInfo>();
        
        [NonSerialized]
        public List<List<string>> previewData;
        
        /// <summary>
        /// 父文件引用（运行时设置）
        /// </summary>
        [NonSerialized]
        public ConfigFileInfo parentFile;
        
        public ConfigSheetInfo()
        {
        }
        
        public ConfigSheetInfo(string sheetName, string className)
        {
            this.sheetName = sheetName;
            this.className = className;
        }
        
        /// <summary>
        /// 获取代码输出路径
        /// </summary>
        public string GetCodeOutputPath()
        {
            return ConfigImportData.Instance.codeOutputPath;
        }
        
        /// <summary>
        /// 获取 JSON 输出路径
        /// </summary>
        public string GetJsonOutputPath()
        {
            return ConfigImportData.Instance.jsonOutputPath;
        }
        
        /// <summary>
        /// 获取源文件路径
        /// </summary>
        public string GetFilePath()
        {
            return parentFile?.filePath;
        }
    }
    
    /// <summary>
    /// 字段作用域
    /// </summary>
    public enum FieldScope
    {
        Common,  // 客户端和服务器都需要
        Client,  // 仅客户端
        Server   // 仅服务器（导出时忽略）
    }
    
    /// <summary>
    /// 配置字段信息
    /// </summary>
    [Serializable]
    public class ConfigFieldInfo
    {
        public string fieldName;
        public string fieldType;
        public string comment;
        public bool isKey;
        public FieldScope scope = FieldScope.Common;
        
        [NonSerialized]
        private IValueTransport _cachedTransport;
        
        public ConfigFieldInfo()
        {
        }
        
        public ConfigFieldInfo(string name, string type, string comment, FieldScope scope = FieldScope.Common)
        {
            this.fieldName = name;
            this.fieldType = type;
            this.comment = comment;
            this.scope = scope;
            this.isKey = name.ToLower() == "id" || name.ToLower() == "key";
        }
        
        /// <summary>
        /// 是否需要导出（服务器专属字段不导出）
        /// </summary>
        public bool ShouldExport => scope != FieldScope.Server;
        
        /// <summary>
        /// 获取该字段的Transport
        /// </summary>
        public IValueTransport GetTransport()
        {
            if (_cachedTransport == null)
            {
                _cachedTransport = ValueTransportRegistry.GetTransport(fieldType);
            }
            return _cachedTransport;
        }
        
        /// <summary>
        /// 获取C#类型字符串
        /// </summary>
        public string GetCSharpType()
        {
            return GetTransport().GetCSharpType();
        }
        
        /// <summary>
        /// 是否为数组类型
        /// </summary>
        public bool IsArray => fieldType?.EndsWith("[]") ?? false;
        
        /// <summary>
        /// 获取数组元素类型
        /// </summary>
        public string GetElementType()
        {
            if (!IsArray) return fieldType;
            return fieldType.Substring(0, fieldType.Length - 2);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.ArtMapping
{
    /// <summary>
    /// 目录映射规则
    /// </summary>
    [Serializable]
    public class DirectoryMapping
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string id = Guid.NewGuid().ToString();
        
        /// <summary>
        /// 源目录（美术工程中的相对路径）
        /// </summary>
        public string sourceDirectory = "";
        
        /// <summary>
        /// 目标目录（程序工程中的路径，相对于 Assets）
        /// </summary>
        public string targetDirectory = "";
        
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool enabled = true;
        
        /// <summary>
        /// 备注
        /// </summary>
        public string note = "";
    }

    /// <summary>
    /// 美术资源映射配置（存储在 ProjectSettings）
    /// </summary>
    [Serializable]
    public class ArtResourceMappingSettings
    {
        private const string SETTINGS_PATH = "ProjectSettings/ArtResourceMappingSettings.json";

        /// <summary>
        /// 美术工程根目录（绝对路径）
        /// </summary>
        public string artProjectRoot = "";

        /// <summary>
        /// 目录映射列表
        /// </summary>
        public List<DirectoryMapping> mappings = new List<DirectoryMapping>();

        private static ArtResourceMappingSettings _instance;

        public static ArtResourceMappingSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }

        public static ArtResourceMappingSettings Load()
        {
            if (File.Exists(SETTINGS_PATH))
            {
                try
                {
                    var json = File.ReadAllText(SETTINGS_PATH);
                    var settings = JsonUtility.FromJson<ArtResourceMappingSettings>(json);
                    return settings ?? new ArtResourceMappingSettings();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ArtResourceMapping] 加载配置失败: {ex.Message}");
                    return new ArtResourceMappingSettings();
                }
            }
            return new ArtResourceMappingSettings();
        }

        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(SETTINGS_PATH, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ArtResourceMapping] 保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加映射规则
        /// </summary>
        public DirectoryMapping AddMapping(string sourceDir = "", string targetDir = "")
        {
            var mapping = new DirectoryMapping
            {
                sourceDirectory = sourceDir,
                targetDirectory = targetDir
            };
            mappings.Add(mapping);
            Save();
            return mapping;
        }

        /// <summary>
        /// 移除映射规则
        /// </summary>
        public void RemoveMapping(string id)
        {
            mappings.RemoveAll(m => m.id == id);
            Save();
        }

        /// <summary>
        /// 获取源目录的完整路径
        /// </summary>
        public string GetFullSourcePath(DirectoryMapping mapping)
        {
            if (string.IsNullOrEmpty(artProjectRoot) || string.IsNullOrEmpty(mapping.sourceDirectory))
                return "";
            return Path.Combine(artProjectRoot, mapping.sourceDirectory).Replace('\\', '/');
        }

        /// <summary>
        /// 获取目标目录的完整路径
        /// </summary>
        public string GetFullTargetPath(DirectoryMapping mapping)
        {
            if (string.IsNullOrEmpty(mapping.targetDirectory))
                return "";
            return Path.GetFullPath(Path.Combine("Assets", mapping.targetDirectory)).Replace('\\', '/');
        }
    }
}

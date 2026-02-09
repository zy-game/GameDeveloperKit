using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源构建器全局设置（存储在 ProjectSettings）
    /// </summary>
    [Serializable]
    public class ResourceBuilderSettings
    {
        private const string SETTINGS_PATH = "ProjectSettings/ResourceBuilderSettings.json";
        
        public bool isConfigured = false;
        public string manifestSavePath = "Assets/Resources/Manifests";
        public string outputPath = "Build/AssetBundles";
        public BuildTarget buildTarget = BuildTarget.StandaloneWindows64;
        public BuildAssetBundleOptions compression = BuildAssetBundleOptions.ChunkBasedCompression;
        public bool generateHash = true;
        public bool enableIncrementalBuild = true;
        public bool forceRebuild = false;
        public string bundleExtension = ".bundle";
        
        // SBP (Scriptable Build Pipeline) 设置 - 现在只使用SBP构建
        public bool enableContentUpdate = true;
        public bool enableDependencyOptimization = true;
        public bool generateLinkXML = false;
        
        // 全局可用标签列表
        public string[] availableLabels = new[] 
        { 
            "prefab", 
            "texture", 
            "material", 
            "audio", 
            "scene", 
            "ui", 
            "config",
            "common"
        };
        
        private static ResourceBuilderSettings _instance;
        public static ResourceBuilderSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }
        
        public static ResourceBuilderSettings Load()
        {
            if (File.Exists(SETTINGS_PATH))
            {
                try
                {
                    var json = File.ReadAllText(SETTINGS_PATH);
                    var settings = JsonUtility.FromJson<ResourceBuilderSettings>(json);
                    return settings ?? new ResourceBuilderSettings();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load ResourceBuilderSettings.json: {ex.Message}");
                    return new ResourceBuilderSettings();
                }
            }
            return new ResourceBuilderSettings();
        }
        
        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(SETTINGS_PATH, json);
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save ResourceBuilderSettings.json: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 添加标签
        /// </summary>
        public void AddLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return;
            
            label = label.Trim();
            
            // 验证标签名称（只允许字母、数字、下划线）
            if (!System.Text.RegularExpressions.Regex.IsMatch(label, @"^[a-zA-Z0-9_]+$"))
            {
                Debug.LogWarning($"标签名称只能包含字母、数字和下划线: {label}");
                return;
            }
            
            if (!HasLabel(label))
            {
                var list = new System.Collections.Generic.List<string>(availableLabels);
                list.Add(label);
                availableLabels = list.ToArray();
                Save();
            }
        }
        
        /// <summary>
        /// 移除标签
        /// </summary>
        public void RemoveLabel(string label)
        {
            if (HasLabel(label))
            {
                var list = new System.Collections.Generic.List<string>(availableLabels);
                list.Remove(label);
                availableLabels = list.ToArray();
                Save();
            }
        }
        
        /// <summary>
        /// 检查标签是否存在
        /// </summary>
        public bool HasLabel(string label)
        {
            return System.Array.Exists(availableLabels, l => l == label);
        }
    }
}

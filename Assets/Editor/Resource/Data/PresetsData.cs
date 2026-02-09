using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 收集器预设数据（存储在 ProjectSettings）
    /// </summary>
    [Serializable]
    public class CollectorPresetsData
    {
        private const string SETTINGS_PATH = "ProjectSettings/CollectorPresets.json";
        
        public List<CollectorPreset> presets = new List<CollectorPreset>();
        
        private static CollectorPresetsData _instance;
        public static CollectorPresetsData Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }
        
        public static CollectorPresetsData Load()
        {
            if (File.Exists(SETTINGS_PATH))
            {
                try
                {
                    var json = File.ReadAllText(SETTINGS_PATH);
                    var data = JsonUtility.FromJson<CollectorPresetsData>(json);
                    return data ?? new CollectorPresetsData();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load CollectorPresets.json: {ex.Message}");
                    return new CollectorPresetsData();
                }
            }
            return new CollectorPresetsData();
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
                Debug.LogError($"Failed to save CollectorPresets.json: {ex.Message}");
            }
        }
        
        public void AddPreset(CollectorPreset preset)
        {
            if (presets.Any(p => p.presetId == preset.presetId))
            {
                Debug.LogWarning($"Preset '{preset.presetName}' already exists");
                return;
            }
            presets.Add(preset);
            Save();
        }
        
        public void RemovePreset(string presetId)
        {
            presets.RemoveAll(p => p.presetId == presetId);
            Save();
        }
        
        public CollectorPreset FindPreset(string presetId)
        {
            return presets.Find(p => p.presetId == presetId);
        }
        
        public CollectorPreset FindPresetByName(string presetName)
        {
            return presets.Find(p => p.presetName == presetName);
        }
    }
    
    /// <summary>
    /// 收集器预设
    /// </summary>
    [Serializable]
    public class CollectorPreset
    {
        public string presetId;
        public string presetName;
        public string description;
        
        [SerializeReference]
        public IAssetCollector collector;
        
        public CollectorPreset()
        {
            presetId = Guid.NewGuid().ToString();
            presetName = "New Preset";
            description = "";
        }
        
        public CollectorPreset(string name, IAssetCollector collector)
        {
            presetId = Guid.NewGuid().ToString();
            presetName = name;
            description = "";
            this.collector = collector;
        }
    }
    
    /// <summary>
    /// 打包策略预设数据（存储在 ProjectSettings）
    /// </summary>
    [Serializable]
    public class PackStrategyPresetsData
    {
        private const string SETTINGS_PATH = "ProjectSettings/PackStrategyPresets.json";
        
        public List<PackStrategyPreset> presets = new List<PackStrategyPreset>();
        
        private static PackStrategyPresetsData _instance;
        public static PackStrategyPresetsData Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }
        
        public static PackStrategyPresetsData Load()
        {
            if (File.Exists(SETTINGS_PATH))
            {
                try
                {
                    var json = File.ReadAllText(SETTINGS_PATH);
                    var data = JsonUtility.FromJson<PackStrategyPresetsData>(json);
                    return data ?? new PackStrategyPresetsData();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load PackStrategyPresets.json: {ex.Message}");
                    return new PackStrategyPresetsData();
                }
            }
            return new PackStrategyPresetsData();
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
                Debug.LogError($"Failed to save PackStrategyPresets.json: {ex.Message}");
            }
        }
        
        public void AddPreset(PackStrategyPreset preset)
        {
            if (presets.Any(p => p.presetId == preset.presetId))
            {
                Debug.LogWarning($"Preset '{preset.presetName}' already exists");
                return;
            }
            presets.Add(preset);
            Save();
        }
        
        public void RemovePreset(string presetId)
        {
            presets.RemoveAll(p => p.presetId == presetId);
            Save();
        }
        
        public PackStrategyPreset FindPreset(string presetId)
        {
            return presets.Find(p => p.presetId == presetId);
        }
        
        public PackStrategyPreset FindPresetByName(string presetName)
        {
            return presets.Find(p => p.presetName == presetName);
        }
    }
    
    /// <summary>
    /// 打包策略预设
    /// </summary>
    [Serializable]
    public class PackStrategyPreset
    {
        public string presetId;
        public string presetName;
        public string description;
        
        [SerializeReference]
        public PackStrategyConfig strategyConfig;
        
        public PackStrategyPreset()
        {
            presetId = Guid.NewGuid().ToString();
            presetName = "New Preset";
            description = "";
        }
        
        public PackStrategyPreset(string name, PackStrategyConfig config)
        {
            presetId = Guid.NewGuid().ToString();
            presetName = name;
            description = "";
            strategyConfig = config;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源构建器用户偏好（存储在 UserSettings）
    /// </summary>
    [Serializable]
    public class ResourceBuilderPrefs
    {
        private const string PREFS_PATH = "UserSettings/ResourceBuilderPrefs.json";
        
        public string selectedPackageId;
        public string selectedGroupId;
        public float splitterPosition = 0.3f;
        public List<string> expandedPackages = new List<string>();
        public List<string> expandedGroups = new List<string>();
        
        private static ResourceBuilderPrefs _instance;
        public static ResourceBuilderPrefs Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }
        
        public static ResourceBuilderPrefs Load()
        {
            if (File.Exists(PREFS_PATH))
            {
                try
                {
                    var json = File.ReadAllText(PREFS_PATH);
                    var prefs = JsonUtility.FromJson<ResourceBuilderPrefs>(json);
                    return prefs ?? new ResourceBuilderPrefs();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load ResourceBuilderPrefs.json: {ex.Message}");
                    return new ResourceBuilderPrefs();
                }
            }
            return new ResourceBuilderPrefs();
        }
        
        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(this, true);
                var directory = Path.GetDirectoryName(PREFS_PATH);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(PREFS_PATH, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save ResourceBuilderPrefs.json: {ex.Message}");
            }
        }
        
        public bool IsPackageExpanded(string packageName)
        {
            return expandedPackages.Contains(packageName);
        }
        
        public void SetPackageExpanded(string packageName, bool expanded)
        {
            if (expanded && !expandedPackages.Contains(packageName))
                expandedPackages.Add(packageName);
            else if (!expanded)
                expandedPackages.Remove(packageName);
            Save();
        }
        
        public bool IsGroupExpanded(string groupId)
        {
            return expandedGroups.Contains(groupId);
        }
        
        public void SetGroupExpanded(string groupId, bool expanded)
        {
            if (expanded && !expandedGroups.Contains(groupId))
                expandedGroups.Add(groupId);
            else if (!expanded)
                expandedGroups.Remove(groupId);
            Save();
        }
    }
}

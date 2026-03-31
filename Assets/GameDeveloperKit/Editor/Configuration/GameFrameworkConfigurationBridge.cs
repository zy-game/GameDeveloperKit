using System;
using System.Collections.Generic;
using System.IO;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    [Serializable]
    internal sealed class ResourceProjectSettingsData
    {
        public ResourcePlayMode PlayMode;
        public string RemoteBaseUrl;
        public List<ResourcePackageDefinition> Packages = new();
    }

    internal readonly struct GameFrameworkConfigurationBridgeResult
    {
        public GameFrameworkConfigurationBridgeResult(string message, HelpBoxMessageType messageType)
        {
            Message = message;
            MessageType = messageType;
        }

        public string Message { get; }

        public HelpBoxMessageType MessageType { get; }
    }

    internal static class GameFrameworkConfigurationBridge
    {
        private const string ResourceSettingsPath = "ProjectSettings/GameDeveloperKit/ResourceSettings.json";

        public static GameFrameworkConfiguration ResolveSelectedOrFirstConfiguration()
        {
            if (Selection.activeObject is GameFrameworkConfiguration selectedConfiguration)
            {
                return selectedConfiguration;
            }

            var guids = AssetDatabase.FindAssets("t:GameFrameworkConfiguration");
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<GameFrameworkConfiguration>(assetPath);
        }

        public static GameFrameworkConfiguration CreateConfigurationAsset()
        {
            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Create Game Framework Configuration",
                "GameFrameworkConfiguration",
                "asset",
                "Select a location for the GameFrameworkConfiguration asset.");
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var configuration = ScriptableObject.CreateInstance<GameFrameworkConfiguration>();
            AssetDatabase.CreateAsset(configuration, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = configuration;
            return configuration;
        }

        public static GameFrameworkConfigurationBridgeResult SyncResourceSettings(GameFrameworkConfiguration configuration)
        {
            if (configuration == null)
            {
                return new GameFrameworkConfigurationBridgeResult("Select or create a GameFrameworkConfiguration asset first.", HelpBoxMessageType.Error);
            }

            var resourceSettings = LoadResourceSettingsData();
            configuration.ResourcePlayMode = resourceSettings.PlayMode;
            if (!string.IsNullOrWhiteSpace(resourceSettings.RemoteBaseUrl))
            {
                configuration.GatewayServerUrl = resourceSettings.RemoteBaseUrl;
            }

            SaveConfiguration(configuration);
            return new GameFrameworkConfigurationBridgeResult("Synced play mode and gateway URL to GameFrameworkConfiguration.", HelpBoxMessageType.Info);
        }

        public static void SaveConfiguration(GameFrameworkConfiguration configuration)
        {
            if (configuration == null)
            {
                return;
            }

            EditorUtility.SetDirty(configuration);
            AssetDatabase.SaveAssets();
        }

        public static ResourceProjectSettingsData LoadResourceSettingsData()
        {
            if (File.Exists(ResourceSettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(ResourceSettingsPath);
                    var data = JsonUtility.FromJson<ResourceProjectSettingsData>(json);
                    if (data != null)
                    {
                        Normalize(data);
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameFrameworkConfigurationBridge] Load resource settings failed: {ex.Message}");
                }
            }

            var defaultData = new ResourceProjectSettingsData();
            Normalize(defaultData);
            return defaultData;
        }

        public static void SaveResourceSettingsData(ResourceProjectSettingsData data)
        {
            try
            {
                data ??= new ResourceProjectSettingsData();
                Normalize(data);
                var directory = Path.GetDirectoryName(ResourceSettingsPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(ResourceSettingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameFrameworkConfigurationBridge] Save resource settings failed: {ex.Message}");
            }
        }

        public static ResourceProjectSettingsData BuildResourceSettingsData(GameFrameworkConfiguration configuration)
        {
            var data = new ResourceProjectSettingsData();
            if (configuration == null)
            {
                return data;
            }

            data.PlayMode = configuration.ResourcePlayMode;
            data.RemoteBaseUrl = configuration.GatewayServerUrl;
            data.Packages = LoadResourceSettingsData().Packages ?? new List<ResourcePackageDefinition>();
            Normalize(data);
            return data;
        }

        public static void ApplyResourceSettings(GameFrameworkConfiguration configuration, ResourceProjectSettingsData data)
        {
            if (configuration == null || data == null)
            {
                return;
            }

            configuration.ResourcePlayMode = data.PlayMode;
            configuration.GatewayServerUrl = data.RemoteBaseUrl;
        }

        private static void Normalize(ResourceProjectSettingsData data)
        {
            if (data == null)
            {
                return;
            }

            data.Packages ??= new List<ResourcePackageDefinition>();
            for (var i = 0; i < data.Packages.Count; i++)
            {
                ResourceCollectionService.NormalizePackage(data.Packages[i]);
            }
        }
    }
}

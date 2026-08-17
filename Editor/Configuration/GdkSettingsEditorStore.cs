using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using IOFile = System.IO.File;

namespace GameDeveloperKit.EditorConfiguration
{
    /// <summary>
    /// GDKSetting.json 编辑器存储。文件缺失时按内置默认生成，保存时原子写入并刷新资产数据库。
    /// </summary>
    public static class GdkSettingsEditorStore
    {
        public const string JsonPath = "Assets/Resources/GameDeveloperKit/GDKSetting.json";

        private static readonly JsonSerializerSettings s_SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// 加载 GDKSetting.json；不存在时生成默认文件。
        /// </summary>
        public static GdkSettings LoadOrCreate()
        {
            var settings = Load();
            if (settings != null)
            {
                return settings;
            }

            settings = new GdkSettings();
            settings.EnsureDefaults();
            Save(settings);
            return settings;
        }

        /// <summary>
        /// 加载 GDKSetting.json；不存在或解析失败时返回 null。
        /// </summary>
        public static GdkSettings Load()
        {
            if (IOFile.Exists(JsonPath) is false)
            {
                return null;
            }

            try
            {
                var settings = JsonConvert.DeserializeObject<GdkSettings>(IOFile.ReadAllText(JsonPath));
                settings?.EnsureDefaults();
                return settings;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GdkSettingsEditorStore] Failed to read {JsonPath}: {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存 GDKSetting.json（原子写）。
        /// </summary>
        public static void Save(GdkSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.EnsureDefaults();
            EnsureFolder(Path.GetDirectoryName(JsonPath));
            var tempPath = JsonPath + ".tmp";
            IOFile.WriteAllText(tempPath, JsonConvert.SerializeObject(settings, s_SerializerSettings));
            if (IOFile.Exists(JsonPath))
            {
                IOFile.Replace(tempPath, JsonPath, null);
            }
            else
            {
                IOFile.Move(tempPath, JsonPath);
            }

            AssetDatabase.ImportAsset(JsonPath, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || Directory.Exists(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
        }
    }
}

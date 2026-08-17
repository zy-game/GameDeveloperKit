using System;
using GameDeveloperKit.Config;
using GameDeveloperKit.Media;
using GameDeveloperKit.Resource;
using Newtonsoft.Json;
using UnityEngine;

namespace GameDeveloperKit
{
    /// <summary>
    /// GDKSetting.json 根模型。项目级运行时配置的唯一真相，各模块按需从对应 section 取值。
    /// </summary>
    [Serializable]
    public sealed class GdkSettings
    {
        public const string ResourcePath = "GameDeveloperKit/GDKSetting";
        public const int CurrentSchemaVersion = 1;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        [JsonProperty("localization")]
        public GdkLocalizationSettings Localization { get; set; }

        [JsonProperty("mediaDelivery")]
        public MediaDeliverySettings MediaDelivery { get; set; }

        [JsonProperty("tagCatalog")]
        public TagCatalogSettings TagCatalog { get; set; }

        [JsonProperty("resourceSettings")]
        public ResourceSettings ResourceSettings { get; set; }

        public void EnsureDefaults()
        {
            if (SchemaVersion <= 0)
            {
                SchemaVersion = CurrentSchemaVersion;
            }

            Localization ??= new GdkLocalizationSettings();
            Localization.EnsureDefaults();

            MediaDelivery?.EnsureDefaults();

            TagCatalog ??= new TagCatalogSettings();
            TagCatalog.EnsureDefaults();

            ResourceSettings ??= new ResourceSettings();
        }
    }

    /// <summary>
    /// 本地化 section：catalog 资产地址、启动语言与所在资源包。
    /// </summary>
    [Serializable]
    public sealed class GdkLocalizationSettings
    {
        [JsonProperty("catalogLocation")]
        public string CatalogLocation { get; set; } = string.Empty;

        [JsonProperty("startupLocale")]
        public string StartupLocale { get; set; } = string.Empty;

        [JsonProperty("requiredPackage")]
        public string RequiredPackage { get; set; } = string.Empty;

        public void EnsureDefaults()
        {
            CatalogLocation = CatalogLocation?.Trim() ?? string.Empty;
            StartupLocale = StartupLocale?.Trim() ?? string.Empty;
            RequiredPackage = RequiredPackage?.Trim() ?? string.Empty;
        }
    }

    /// <summary>
    /// GDKSetting.json 同步读取入口。文件缺失或解析失败时返回 null，由调用方优雅降级。
    /// </summary>
    public static class GdkSettingsStore
    {
        public static GdkSettings Load()
        {
            try
            {
                var asset = Resources.Load<TextAsset>(GdkSettings.ResourcePath);
                if (asset == null)
                {
                    return null;
                }

                var settings = JsonConvert.DeserializeObject<GdkSettings>(asset.text);
                settings?.EnsureDefaults();
                return settings;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GdkSettings] Failed to load '{GdkSettings.ResourcePath}': {exception.Message}");
                return null;
            }
        }
    }
}

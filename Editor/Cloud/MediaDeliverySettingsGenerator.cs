using System;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Media;

namespace GameDeveloperKit.EditorCloud
{
    /// <summary>
    /// 把云配置解析为运行时媒体端点，写入 GDKSetting.json 的 mediaDelivery section。
    /// </summary>
    public static class MediaDeliverySettingsGenerator
    {
        public static MediaDeliverySettings Generate(CloudProjectConfig config)
        {
            var settings = CreateSettings(config);

            var gdkSettings = GdkSettingsEditorStore.LoadOrCreate();
            gdkSettings.MediaDelivery = settings;
            GdkSettingsEditorStore.Save(gdkSettings);
            return settings;
        }

        internal static MediaDeliverySettings CreateSettings(CloudProjectConfig config)
        {
            var settings = new MediaDeliverySettings();
            ApplyConfiguration(settings, config);
            return settings;
        }

        private static void ApplyConfiguration(
            MediaDeliverySettings settings,
            CloudProjectConfig config)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (CloudPublicUrlResolver.TryResolveOriginBaseUrl(
                    config,
                    out var originBaseUrl,
                    out var error) is false)
            {
                throw new ArgumentException(error, nameof(config));
            }

            settings.SetPublicUrls(originBaseUrl, config.CdnBaseUrl);
        }
    }
}

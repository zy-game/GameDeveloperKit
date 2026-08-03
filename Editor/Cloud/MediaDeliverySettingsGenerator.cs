using System;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Media;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.EditorCloud
{
    public static class MediaDeliverySettingsGenerator
    {
        public const string AssetPath = "Assets/Resources/GameDeveloperKit/MediaDeliverySettings.asset";

        public static MediaDeliverySettings Generate(CloudProjectConfig config)
        {
            EnsureAssetFolder();
            var settings = AssetDatabase.LoadAssetAtPath<MediaDeliverySettings>(AssetPath);
            if (settings == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(AssetPath) != null)
                {
                    throw new InvalidOperationException(
                        $"Runtime media settings path contains another asset type: {AssetPath}");
                }

                settings = CreateSettings(config);
                AssetDatabase.CreateAsset(settings, AssetPath);
            }
            else
            {
                ApplyConfiguration(settings, config);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        internal static MediaDeliverySettings CreateSettings(CloudProjectConfig config)
        {
            var settings = ScriptableObject.CreateInstance<MediaDeliverySettings>();
            try
            {
                ApplyConfiguration(settings, config);
                return settings;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(settings);
                throw;
            }
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

        private static void EnsureAssetFolder()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "GameDeveloperKit");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (AssetDatabase.IsValidFolder(path) is false)
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}

using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.Config.Internal
{
    internal static class ConfigPayloadResolver
    {
        public static async UniTask<ConfigSourcePayload> ResolveAsync(ConfigSourceDefinition source)
        {
            if (source.Format == ConfigFormat.ScriptableObject)
            {
                var asset = await ResolveAssetAsync(source);
                return ConfigSourcePayload.FromAsset(asset);
            }

            var text = await ResolveTextAsync(source);
            if (string.IsNullOrEmpty(text))
            {
                throw new GameException(
                    $"Config source '{source.Name}' at '{source.Location}' is empty.");
            }

            return ConfigSourcePayload.FromText(text);
        }

        private static async UniTask<string> ResolveTextAsync(ConfigSourceDefinition source)
        {
            if (System.IO.File.Exists(source.Location))
            {
                return await System.IO.File.ReadAllTextAsync(source.Location, Encoding.UTF8);
            }

            var textAsset = Resources.Load<TextAsset>(source.Location);
            if (textAsset != null)
            {
                return textAsset.text;
            }

            if (Super.TryGetRegistered<ResourceModule>(out var resource))
            {
                var handle = await resource.LoadRawAssetAsync(source.Location);
                if (handle != null && handle.Status == ResourceStatus.Succeeded)
                {
                    return handle.GetString();
                }
            }

            throw new GameException(
                $"Config source '{source.Name}' text asset was not found. Format: {source.Format}, location: {source.Location}.");
        }

        private static async UniTask<Object> ResolveAssetAsync(ConfigSourceDefinition source)
        {
            var asset = Resources.Load<Object>(source.Location);
            if (asset != null)
            {
                return asset;
            }

            if (Super.TryGetRegistered<ResourceModule>(out var resource))
            {
                var handle = await resource.LoadAssetAsync(source.Location);
                if (handle != null && handle.Status == ResourceStatus.Succeeded && handle.Asset != null)
                {
                    return handle.Asset;
                }
            }

            throw new GameException(
                $"Config source '{source.Name}' ScriptableObject asset was not found. Location: {source.Location}.");
        }
    }
}

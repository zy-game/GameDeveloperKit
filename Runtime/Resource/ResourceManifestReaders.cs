using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using Newtonsoft.Json;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    internal static class ResourceManifestReader
    {
        public static async UniTask<ManifestInfo> ReadAsync(string location)
        {
            var bytes = await ReadManifestBytesAsync(location);
            if (bytes is null || bytes.Length == 0)
            {
                throw new GameException("Manifest file is empty.");
            }

            var text = Encoding.UTF8.GetString(bytes);
            var manifest = JsonConvert.DeserializeObject<ManifestInfo>(text);
            if (manifest is null)
            {
                throw new GameException("Unable to deserialize manifest.");
            }

            return manifest;
        }

        private static async UniTask<byte[]> ReadManifestBytesAsync(string location)
        {
            if (Uri.TryCreate(location, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var operation = App.Download.DownloadAsync(location);
                await operation.WaitCompletionAsync();
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    throw operation.Error ?? new GameException($"Manifest download failed: {location}");
                }

                return await System.IO.File.ReadAllBytesAsync(operation.TempPath);
            }

            var path = ResolveLocalManifestPath(location);
            return await System.IO.File.ReadAllBytesAsync(path);
        }

        private static string ResolveLocalManifestPath(string location)
        {
            if (Path.IsPathRooted(location) && System.IO.File.Exists(location))
            {
                return location;
            }

            var streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, location);
            if (System.IO.File.Exists(streamingAssetsPath))
            {
                return streamingAssetsPath;
            }

            var fallbackPath = Path.Combine(Application.streamingAssetsPath, ResourceSettings.MANIFEST_NAME);
            if (System.IO.File.Exists(fallbackPath))
            {
                return fallbackPath;
            }

            return Path.IsPathRooted(location) ? location : streamingAssetsPath;
        }
    }
    //__APPEND_READERS__

    internal static class ResourcePublishVersionReader
    {
        public static async UniTask<string> ReadAsync(string location)
        {
            var bytes = await ReadPublishBytesAsync(location);
            if (bytes is null || bytes.Length == 0)
            {
                throw new GameException("Publish file is empty.");
            }

            var text = Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrEmpty(text))
            {
                throw new GameException("Publish text is empty.");
            }

            var pointer = JsonConvert.DeserializeObject<ResourcePublishPointer>(text);
            if (pointer == null || string.IsNullOrWhiteSpace(pointer.version))
            {
                throw new GameException("Publish version is empty.");
            }

            App.Debug.Info($"Publish version loaded from: {location} Version: {pointer.version}");
            return pointer.version;
        }

        private static async UniTask<byte[]> ReadPublishBytesAsync(string location)
        {
            if (Uri.TryCreate(location, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var operation = App.Download.DownloadAsync(location);
                await operation.WaitCompletionAsync();
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    throw operation.Error ?? new GameException($"Publish download failed: {location}");
                }

                return await System.IO.File.ReadAllBytesAsync(operation.TempPath);
            }

            var path = ResolveLocalPublishPath(location);
            return await System.IO.File.ReadAllBytesAsync(path);
        }

        private static string ResolveLocalPublishPath(string location)
        {
            if (Path.IsPathRooted(location) && System.IO.File.Exists(location))
            {
                return location;
            }

            var streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, location);
            if (System.IO.File.Exists(streamingAssetsPath))
            {
                return streamingAssetsPath;
            }

            return Path.IsPathRooted(location) ? location : streamingAssetsPath;
        }

        private sealed class ResourcePublishPointer
        {
            public string version { get; set; }
        }
    }

    internal static class ResourceRemoteManifestLoader
    {
        public static async UniTask<ManifestInfo> LoadAsync(ResourceSettings setting)
        {
            var publishLocation = App.Resource.GetPublishAddress(setting);
            App.Debug.Info($"Resource publish source. Mode: {setting.Mode}, Location: {publishLocation}");
            var version = await ResourcePublishVersionReader.ReadAsync(publishLocation);
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new GameException($"Failed to load resource publish version: {publishLocation}");
            }

            var manifestLocation = App.Resource.GetManifestAddress(setting, version);
            App.Debug.Info($"Resource manifest source. Mode: {setting.Mode}, Location: {manifestLocation}");
            return await ResourceManifestReader.ReadAsync(manifestLocation);
        }
    }
}

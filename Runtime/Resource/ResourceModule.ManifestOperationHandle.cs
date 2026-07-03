using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Debugger;
using GameDeveloperKit.Operation;
using Newtonsoft.Json;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    public sealed partial class ResourceModule
    {
        public sealed class ManifestOperationHandle : OperationHandle<ManifestInfo>
        {
            public override async void Execute(params object[] args)
            {
                try
                {
                    var location = OperationArgs.RequireString(args, 0, "location", "Manifest location");
                    SetResult(await ResourceManifestReader.ReadAsync(location));
                }
                catch (Exception e)
                {
                    SetException(e);
                }
            }
        }
    }

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
            if (string.IsNullOrEmpty(text))
            {
                throw new GameException("Manifest text is empty.");
            }

            if (App.TryGetRegistered<DebugModule>(out var debugModule))
            {
                debugModule.Info($"Manifest loaded from: {location} Content: {text}");
            }

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

    internal static class OperationArgs
    {
        public static T Require<T>(object[] args, int index, string name) where T : class
        {
            if (args == null || args.Length <= index || args[index] is not T value)
            {
                throw new ArgumentNullException(name);
            }

            return value;
        }

        public static string RequireString(object[] args, int index, string name, string displayName)
        {
            var value = Require<string>(args, index, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{displayName} cannot be empty.", name);
            }

            return value;
        }
    }
}

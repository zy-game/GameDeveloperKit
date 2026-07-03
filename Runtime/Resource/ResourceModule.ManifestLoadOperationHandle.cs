using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Operation;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    public sealed partial class ResourceModule
    {
        private sealed class ManifestLoadResult
        {
            public ManifestInfo Manifest;

            public List<string> LocalPackages = new List<string>();
        }

        private sealed class ManifestLoadOperationHandle : OperationHandle<ManifestLoadResult>
        {
            public override async void Execute(params object[] args)
            {
                try
                {
                    var setting = OperationArgs.Require<ResourceSettings>(args, 0, "setting");

                    var localManifest = await LoadManifestAsync(GetLocalManifestLocation(setting), setting.Mode);
                    var manifest = setting.Mode switch
                    {
                        ResourceMode.EditorSimulator => await EditorSimulatorMode.LoadManifestAsync(setting, localManifest),
                        ResourceMode.Offline => await StreamingAssetMode.LoadManifestAsync(setting, localManifest),
                        ResourceMode.Online => await BundleMode.LoadManifestAsync(setting, localManifest),
                        ResourceMode.Web => await WebGLMode.LoadManifestAsync(setting, localManifest),
                        _ => throw new GameException($"Unsupported resource mode: {setting.Mode}")
                    };

                    if (manifest == null)
                    {
                        throw new GameException($"Resource manifest load failed. Mode: {setting.Mode}");
                    }

                    App.Debug.Info($"Resource manifest loaded. Mode: {setting.Mode}, Version: {manifest.Version}");
                    SetResult(new ManifestLoadResult
                    {
                        Manifest = manifest,
                        LocalPackages = GetPackageNames(localManifest)
                    });
                }
                catch (Exception e)
                {
                    SetException(e);
                }
            }

            private static List<string> GetPackageNames(ManifestInfo manifest)
            {
                return manifest?.Packages?
                    .Where(package => package != null && string.IsNullOrWhiteSpace(package.Name) is false)
                    .Select(package => package.Name)
                    .Distinct(StringComparer.Ordinal)
                    .ToList() ?? new List<string>();
            }

            private static async Cysharp.Threading.Tasks.UniTask<ManifestInfo> LoadManifestAsync(string manifestLocation, ResourceMode mode)
            {
                App.Debug.Info($"Resource manifest source. Mode: {mode}, Location: {manifestLocation}");
                var operationHandle = await App.Operation.WaitCompletionWithKeyAsync<ManifestOperationHandle>(manifestLocation, manifestLocation);
                if (operationHandle.Status is not OperationStatus.Succeeded || operationHandle.Value == null)
                {
                    throw new GameException($"Failed to load resource manifest. Mode: {mode}, Location: {manifestLocation}", operationHandle.Error);
                }

                return operationHandle.Value;
            }

            private static string GetLocalManifestLocation(ResourceSettings setting)
            {
                var manifestName = string.IsNullOrWhiteSpace(setting.ManifestName)
                    ? ResourceSettings.MANIFEST_NAME
                    : setting.ManifestName;
                if (Path.IsPathRooted(manifestName))
                {
                    return manifestName;
                }

                return Path.Combine(Application.streamingAssetsPath, manifestName).Replace('\\', '/');
            }

        }
    }

    internal static class ResourceRemoteManifestLoader
    {
        public static async Cysharp.Threading.Tasks.UniTask<ManifestInfo> LoadAsync(ResourceSettings setting)
        {
            var publishLocation = setting.GetPublishAddress();
            App.Debug.Info($"Resource publish source. Mode: {setting.Mode}, Location: {publishLocation}");
            var versionHandle = await App.Operation.WaitCompletionWithKeyAsync<ResourceModule.PublishVersionOperationHandle>(publishLocation, publishLocation);
            if (versionHandle.Status is not OperationStatus.Succeeded || string.IsNullOrWhiteSpace(versionHandle.Value))
            {
                throw new GameException($"Failed to load resource publish version: {publishLocation}", versionHandle.Error);
            }

            var manifestLocation = setting.GetManifestAddress(versionHandle.Value);
            App.Debug.Info($"Resource manifest source. Mode: {setting.Mode}, Location: {manifestLocation}");
            var operationHandle = await App.Operation.WaitCompletionWithKeyAsync<ResourceModule.ManifestOperationHandle>(manifestLocation, manifestLocation);
            if (operationHandle.Status is not OperationStatus.Succeeded || operationHandle.Value == null)
            {
                throw new GameException($"Failed to load resource manifest. Mode: {setting.Mode}, Location: {manifestLocation}", operationHandle.Error);
            }

            return operationHandle.Value;
        }
    }
}

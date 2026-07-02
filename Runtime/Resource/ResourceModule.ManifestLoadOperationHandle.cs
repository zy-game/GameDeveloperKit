using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
                    App.Debug.Assert(args is { Length: >= 1 });
                    var setting = (ResourceSettings)args[0];

                    var editorSimulatorManifest = setting.Mode == ResourceMode.EditorSimulator
                        ? BuildEditorSimulatorManifest()
                        : null;
                    var localManifest = await LoadManifestAsync(GetLocalManifestLocation(setting), setting.Mode);
                    var manifest = setting.Mode switch
                    {
                        ResourceMode.EditorSimulator => ManifestMergeUtility.Merge(localManifest, editorSimulatorManifest),
                        ResourceMode.Offline => localManifest,
                        ResourceMode.Online => ManifestMergeUtility.Merge(localManifest, await LoadRemoteManifestAsync(setting)),
                        ResourceMode.Web => ManifestMergeUtility.Merge(localManifest, await LoadRemoteManifestAsync(setting)),
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

            private static async Cysharp.Threading.Tasks.UniTask<ManifestInfo> LoadRemoteManifestAsync(ResourceSettings setting)
            {
                var publishLocation = setting.GetPublishAddress();
                App.Debug.Info($"Resource publish source. Mode: {setting.Mode}, Location: {publishLocation}");
                var versionHandle = await App.Operation.WaitCompletionWithKeyAsync<PublishVersionOperationHandle>(publishLocation, publishLocation);
                if (versionHandle.Status is not OperationStatus.Succeeded || string.IsNullOrWhiteSpace(versionHandle.Value))
                {
                    throw new GameException($"Failed to load resource publish version: {publishLocation}", versionHandle.Error);
                }

                var manifestLocation = setting.GetManifestAddress(versionHandle.Value);
                return await LoadManifestAsync(manifestLocation, setting.Mode);
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

            private static ManifestInfo BuildEditorSimulatorManifest()
            {
#if UNITY_EDITOR
                const string providerTypeName = "GameDeveloperKit.ResourceEditor.ResourceEditorPlayModeManifestProvider, GameDeveloperKit.Editor";
                const string methodName = "BuildEditorSimulatorManifest";
                var providerType = Type.GetType(providerTypeName);
                if (providerType == null)
                {
                    throw new GameException($"EditorSimulator manifest provider is missing: {providerTypeName}");
                }

                var method = providerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    throw new GameException($"EditorSimulator manifest provider method is missing: {providerTypeName}.{methodName}");
                }

                try
                {
                    App.Debug.Info($"Resource manifest source. Mode: {ResourceMode.EditorSimulator}, Provider: {providerTypeName}.{methodName}");
                    var result = method.Invoke(null, Array.Empty<object>());
                    if (result is not ManifestInfo manifest)
                    {
                        throw new GameException($"EditorSimulator manifest provider returned invalid result: {providerTypeName}.{methodName}");
                    }

                    return manifest;
                }
                catch (TargetInvocationException exception)
                {
                    throw new GameException("EditorSimulator manifest generation failed.", exception.InnerException ?? exception);
                }
#else
                throw new GameException("EditorSimulator resource mode is only available in Unity Editor.");
#endif
            }
        }
    }
}

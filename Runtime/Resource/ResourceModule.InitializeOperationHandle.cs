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
        private sealed class ManifestInitializationResult
        {
            public ManifestInfo Manifest;

            public List<string> LocalPackages = new List<string>();
        }

        sealed class InitializeOperationHandle : OperationHandle<ManifestInitializationResult>
        {
            /// <summary>
            /// 执行 Execute。
            /// </summary>
            public override async void Execute(params object[] args)
            {
                try
                {
                    App.Debug.Assert(args is { Length: >= 1 });
                    var setting = (ResourceSettings)args[0];
                    var localOnly = args.Length > 1 && args[1] is bool value && value;

                    App.Debug.Info($"Resource settings loaded. ServerUrl: {setting.ServerUrl}, Mode: {setting.Mode}");
                    var editorSimulatorManifest = setting.Mode == ResourceMode.EditorSimulator
                        ? BuildEditorSimulatorManifest()
                        : null;
                    var localManifest = await LoadManifestAsync(GetLocalManifestLocation(setting), setting.Mode);
                    if (localOnly)
                    {
                        App.Debug.Info($"Local resource manifest loaded. Mode: {setting.Mode}, Version: {localManifest.Version}");
                        SetResult(new ManifestInitializationResult
                        {
                            Manifest = localManifest,
                            LocalPackages = GetPackageNames(localManifest)
                        });
                        return;
                    }

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
                        throw new GameException($"Resource manifest initialize failed. Mode: {setting.Mode}");
                    }

                    App.Debug.Info($"Resource manifest loaded. Mode: {setting.Mode}, Version: {manifest.Version}");
                    SetResult(new ManifestInitializationResult
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

            /// <summary>
            /// 加载 Remote Manifest Async。
            /// </summary>
            private static async Cysharp.Threading.Tasks.UniTask<ManifestInfo> LoadRemoteManifestAsync(ResourceSettings setting)
            {
                if (string.IsNullOrWhiteSpace(setting.ServerUrl))
                {
                    throw new GameException("Server URL cannot be empty for online or web resource mode.");
                }

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

            /// <summary>
            /// 加载 Manifest Async。
            /// </summary>
            /// <param name="manifestLocation">manifest Location 参数。</param>
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

            /// <summary>
            /// 获取 Local Manifest Location。
            /// </summary>
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

            /// <summary>
            /// 构建 Editor Simulator Manifest。
            /// </summary>
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;
using Newtonsoft.Json;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源模块分部定义。
    /// </summary>
    public sealed partial class ResourceModule
    {
        /// <summary>
        /// 资源清单加载操作句柄：按资源模式读取并验证候选清单。
        /// </summary>
        internal sealed class LoadManifestOperationHandle : OperationHandle<ResourceManifestIndex>
        {
            /// <summary>
            /// 创建资源清单加载失败操作句柄。
            /// </summary>
            /// <param name="exception">错误信息。</param>
            /// <returns>资源清单加载操作句柄。</returns>
            public static LoadManifestOperationHandle Failure(Exception exception)
            {
                var handle = new LoadManifestOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            /// <summary>
            /// 执行操作句柄逻辑。
            /// </summary>
            /// <param name="args">操作参数。</param>
            public override void Execute(params object[] args)
            {
                ExecuteAsync(args).Forget(UnityEngine.Debug.LogException);
            }

            private async UniTask ExecuteAsync(object[] args)
            {
                try
                {
                    var setting = args[0] as ResourceSettings;
                    if (setting == null)
                    {
                        throw new ArgumentNullException(nameof(setting));
                    }

                    var (manifest, remoteBundleNames) = await LoadByModeAsync(setting);
                    if (manifest == null)
                    {
                        throw new GameException($"Resource manifest initialize failed. Mode: {setting.Mode}");
                    }

                    var index = ResourceManifestValidator.ValidateAndIndex(
                        manifest,
                        setting.Mode,
                        remoteBundleNames);
                    App.Debug.Info(
                        $"Resource manifest validated. Mode: {setting.Mode}, Version: {index.Version}, " +
                        $"Packages: {index.PackageCount}, Bundles: {index.BundleCount}, Assets: {index.AssetCount}");
                    SetResult(index);
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            /// <summary>
            /// 同步读取启动清单（StreamingAssets），不存在时返回 null。
            /// </summary>
            /// <returns>启动清单；不存在时返回 null。</returns>
            public static ManifestInfo ReadStartupManifest()
            {
                if (!App.TryGetRegistered<FileModule>(out var fileModule) ||
                    !fileModule.TryReadPackagedBytes(ResourceSettings.MANIFEST_NAME, out var bytes))
                {
                    return null;
                }

                var text = Encoding.UTF8.GetString(bytes);
                if (text.Length > 0 && text[0] == '\uFEFF')
                {
                    text = text.Substring(1);
                }
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new GameException($"Startup resource manifest is empty: {ResourceSettings.MANIFEST_NAME}");
                }

                var manifest = JsonConvert.DeserializeObject<ManifestInfo>(text);
                if (manifest == null)
                {
                    throw new GameException($"Unable to deserialize startup resource manifest: {ResourceSettings.MANIFEST_NAME}");
                }

                return manifest;
            }

            private static async UniTask<ManifestInfo> LoadLocalManifestAsync(ResourceSettings setting)
            {
                var location = GetLocalManifestLocation(setting);
                App.Debug.Info($"Resource local manifest source. Mode: {setting.Mode}, Location: {location}");
                return await ResourceManifestReader.ReadAsync(location);
            }

            private static async UniTask<(ManifestInfo Manifest, IReadOnlyCollection<string> RemoteBundleNames)> LoadByModeAsync(
                ResourceSettings setting)
            {
                switch (setting.Mode)
                {
                    case ResourceMode.EditorSimulator:
                        return (BuildEditorSimulatorManifest(), Array.Empty<string>());
                    case ResourceMode.Offline:
                        return (await LoadLocalManifestAsync(setting), Array.Empty<string>());
                    case ResourceMode.Online:
                    case ResourceMode.Web:
                        var localManifest = await LoadLocalManifestAsync(setting);
                        var remoteManifest = await ResourceRemoteManifestLoader.LoadAsync(setting);
                        return (
                            ManifestMergeUtility.Merge(localManifest, remoteManifest),
                            CollectRemoteBundleNames(remoteManifest));
                    default:
                        throw new GameException($"Unsupported resource mode: {setting.Mode}");
                }
            }

            private static IReadOnlyCollection<string> CollectRemoteBundleNames(ManifestInfo manifest)
            {
                var bundleNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var package in manifest?.Packages ?? new List<PackageInfo>())
                {
                    if (package == null ||
                        string.Equals(package.Name, ResourceConstants.BUILTIN_PACKAGE_NAME, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (var bundle in package.Bundles ?? new List<BundleInfo>())
                    {
                        if (string.IsNullOrWhiteSpace(bundle?.Name) is false)
                        {
                            bundleNames.Add(bundle.Name);
                        }
                    }
                }

                return bundleNames;
            }

            private static ManifestInfo BuildEditorSimulatorManifest()
            {
#if UNITY_EDITOR
                const string providerTypeName = "GameDeveloperKit.ResourceEditor.Build.PlayModeManifestProvider, GameDeveloperKit.Editor";
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

            private static string GetLocalManifestLocation(ResourceSettings setting)
            {
                var manifestName = string.IsNullOrWhiteSpace(setting.ManifestName)
                    ? ResourceSettings.MANIFEST_NAME
                    : setting.ManifestName;
                return manifestName;
            }
        }
    }
}

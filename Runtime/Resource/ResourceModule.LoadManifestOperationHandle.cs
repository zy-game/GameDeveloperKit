using System;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using Newtonsoft.Json;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源模块分部定义。
    /// </summary>
    public sealed partial class ResourceModule
    {
        /// <summary>
        /// 资源清单加载操作句柄：读取本地清单并按资源模式合并远端/编辑器清单。
        /// </summary>
        public sealed class LoadManifestOperationHandle : OperationHandle<ManifestInfo>
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
            public override async void Execute(params object[] args)
            {
                try
                {
                    var setting = args[0] as ResourceSettings;
                    if (setting == null)
                    {
                        throw new ArgumentNullException(nameof(setting));
                    }

                    var localManifest = await LoadLocalManifestAsync(setting);
                    var manifest = await LoadByModeAsync(setting, localManifest);
                    if (manifest == null)
                    {
                        throw new GameException($"Resource manifest initialize failed. Mode: {setting.Mode}");
                    }

                    App.Debug.Info($"Resource manifest loaded. Mode: {setting.Mode}, Version: {manifest.Version}");
                    SetResult(manifest);
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
                var path = Path.Combine(Application.streamingAssetsPath, ResourceSettings.MANIFEST_NAME);
                if (System.IO.File.Exists(path) is false)
                {
                    return null;
                }

                var text = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new GameException($"Startup resource manifest is empty: {path}");
                }

                var manifest = JsonConvert.DeserializeObject<ManifestInfo>(text);
                if (manifest == null)
                {
                    throw new GameException($"Unable to deserialize startup resource manifest: {path}");
                }

                return manifest;
            }

            private static async UniTask<ManifestInfo> LoadLocalManifestAsync(ResourceSettings setting)
            {
                var location = GetLocalManifestLocation(setting);
                App.Debug.Info($"Resource local manifest source. Mode: {setting.Mode}, Location: {location}");
                return await ResourceManifestReader.ReadAsync(location);
            }

            private static async UniTask<ManifestInfo> LoadByModeAsync(ResourceSettings setting, ManifestInfo localManifest)
            {
                switch (setting.Mode)
                {
                    case ResourceMode.EditorSimulator:
                        return ManifestMergeUtility.Merge(localManifest, BuildEditorSimulatorManifest());
                    case ResourceMode.Offline:
                        return localManifest;
                    case ResourceMode.Online:
                    case ResourceMode.Web:
                        return ManifestMergeUtility.Merge(localManifest, await ResourceRemoteManifestLoader.LoadAsync(setting));
                    default:
                        throw new GameException($"Unsupported resource mode: {setting.Mode}");
                }
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
}

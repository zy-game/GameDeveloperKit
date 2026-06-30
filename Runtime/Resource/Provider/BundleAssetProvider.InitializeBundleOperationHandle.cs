using System;
using GameDeveloperKit.Operation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.IO;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BundleAssetProvider
    {
        /// <summary>
        /// 资源包加载操作句柄。
        /// </summary>
        public sealed class InitializeBundleOperationHandle : OperationHandle<BundleHandle>
        {
            /// <summary>
            /// 创建资源包加载失败操作句柄。
            /// </summary>
            /// <param name="exception">错误信息。</param>
            /// <returns>资源包加载操作句柄。</returns>
            public static InitializeBundleOperationHandle Failure(Exception exception)
            {
                var handle = new InitializeBundleOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            /// <summary>
            /// 创建资源包加载成功操作句柄。
            /// </summary>
            /// <returns>资源包加载操作句柄。</returns>
            public static InitializeBundleOperationHandle Success()
            {
                var handle = new InitializeBundleOperationHandle();
                handle.SetResult();
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
                    var bundleInfo = args[0] as BundleInfo;
                    if (bundleInfo == null)
                    {
                        SetException(new ArgumentNullException(nameof(bundleInfo)));
                        return;
                    }

                    var bundlePath = ProviderBase.ResolveBundleFileName(bundleInfo);
                    var bytes = await App.File.ReadAsync(bundlePath);
                    if (bytes == null || bytes.Length == 0)
                    {
                        bytes = await ReadStreamingAssetsBytesAsync(bundlePath);
                    }

                    if (bytes == null || bytes.Length == 0)
                    {
                        SetException(new GameException($"Bundle load failed: {bundlePath}"));
                        return;
                    }

                    var bundle = await AssetBundle.LoadFromMemoryAsync(bytes);
                    if (bundle == null)
                    {
                        SetException(new GameException($"Bundle load failed: {bundlePath}"));
                        return;
                    }

                    SetResult(BundleHandle.Success(bundleInfo, bundle));
                    App.Debug.Info($"Loading {bundlePath} AssetBundle Completion");
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            private static async UniTask<byte[]> ReadStreamingAssetsBytesAsync(string bundlePath)
            {
                var path = Path.Combine(Application.streamingAssetsPath, bundlePath).Replace('\\', '/');
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    using (var request = UnityWebRequest.Get(path))
                    {
                        await request.SendWebRequest();
                        return request.result == UnityWebRequest.Result.Success
                            ? request.downloadHandler.data
                            : null;
                    }
                }

                if (System.IO.File.Exists(path) is false)
                {
                    return null;
                }

                return await System.IO.File.ReadAllBytesAsync(path);
            }

        }
    }
}

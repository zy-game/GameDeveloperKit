using System;
using GameDeveloperKit.Operation;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    public sealed partial class WebAssetProvider
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
                    var settings = App.Resource.Settings;
                    if (settings == null)
                    {
                        SetException(new GameException("Resource server url is empty."));
                        return;
                    }

                    var version = App.Resource.Manifest.Version;
                    if (string.IsNullOrWhiteSpace(version))
                    {
                        SetException(new GameException("Resource current version is empty."));
                        return;
                    }

                    var uri = settings.GetAssetAddress(bundlePath, version);
                    using (var request = UnityWebRequestAssetBundle.GetAssetBundle(uri, bundleInfo.Crc))
                    {
                        await request.SendWebRequest();
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            SetException(new GameException(request.error ?? $"Bundle load failed: {uri}"));
                            return;
                        }

                        var bundle = DownloadHandlerAssetBundle.GetContent(request);
                        if (bundle == null)
                        {
                            SetException(new GameException($"Bundle load failed: {uri}"));
                            return;
                        }

                        SetResult(BundleHandle.Success(bundleInfo, bundle));
                    }
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }
        }
    }
}

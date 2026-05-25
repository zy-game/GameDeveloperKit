using System;
using GameDeveloperKit.Operation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BundleProvider
    {
        public sealed class InitializeBundleOperationHandle : OperationHandle<BundleHandle>
        {
            public static InitializeBundleOperationHandle Failure(Exception exception)
            {
                var handle = new InitializeBundleOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            public static InitializeBundleOperationHandle Success()
            {
                var handle = new InitializeBundleOperationHandle();
                handle.SetResult();
                return handle;
            }

            public override void Execute(params object[] args)
            {
                try
                {
                    var bundleInfo = args[0] as BundleInfo;
                    if (bundleInfo == null)
                    {
                        SetException(new ArgumentNullException(nameof(bundleInfo)));
                        return;
                    }

                    var bundlePath = bundleInfo.Name;
                    if (string.IsNullOrWhiteSpace(bundlePath))
                    {
                        SetException(new ArgumentException("Bundle name cannot be empty.", nameof(bundleInfo)));
                        return;
                    }

                    if (bundlePath.Contains("://"))
                    {
                        LoadFromUriAsync(bundleInfo, bundlePath);
                        return;
                    }

                    if (System.IO.File.Exists(bundlePath) is false)
                    {
                        SetException(new GameException($"Bundle file not found: {bundlePath}"));
                        return;
                    }

                    var bundle = AssetBundle.LoadFromFile(bundlePath);
                    if (bundle == null)
                    {
                        SetException(new GameException($"Bundle load failed: {bundlePath}"));
                        return;
                    }

                    SetResult(BundleHandle.Success(bundleInfo, bundle));
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            private async void LoadFromUriAsync(BundleInfo bundleInfo, string uri)
            {
                try
                {
                    using (var request = UnityWebRequestAssetBundle.GetAssetBundle(uri))
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

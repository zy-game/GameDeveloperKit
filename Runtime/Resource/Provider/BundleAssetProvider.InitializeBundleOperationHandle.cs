using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

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
            public override void Execute(params object[] args)
            {
                ExecuteAsync(args).Forget(UnityEngine.Debug.LogException);
            }

            private async UniTask ExecuteAsync(object[] args)
            {
                try
                {
                    var bundleInfo = args.Length > 0 ? args[0] as BundleInfo : null;
                    if (bundleInfo == null)
                    {
                        SetException(new ArgumentNullException(nameof(bundleInfo)));
                        return;
                    }

                    if (args.Length < 4 || args[1] is not ResourceMode mode || args[3] is not bool isRemote)
                    {
                        throw new ArgumentException("Bundle initialization requires mode, manifest version and remote source arguments.", nameof(args));
                    }

                    var manifestVersion = args.Length > 2 ? args[2] as string : null;
                    var bundlePath = ProviderBase.ResolveBundleFileName(bundleInfo);
                    var bundle = await AcquireBundleAsync(bundleInfo, mode, manifestVersion, isRemote);
                    if (bundle == null)
                    {
                        SetException(new GameException($"Bundle load failed: {bundlePath}"));
                        return;
                    }

                    SetResult(BundleHandle.Success(bundleInfo, bundle));
                    App.Debug.Info(
                        $"AssetBundle initialized. Name: {bundlePath}, Mode: {mode}, Source: {(isRemote ? "Remote" : "Packaged")}");
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

        }
    }
}

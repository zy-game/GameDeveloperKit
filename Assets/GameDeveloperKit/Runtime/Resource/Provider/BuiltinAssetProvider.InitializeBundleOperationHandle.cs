using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BuiltinAssetProvider
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
            /// <param name="bundleInfo">资源包信息。</param>
            /// <returns>资源包加载操作句柄。</returns>
            public static InitializeBundleOperationHandle Success(BundleInfo bundleInfo = null)
            {
                var handle = new InitializeBundleOperationHandle();
                handle.SetResult(BundleHandle.Success(bundleInfo, null));
                return handle;
            }

            /// <summary>
            /// 执行操作句柄逻辑。
            /// </summary>
            /// <param name="args">操作参数。</param>
            public override void Execute(params object[] args)
            {
                var bundleInfo = args.Length > 0 ? args[0] as BundleInfo : null;
                SetResult(BundleHandle.Success(bundleInfo, null));
            }
        }
    }
}

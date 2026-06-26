using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class EditorAssetProvider
    {
        /// <summary>
        /// 资源包卸载操作句柄。
        /// </summary>
        public sealed class UninitializeBundleOperationHandle : OperationHandle
        {
            /// <summary>
            /// 创建资源包卸载失败操作句柄。
            /// </summary>
            /// <param name="exception">错误信息。</param>
            /// <returns>资源包卸载操作句柄。</returns>
            public static UninitializeBundleOperationHandle Failure(Exception exception)
            {
                var handle = new UninitializeBundleOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            /// <summary>
            /// 创建资源包卸载成功操作句柄。
            /// </summary>
            /// <returns>资源包卸载操作句柄。</returns>
            public static UninitializeBundleOperationHandle Sucecess()
            {
                var handle = new UninitializeBundleOperationHandle();
                handle.SetResult();
                return handle;
            }

            /// <summary>
            /// 执行操作句柄逻辑。
            /// </summary>
            /// <param name="args">操作参数。</param>
            public override void Execute(params object[] args)
            {
                var bundle = args.Length > 1 ? args[1] as BundleHandle : null;
                bundle?.Release();
                SetResult();
            }
        }
    }
}

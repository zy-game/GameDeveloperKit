using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 定义 Web Asset Provider 类型。
    /// </summary>
    public sealed partial class WebAssetProvider
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
                try
                {
                    var bundle = args.Length > 1 ? args[1] as BundleHandle : null;
                    if (bundle == null)
                    {
                        SetException(new ArgumentNullException(nameof(bundle)));
                        return;
                    }

                    bundle.Release();
                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }
        }
    }
}

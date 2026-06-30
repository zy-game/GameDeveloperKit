using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 内置资源模式分部定义。
    /// </summary>
    public sealed partial class BuiltinMode
    {
        /// <summary>
        /// 资源包卸载操作句柄。
        /// </summary>
        public sealed class UninitializePackageOperationHandle : OperationHandle
        {
            /// <summary>
            /// 创建资源包卸载失败操作句柄。
            /// </summary>
            /// <param name="exception">错误信息。</param>
            /// <returns>资源包卸载操作句柄。</returns>
            public static UninitializePackageOperationHandle Failure(Exception exception)
            {
                var handle = new UninitializePackageOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            /// <summary>
            /// 创建资源包卸载成功操作句柄。
            /// </summary>
            /// <returns>资源包卸载操作句柄。</returns>
            public static UninitializePackageOperationHandle Success()
            {
                var handle = new UninitializePackageOperationHandle();
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
                    var packageName = args[0] as string;
                    var mode = args.Length > 1 ? args[1] as BuiltinMode : null;
                    Validate(packageName, mode);

                    var packageBundleNames = mode.GetPackageBundleNames(packageName);
                    var targets = mode._providers.Where(x => x.Info != null && packageBundleNames.Contains(x.Info.Name)).ToArray();
                    if (targets.Length == 0)
                    {
                        SetException(new GameException($"Package not initialized: {packageName}"));
                        return;
                    }

                    foreach (var provider in targets)
                    {
                        if (provider.ReleaseReference() > 0 || provider.HasLoadedAssets)
                        {
                            continue;
                        }

                        var operation = await provider.UninitializeProviderAsync();
                        if (operation.Status is not OperationStatus.Succeeded)
                        {
                            SetException(operation.Error ?? new GameException($"{packageName} uninitialize failed"));
                            return;
                        }

                        provider.Release();
                        mode._providers.Remove(provider);
                    }

                    if (mode._providers.Count == 0)
                    {
                        mode.Status = ResourceStatus.Released;
                    }

                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            /// <summary>
            /// 校验 member。
            /// </summary>
            /// <param name="packageName">package Name 参数。</param>
            /// <param name="mode">mode 参数。</param>
            private static void Validate(string packageName, BuiltinMode mode)
            {
                if (packageName == null)
                {
                    throw new ArgumentNullException(nameof(packageName));
                }

                if (packageName is not BUILTIN_PACKAGE_NAME)
                {
                    throw new ArgumentException($"Invalid package: {packageName}", nameof(packageName));
                }

                if (mode == null)
                {
                    throw new ArgumentNullException(nameof(mode));
                }
            }
        }
    }
}

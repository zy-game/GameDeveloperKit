using System;
using System.Collections.Generic;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源包模式分部定义。
    /// </summary>
    public sealed partial class BundleMode
    {
        /// <summary>
        /// 资源包初始化操作句柄。
        /// </summary>
        public sealed class InitializePackageOperationHandle : OperationHandle
        {
            /// <summary>
            /// 创建资源包初始化失败操作句柄。
            /// </summary>
            /// <param name="exception">错误信息。</param>
            /// <returns>资源包初始化操作句柄。</returns>
            public static InitializePackageOperationHandle Failure(Exception exception)
            {
                InitializePackageOperationHandle handle = new InitializePackageOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            /// <summary>
            /// 创建资源包初始化成功操作句柄。
            /// </summary>
            /// <returns>资源包初始化操作句柄。</returns>
            public static InitializePackageOperationHandle Success()
            {
                var handle = new InitializePackageOperationHandle();
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
                    var mode = args[1] as BundleMode;
                    var providers = mode?._providers;
                    var manifest = mode?.Manifest;
                    Validate(packageName, providers, manifest);
                    await PackageProviderInitializationTransaction.InitializeAsync(
                        packageName,
                        manifest,
                        providers,
                        ResourceAssetBundleProviderKind.Bundle);

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
            private static void Validate(string packageName, List<ProviderBase> providers, ManifestInfo manifest)
            {
                if (packageName == null)
                {
                    throw new ArgumentNullException(nameof(packageName));
                }

                if (string.IsNullOrWhiteSpace(packageName))
                {
                    throw new ArgumentException("Value cannot be empty.", nameof(packageName));
                }

                if (providers == null)
                {
                    throw new ArgumentNullException(nameof(providers));
                }

                if (manifest == null)
                {
                    throw new ArgumentNullException(nameof(manifest));
                }
            }

        }
    }
}

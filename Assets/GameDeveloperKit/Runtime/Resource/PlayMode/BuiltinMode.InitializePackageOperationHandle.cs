using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 内置资源模式分部定义。
    /// </summary>
    public sealed partial class BuiltinMode
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
                var handle = new InitializePackageOperationHandle();
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
                    var mode = args[1] as BuiltinMode;
                    var manifest = mode?.Manifest;
                    Validate(packageName, mode, manifest);

                    var builtinBundle = manifest.GetBundle(BUILTIN_PACKAGE_NAME);
                    if (builtinBundle == null)
                    {
                        SetException(new GameException($"{packageName} not found"));
                        return;
                    }

                    var provider = new BuiltinAssetProvider(builtinBundle);
                    var operation = await provider.InitializeProviderAsync();
                    if (operation.Status is not OperationStatus.Succeeded)
                    {
                        provider.Release();
                        SetException(operation.Error ?? new GameException($"{packageName} initialize failed"));
                        return;
                    }

                    mode.assetProvider = provider;
                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            private static void Validate(string packageName, BuiltinMode mode, ManifestInfo manifest)
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

                if (manifest == null)
                {
                    throw new ArgumentNullException(nameof(manifest));
                }
            }
        }
    }
}
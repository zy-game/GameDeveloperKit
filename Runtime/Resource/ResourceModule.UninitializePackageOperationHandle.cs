using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源模块分部定义。
    /// </summary>
    public sealed partial class ResourceModule
    {
        /// <summary>
        /// 资源包卸载操作句柄：释放包内 bundle 的 provider 引用，无引用且无在用资源时卸载。
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
                    var packageName = args[0] as string;
                    var module = args[1] as ResourceModule;
                    Validate(packageName, module);
                    await UninitializeAsync(packageName, module.Providers, module.PackageSessions);
                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            internal static async UniTask UninitializeAsync(
                string packageName,
                List<ProviderBase> providers,
                Dictionary<string, PackageSession> sessions)
            {
                if (sessions == null)
                {
                    throw new ArgumentNullException(nameof(sessions));
                }

                if (sessions.TryGetValue(packageName, out var session) is false)
                {
                    throw new GameException($"Package not initialized: {packageName}");
                }

                if (session.ReleaseReference() > 0)
                {
                    return;
                }

                sessions.Remove(packageName);
                foreach (var provider in session.Providers)
                {
                    if (provider.ReleaseReference() > 0 || provider.HasLoadedAssets)
                    {
                        continue;
                    }

                    var operation = await provider.UninitializeProviderAsync();
                    if (operation.Status is not OperationStatus.Succeeded)
                    {
                        throw operation.Error ?? new GameException($"Bundle uninitialize failed: {provider.Info.Name}");
                    }

                    provider.Release();
                    providers.Remove(provider);
                }
            }

            private static void Validate(string packageName, ResourceModule module)
            {
                if (packageName == null)
                {
                    throw new ArgumentNullException(nameof(packageName));
                }

                if (string.IsNullOrWhiteSpace(packageName))
                {
                    throw new ArgumentException("Value cannot be empty.", nameof(packageName));
                }

                if (module == null)
                {
                    throw new ArgumentNullException(nameof(module));
                }
            }
        }
    }
}

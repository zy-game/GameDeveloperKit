using System;
using System.Collections.Generic;
using System.Linq;
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
        /// 资源包初始化操作句柄：一个 bundle 一个 provider，按依赖顺序初始化，失败回滚。
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
            /// 执行操作句柄逻辑。
            /// </summary>
            /// <param name="args">操作参数。</param>
            public override async void Execute(params object[] args)
            {
                try
                {
                    var packageName = args[0] as string;
                    var module = args[1] as ResourceModule;
                    Validate(packageName, module);
                    await InitializeAsync(packageName, module.ManifestInternal, module.Providers, module.Mode);
                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            private static async UniTask InitializeAsync(string packageName, ManifestInfo manifest, List<ProviderBase> providers, ResourceMode mode)
            {
                if (manifest == null)
                {
                    throw new ArgumentNullException(nameof(manifest));
                }

                var bundles = manifest.GetPackageBundles(packageName);
                if (bundles == null)
                {
                    throw new GameException($"{packageName} not found");
                }

                var initializedProviders = new List<ProviderBase>();
                var retainedProviders = new List<ProviderBase>();
                foreach (var bundle in bundles)
                {
                    var existingProvider = providers.FirstOrDefault(x => x.Info != null && x.Info.Name == bundle.Name);
                    if (existingProvider != null)
                    {
                        existingProvider.RetainReference();
                        retainedProviders.Add(existingProvider);
                        continue;
                    }

                    var provider = ResourceProviderFactory.Create(bundle, mode);
                    var operation = await provider.InitializeProviderAsync();
                    if (operation.Status is not OperationStatus.Succeeded)
                    {
                        provider.Release();
                        Rollback(providers, initializedProviders, retainedProviders);
                        throw operation.Error ?? new GameException($"{packageName} initialize failed");
                    }

                    providers.Add(provider);
                    initializedProviders.Add(provider);
                }
            }

            private static void Rollback(List<ProviderBase> providers, IReadOnlyList<ProviderBase> initializedProviders, IReadOnlyList<ProviderBase> retainedProviders)
            {
                foreach (var provider in retainedProviders)
                {
                    provider.ReleaseReference();
                }

                foreach (var provider in initializedProviders)
                {
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

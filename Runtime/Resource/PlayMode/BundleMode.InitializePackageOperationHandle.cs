using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
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

                    var package = manifest.Packages.FirstOrDefault(x => x != null && x.Name == packageName);
                    if (package == null)
                    {
                        SetException(new GameException($"Package not found: {packageName}"));
                        return;
                    }

                    var bundles = GetPackageBundles(package, manifest);
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

                        var provider = new BundleAssetProvider(bundle);
                        var operation = await provider.InitializeProviderAsync();
                        if (operation.Status is not OperationStatus.Succeeded)
                        {
                            provider.Release();
                            RollbackProviderReferences(retainedProviders);
                            RollbackProviders(providers, initializedProviders);
                            SetException(operation.Error ?? new GameException($"Bundle initialize failed: {bundle.Name}"));
                            return;
                        }

                        providers.Add(provider);
                        initializedProviders.Add(provider);
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

            /// <summary>
            /// 获取 Package Bundles。
            /// </summary>
            private static IReadOnlyList<BundleInfo> GetPackageBundles(PackageInfo package, ManifestInfo manifest)
            {
                var bundles = new List<BundleInfo>();
                var visited = new HashSet<string>();
                if (package.Bundles == null)
                {
                    return bundles;
                }

                foreach (var bundle in package.Bundles)
                {
                    AddBundleWithDependencies(bundle, manifest, bundles, visited);
                }

                return bundles;
            }

            /// <summary>
            /// 添加 Bundle With Dependencies。
            /// </summary>
            private static void AddBundleWithDependencies(BundleInfo bundle, ManifestInfo manifest, List<BundleInfo> bundles, HashSet<string> visited)
            {
                if (bundle == null || string.IsNullOrWhiteSpace(bundle.Name) || visited.Add(bundle.Name) is false)
                {
                    return;
                }

                if (bundle.Dependencies != null)
                {
                    foreach (var dependencyName in bundle.Dependencies)
                    {
                        var dependency = manifest.GetBundle(dependencyName);
                        if (dependency == null)
                        {
                            throw new GameException($"Bundle dependency not found: {dependencyName}");
                        }

                        AddBundleWithDependencies(dependency, manifest, bundles, visited);
                    }
                }

                bundles.Add(bundle);
            }

            /// <summary>
            /// 执行 Rollback Providers。
            /// </summary>
            /// <param name="initializedProviders">initialized Providers 参数。</param>
            private static void RollbackProviders(List<ProviderBase> providers, IReadOnlyList<ProviderBase> initializedProviders)
            {
                foreach (var provider in initializedProviders)
                {
                    provider.Release();
                    providers.Remove(provider);
                }
            }

            private static void RollbackProviderReferences(IReadOnlyList<ProviderBase> retainedProviders)
            {
                foreach (var provider in retainedProviders)
                {
                    provider.ReleaseReference();
                }
            }
        }
    }
}

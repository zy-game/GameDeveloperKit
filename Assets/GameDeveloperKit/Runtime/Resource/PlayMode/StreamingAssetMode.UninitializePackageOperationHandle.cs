using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// StreamingAssets资源模式分部定义。
    /// </summary>
    public sealed partial class StreamingAssetMode
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
            /// 执行操作句柄逻辑。
            /// </summary>
            /// <param name="args">操作参数。</param>
            public override async void Execute(params object[] args)
            {
                try
                {
                    var packageName = args[0] as string;
                    var mode = args[1] as StreamingAssetMode;
                    var providers = mode?._providers;
                    var manifest = mode?.Manifest;
                    Validate(packageName, providers, manifest);

                    var package = manifest.Packages.FirstOrDefault(x => x != null && x.Name == packageName);
                    if (package == null || package.Bundles == null)
                    {
                        SetException(new GameException($"Package not found: {packageName}"));
                        return;
                    }

                    var packageBundleNames = GetPackageBundleNames(package, manifest);
                    var targets = providers.Where(x => x.Info != null && packageBundleNames.Contains(x.Info.Name)).ToArray();
                    if (targets.Length == 0)
                    {
                        SetException(new GameException($"Package not initialized: {packageName}"));
                        return;
                    }

                    foreach (var provider in targets)
                    {
                        var operation = await provider.UninitializeProviderAsync();
                        if (operation.Status is not OperationStatus.Succeeded)
                        {
                            SetException(operation.Error ?? new GameException($"Bundle uninitialize failed: {provider.Info.Name}"));
                            return;
                        }

                        provider.Release();
                        providers.Remove(provider);
                    }

                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            /// <summary>
            /// 获取 Package Bundle Names。
            /// </summary>
            /// <param name="package">package 参数。</param>
            /// <param name="manifest">manifest 参数。</param>
            /// <returns>执行结果。</returns>
            private static HashSet<string> GetPackageBundleNames(PackageInfo package, ManifestInfo manifest)
            {
                var bundleNames = new HashSet<string>();
                if (package.Bundles == null)
                {
                    return bundleNames;
                }

                foreach (var bundle in package.Bundles)
                {
                    AddBundleWithDependencies(bundle, manifest, bundleNames);
                }

                return bundleNames;
            }

            /// <summary>
            /// 添加 Bundle With Dependencies。
            /// </summary>
            /// <param name="bundle">bundle 参数。</param>
            /// <param name="manifest">manifest 参数。</param>
            /// <param name="bundleNames">bundle Names 参数。</param>
            private static void AddBundleWithDependencies(BundleInfo bundle, ManifestInfo manifest, HashSet<string> bundleNames)
            {
                if (bundle == null || string.IsNullOrWhiteSpace(bundle.Name) || bundleNames.Add(bundle.Name) is false)
                {
                    return;
                }

                if (bundle.Dependencies == null)
                {
                    return;
                }

                foreach (var dependencyName in bundle.Dependencies)
                {
                    var dependency = manifest.GetBundle(dependencyName);
                    if (dependency == null)
                    {
                        throw new GameException($"Bundle dependency not found: {dependencyName}");
                    }

                    AddBundleWithDependencies(dependency, manifest, bundleNames);
                }
            }

            /// <summary>
            /// 校验 member。
            /// </summary>
            /// <param name="packageName">package Name 参数。</param>
            /// <param name="providers">providers 参数。</param>
            /// <param name="manifest">manifest 参数。</param>
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

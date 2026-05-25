using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class WebGLMode
    {
        public sealed class InitializePackageOperationHandle : OperationHandle
        {
            public static InitializePackageOperationHandle Failure(Exception exception)
            {
                var handle = new InitializePackageOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            public override async void Execute(params object[] args)
            {
                try
                {
                    var packageName = args[0] as string;
                    var providers = args[1] as List<ProviderBase>;
                    var manifest = args.Length > 2 ? args[2] as ManifestInfo : null;
                    Validate(packageName, providers, manifest);

                    var package = manifest.Packages.FirstOrDefault(x => x != null && x.Name == packageName);
                    if (package == null)
                    {
                        SetException(new GameException($"Package not found: {packageName}"));
                        return;
                    }

                    var initializedProviders = new List<ProviderBase>();
                    foreach (var bundle in GetPackageBundles(package, manifest))
                    {
                        if (providers.Any(x => x.Info != null && x.Info.Name == bundle.Name))
                        {
                            continue;
                        }

                        var provider = new BundleProvider(bundle);
                        var operation = await provider.InitializeProviderAsync();
                        if (operation.Status is not OperationStatus.Succeeded)
                        {
                            provider.Release();
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

            private static void RollbackProviders(List<ProviderBase> providers, IReadOnlyList<ProviderBase> initializedProviders)
            {
                foreach (var provider in initializedProviders)
                {
                    provider.Release();
                    providers.Remove(provider);
                }
            }

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

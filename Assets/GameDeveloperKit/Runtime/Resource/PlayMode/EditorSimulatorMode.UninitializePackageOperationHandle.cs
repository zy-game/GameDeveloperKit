using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class EditorSimulatorMode
    {
        public sealed class UninitializePackageOperationHandle : OperationHandle
        {
            public static UninitializePackageOperationHandle Failure(Exception exception)
            {
                var handle = new UninitializePackageOperationHandle();
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

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源模式基类，定义了资源模式的基本接口和功能，包括检查资源和资源包的存在、初始化和卸载资源包、加载和卸载资源等方法。它包含一个ManifestInfo属性，用于存储资源清单的信息，并且提供了一系列抽象方法，要求具体的资源模式类必须实现这些方法来完成资源的加载和管理逻辑。通过继承ModeBase类，开发者可以创建不同类型的资源模式，以适应不同的资源加载需求和场景，从而提高游戏的性能和用户体验。
    /// </summary>
    public abstract class ModeBase : IReference
    {
        /// <summary>
        /// 资源清单
        /// </summary>
        public ManifestInfo Manifest { get; }

        /// <summary>
        /// 资源状态
        /// </summary>
        public ResourceStatus Status { get; protected set; } = ResourceStatus.None;

        /// <summary>
        /// 初始化资源模式。
        /// </summary>
        /// <param name="manifest">资源清单。</param>
        public ModeBase(ManifestInfo manifest)
        {
            this.Manifest = manifest;
        }

        /// <summary>
        /// 检查是否存在资源
        /// </summary>
        /// <param name="location">资源地址、类型名或标签。</param>
        /// <returns>如果资源模式包含该资源，则返回true；否则返回false。</returns>
        public abstract bool HasAsset(string location);

        /// <summary>
        /// 检查是否存在资源包
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>如果资源模式包含该资源包，则返回true；否则返回false。</returns>
        public abstract bool HasPackage(string package);

        /// <summary>
        /// 初始化资源包
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源包初始化操作句柄。</returns>
        public abstract UniTask<OperationHandle> InitializePackageAsync(string package);

        /// <summary>
        /// 卸载资源包
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源包卸载操作句柄。</returns>
        public abstract UniTask<OperationHandle> UninitializePackageAsync(string package);

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>资源加载句柄。</returns>
        public abstract UniTask<AssetHandle> LoadAssetAsync(string location);

        /// <summary>
        /// 基于资源标签加载资源
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>资源加载句柄列表。</returns>
        public abstract UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label);

        /// <summary>
        /// 基于资源类型加载资源
        /// </summary>
        /// <typeparam name="T">Unity资源类型。</typeparam>
        /// <returns>资源加载句柄列表。</returns>
        public abstract UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>() where T : UnityEngine.Object;

        /// <summary>
        /// 加载二进制资源
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>二进制资源句柄。</returns>
        public abstract UniTask<RawAssetHandle> LoadRawAssetAsync(string location);

        /// <summary>
        /// 基于资源标签加载二进制资源
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>二进制资源句柄列表。</returns>
        public abstract UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label);

        /// <summary>
        /// 加载场景资源
        /// </summary>
        /// <param name="name">场景资源地址或名称。</param>
        /// <returns>场景资源句柄。</returns>
        public abstract UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name);

        /// <summary>
        /// 卸载未使用的资源
        /// </summary>
        /// <returns>卸载任务。</returns>
        public abstract UniTask UnloadUnusedAssetAsync();

        /// <summary>
        /// 卸载资源句柄
        /// </summary>
        /// <param name="handle">资源句柄。</param>
        /// <returns>卸载任务。</returns>
        public abstract UniTask UnloadAsset(AssetHandle handle);

        /// <summary>
        /// 卸载二进制资源句柄。
        /// </summary>
        /// <param name="handle">二进制资源句柄。</param>
        /// <returns>卸载任务。</returns>
        public abstract UniTask UnloadRawAsset(RawAssetHandle handle);

        /// <summary>
        /// 卸载场景资源句柄。
        /// </summary>
        /// <param name="handle">场景资源句柄。</param>
        /// <returns>卸载任务。</returns>
        public abstract UniTask UnloadSceneAsset(SceneAssetHandle handle);

        /// <summary>
        /// 卸载资源模式
        /// </summary>
        public abstract void Release();

        /// <summary>
        /// 获取资源包直接包含的所有资源包名称。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源包名称集合。</returns>
        protected HashSet<string> GetPackageBundleNames(string package)
        {
            var bundleNames = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(package) || Manifest?.Packages == null)
            {
                return bundleNames;
            }

            PackageInfo packageInfo = null;
            foreach (var candidate in Manifest.Packages)
            {
                if (candidate != null && candidate.Name == package)
                {
                    packageInfo = candidate;
                    break;
                }
            }

            if (packageInfo?.Bundles == null)
            {
                return bundleNames;
            }

            foreach (var bundle in packageInfo.Bundles)
            {
                if (bundle != null && !string.IsNullOrWhiteSpace(bundle.Name))
                {
                    bundleNames.Add(bundle.Name);
                }
            }

            return bundleNames;
        }
    }

    internal static class PackageProviderInitializationTransaction
    {
        public static async UniTask InitializeAsync(
            string packageName,
            ManifestInfo manifest,
            List<ProviderBase> providers,
            ResourceAssetBundleProviderKind providerKind,
            string packageNotFoundMessage = null,
            Func<string, string> initializeFailedMessage = null)
        {
            Validate(packageName, manifest, providers);

            var package = manifest.Packages.FirstOrDefault(x => x != null && x.Name == packageName);
            if (package == null)
            {
                throw new GameException(packageNotFoundMessage ?? $"Package not found: {packageName}");
            }

            var initializedProviders = new List<ProviderBase>();
            var retainedProviders = new List<ProviderBase>();
            foreach (var bundle in GetPackageBundles(package, manifest))
            {
                var existingProvider = providers.FirstOrDefault(x => x.Info != null && x.Info.Name == bundle.Name);
                if (existingProvider != null)
                {
                    existingProvider.RetainReference();
                    retainedProviders.Add(existingProvider);
                    continue;
                }

                var provider = ResourceProviderFactory.Create(bundle, providerKind);
                var operation = await provider.InitializeProviderAsync();
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    provider.Release();
                    RollbackProviderReferences(retainedProviders);
                    RollbackProviders(providers, initializedProviders);
                    var message = initializeFailedMessage?.Invoke(bundle.Name) ?? $"Bundle initialize failed: {bundle.Name}";
                    throw operation.Error ?? new GameException(message);
                }

                providers.Add(provider);
                initializedProviders.Add(provider);
            }
        }

        private static void Validate(string packageName, ManifestInfo manifest, List<ProviderBase> providers)
        {
            if (packageName == null)
            {
                throw new ArgumentNullException(nameof(packageName));
            }

            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(packageName));
            }

            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
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

        private static void RollbackProviderReferences(IReadOnlyList<ProviderBase> retainedProviders)
        {
            foreach (var provider in retainedProviders)
            {
                provider.ReleaseReference();
            }
        }
    }
}

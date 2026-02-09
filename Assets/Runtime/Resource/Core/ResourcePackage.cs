using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源包
    /// 替代原有的 ManagerBase 和 IPackageManager
    /// </summary>
    public class ResourcePackage : IPackageManager
    {
        private IPackageLoader _loader;
        private ResourceLocator _locator;
        private ProviderSystem _providerSystem;
        private PackageManifest _manifest;

        public string Name { get; private set; }
        public string Version { get; private set; }
        public PackageStatus Status { get; private set; }

        public ResourcePackage(string name, string version, IPackageLoader loader)
        {
            Name = name;
            Version = version;
            Status = PackageStatus.None;

            _loader = loader;
            _locator = new ResourceLocator();
            _providerSystem = new ProviderSystem();
        }

        /// <summary>
        /// 获取 Provider 系统（用于调试面板）
        /// </summary>
        public ProviderSystem GetProviderSystem() => _providerSystem;

        /// <summary>
        /// 初始化资源包
        /// </summary>
        public async UniTask<bool> Initialization(IResourceManager resourceManager)
        {
            if (Status == PackageStatus.Ready)
            {
                Game.Debug.Warning($"Package '{Name}' is already initialized");
                return true;
            }

            if (Status == PackageStatus.Initializing)
            {
                Game.Debug.Warning($"Package '{Name}' is initializing");
                return false;
            }

            Status = PackageStatus.Initializing;

            try
            {
                // 1. 加载 Manifest
                Game.Debug.Debug($"[{Name}] Loading manifest...");
                _manifest = await _loader.LoadManifestAsync();

                if (_manifest == null)
                {
                    Game.Debug.Error($"[{Name}] Failed to load manifest");
                    Status = PackageStatus.Failed;
                    return false;
                }

                // 2. 构建索引
                Game.Debug.Debug($"[{Name}] Building resource index...");
                _locator.BuildIndex(_manifest);

                // 3. 初始化 Bundle 依赖图（如果使用 Bundle 加载）
                if (_loader is BundlePackageLoader)
                {
                    var bundleService = (resourceManager as ResourceModule)?.GetBundleService();
                    if (bundleService != null)
                    {
                        Game.Debug.Debug($"[{Name}] Initializing bundle dependencies...");
                        bundleService.InitializeDependencies(_manifest);
                    }
                }

                // 4. 准备资源（下载等）
                Game.Debug.Debug($"[{Name}] Preparing resources...");
                var prepared = await _loader.PrepareResourcesAsync(_manifest);

                if (!prepared)
                {
                    Game.Debug.Error($"[{Name}] Failed to prepare resources");
                    Status = PackageStatus.Failed;
                    return false;
                }

                Status = PackageStatus.Ready;
                Game.Debug.Debug($"[{Name}:{Version}] Initialization completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"[{Name}] Initialization exception: {ex.Message}");
                Status = PackageStatus.Failed;
                return false;
            }
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public async UniTask<AssetHandle<T>> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            if (Status != PackageStatus.Ready)
            {
                Game.Debug.Error($"[{Name}] Package is not ready (Status: {Status}) for asset: {address}");
                return AssetHandle<T>.Failure(address);
            }

            // 1. 定位资源
            var location = _locator.Locate(address);
            if (location == null)
            {
                Game.Debug.Error($"[{Name}] Asset '{address}' not found in package");
                return AssetHandle<T>.Failure(address);
            }

            // 2. 获取 Provider
            var provider = _providerSystem.GetProvider<T>(location);
            if (provider == null)
            {
                Game.Debug.Error($"[{Name}] No provider found for asset '{address}' (type: {typeof(T).Name})");
                return AssetHandle<T>.Failure(address);
            }

            // 3. 加载资源
            return await provider.LoadAsync<T>(location);
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public async UniTask<SceneHandle> LoadSceneAsync(string sceneName, LoadSceneMode mode, Action<float> progressHandler = null)
        {
            if (Status != PackageStatus.Ready)
            {
                Game.Debug.Error($"[{Name}] Package is not ready (Status: {Status})");
                return default;
            }

            // 1. 定位场景
            var location = _locator.Locate(sceneName);
            if (location == null)
            {
                Game.Debug.Error($"[{Name}] Scene '{sceneName}' not found");
                return default;
            }

            // 2. 获取场景 Provider
            var provider = _providerSystem.GetSceneProvider(location);
            if (provider == null)
            {
                Game.Debug.Error($"[{Name}] No scene provider found for '{sceneName}'");
                return default;
            }

            // 3. 加载场景
            return await provider.LoadSceneAsync(location, mode, progressHandler);
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(BaseHandle handle)
        {
            if (handle == null)
                return;

            var location = _locator.Locate(handle.Address);
            if (location == null)
                return;

            var provider = _providerSystem.GetProvider<UnityEngine.Object>(location);
            provider?.Unload(handle);
        }

        /// <summary>
        /// 卸载未使用的资源
        /// </summary>
        public void UnloadUnusedAssets()
        {
            // 由各个 Provider 实现具体的卸载逻辑
            Game.Debug.Debug($"[{Name}] Unloading unused assets...");
        }

        /// <summary>
        /// 通过 Label 批量加载资源
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAssetsByLabelAsync<T>(string label)
            where T : UnityEngine.Object
        {
            if (Status != PackageStatus.Ready)
            {
                Game.Debug.Error($"[{Name}] Package is not ready (Status: {Status})");
                return new List<AssetHandle<T>>();
            }

            var locations = _locator.LocateByLabel(label);
            var handles = new List<AssetHandle<T>>();

            foreach (var location in locations)
            {
                var provider = _providerSystem.GetProvider<T>(location);
                if (provider != null)
                {
                    var handle = await provider.LoadAsync<T>(location);
                    if (handle != null)
                        handles.Add(handle);
                }
            }

            Game.Debug.Debug($"[{Name}] Loaded {handles.Count} assets with label '{label}'");
            return handles;
        }

        /// <summary>
        /// 通过多个 Label 批量加载资源（交集）
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAssetsByLabelsAsync<T>(params string[] labels)
            where T : UnityEngine.Object
        {
            if (Status != PackageStatus.Ready)
            {
                Game.Debug.Error($"[{Name}] Package is not ready (Status: {Status})");
                return new List<AssetHandle<T>>();
            }

            var locations = _locator.LocateByLabels(labels);
            var handles = new List<AssetHandle<T>>();

            foreach (var location in locations)
            {
                var provider = _providerSystem.GetProvider<T>(location);
                if (provider != null)
                {
                    var handle = await provider.LoadAsync<T>(location);
                    if (handle != null)
                        handles.Add(handle);
                }
            }

            Game.Debug.Debug($"[{Name}] Loaded {handles.Count} assets with labels [{string.Join(", ", labels)}]");
            return handles;
        }

        /// <summary>
        /// 加载 Bundle 中的所有资源
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAllAssetsInBundleAsync<T>(string bundleName)
            where T : UnityEngine.Object
        {
            if (Status != PackageStatus.Ready)
            {
                Game.Debug.Error($"[{Name}] Package is not ready (Status: {Status})");
                return new List<AssetHandle<T>>();
            }

            var locations = _locator.LocateByBundle(bundleName);
            var handles = new List<AssetHandle<T>>();

            foreach (var location in locations)
            {
                var provider = _providerSystem.GetProvider<T>(location);
                if (provider != null)
                {
                    var handle = await provider.LoadAsync<T>(location);
                    if (handle != null)
                        handles.Add(handle);
                }
            }

            Game.Debug.Debug($"[{Name}] Loaded {handles.Count} assets from bundle '{bundleName}'");
            return handles;
        }

        /// <summary>
        /// 加载子资源（如 Atlas 中的 Sprite）
        /// </summary>
        public async UniTask<AssetHandle<T>> LoadSubAssetAsync<T>(string address, string subAssetName)
            where T : UnityEngine.Object
        {
            if (Status != PackageStatus.Ready)
            {
                Game.Debug.Error($"[{Name}] Package is not ready (Status: {Status})");
                return default;
            }

            var location = _locator.Locate(address);
            if (location == null)
            {
                Game.Debug.Error($"[{Name}] Asset '{address}' not found");
                return default;
            }

            var provider = _providerSystem.GetProvider<T>(location);
            if (provider == null)
            {
                Game.Debug.Error($"[{Name}] No provider found for asset '{address}'");
                return default;
            }

            return await provider.LoadSubAssetAsync<T>(location, subAssetName);
        }

        /// <summary>
        /// 通过类型批量加载资源（支持继承类型匹配）
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAssetsByTypeAsync<T>()
            where T : UnityEngine.Object
        {
            if (Status != PackageStatus.Ready)
            {
                Game.Debug.Error($"[{Name}] Package is not ready (Status: {Status})");
                return new List<AssetHandle<T>>();
            }

            var locations = _locator.LocateByType(typeof(T));
            var handles = new List<AssetHandle<T>>();

            foreach (var location in locations)
            {
                var provider = _providerSystem.GetProvider<T>(location);
                if (provider != null)
                {
                    var handle = await provider.LoadAsync<T>(location);
                    if (handle != null)
                        handles.Add(handle);
                }
            }

            Game.Debug.Debug($"[{Name}] Loaded {handles.Count} assets of type '{typeof(T).Name}'");
            return handles;
        }

        /// <summary>
        /// 是否包含资源
        /// </summary>
        public bool Contains(string address)
        {
            return _locator.Contains(address);
        }

        /// <summary>
        /// 清理
        /// </summary>
        public void OnClearup()
        {
            _locator.Clear();
            _providerSystem.Clear();
            _manifest = null;
            Status = PackageStatus.None;

            Game.Debug.Debug($"[{Name}] Package cleared");
        }
    }
}

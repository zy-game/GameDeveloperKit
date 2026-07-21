using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源模块
    /// </summary>
    [ModuleDependency(typeof(OperationModule))]
    [ModuleDependency(typeof(DownloadModule))]
    [ModuleDependency(typeof(FileModule))]
    public sealed partial class ResourceModule : GameModuleBase
    {
        private ResourceManifestIndex _manifestIndex;
        private ResourceSettings _setting;
        private ResourceMode _mode = ResourceMode.Offline;
        private readonly List<ProviderBase> _providers = new List<ProviderBase>();
        private readonly Dictionary<string, PackageSession> _packageSessions =
            new Dictionary<string, PackageSession>(StringComparer.Ordinal);
        private readonly SemaphoreSlim _packageLifecycleGate = new SemaphoreSlim(1, 1);
        private readonly NetworkAssetProvider _network = new NetworkAssetProvider();
        private ResourceInitializeState _initializeState = ResourceInitializeState.NotInitialized;
        private UniTaskCompletionSource _initializeCompletion;
        private Exception _initializeError;
        private Exception _startupError;
        internal List<ProviderBase> Providers => _providers;
        internal Dictionary<string, PackageSession> PackageSessions => _packageSessions;

        internal ResourceManifestIndex ManifestIndexInternal => _manifestIndex;

        internal ResourceMode Mode => _mode;

        /// <summary>
        /// 资源设置
        /// </summary>
        public ResourceSettings Settings => _setting;

        /// <summary>
        /// 资源模块是否已完成显式初始化。
        /// </summary>
        public bool IsInitialized => _initializeState == ResourceInitializeState.Initialized;

        public bool IsStartupReady => _initializeState == ResourceInitializeState.LocalInitialized || IsInitialized;

        public bool IsLocalInitialized => IsStartupReady;

        /// <summary>
        /// 资源模块显式初始化状态。
        /// </summary>
        public ResourceInitializeState InitializeState => _initializeState;

        /// <summary>
        /// 启动资源模块同步外壳。
        /// </summary>
        public override void Startup()
        {
            _setting = null;
            _manifestIndex = null;
            _mode = ResourceMode.Offline;
            ReleaseProviders();
            _packageSessions.Clear();
            _initializeCompletion = null;
            _initializeError = null;
            _initializeState = ResourceInitializeState.NotInitialized;
            _startupError = null;
            try
            {
                var manifestIndex = LoadStartupManifestIndex();
                if (manifestIndex != null)
                {
                    ApplyStartupManifestIndex(manifestIndex);
                }
            }
            catch (Exception exception)
            {
                _startupError = exception;
                ReleaseProviders();
                _setting = null;
                _manifestIndex = null;
                _initializeState = ResourceInitializeState.NotInitialized;
            }
        }

        /// <summary>
        /// 显式初始化资源模块。
        /// </summary>
        /// <param name="settings">资源设置。</param>
        /// <returns>初始化任务。</returns>
        public async UniTask InitializeAsync(ResourceSettings settings)
        {
            if (_initializeState == ResourceInitializeState.Initialized)
            {
                return;
            }

            if (_initializeState == ResourceInitializeState.Initializing && _initializeCompletion != null)
            {
                await _initializeCompletion.Task;
                if (_initializeState == ResourceInitializeState.Initialized)
                {
                    return;
                }

                if (_initializeError != null)
                {
                    throw _initializeError;
                }
            }

            var setting = ResolveSettings(settings);
            var hadPriorLocalState = _initializeState == ResourceInitializeState.LocalInitialized &&
                                     _setting != null &&
                                     _manifestIndex != null;
            var completionSource = new UniTaskCompletionSource();
            _initializeCompletion = completionSource;
            _initializeError = null;
            _initializeState = ResourceInitializeState.Initializing;
            try
            {
                var manifestIndex = await LoadManifestIndexAsync(setting);

                if (!ReferenceEquals(_initializeCompletion, completionSource) || _initializeState != ResourceInitializeState.Initializing)
                {
                    throw new GameException("ResourceModule initialization was interrupted.");
                }

                await ApplyManifestIndexAsync(setting, manifestIndex);
                _initializeState = ResourceInitializeState.Initialized;
                _initializeError = null;
                completionSource.TrySetResult();
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(_initializeCompletion, completionSource) && _initializeState == ResourceInitializeState.Initializing)
                {
                    if (hadPriorLocalState)
                    {
                        _initializeState = ResourceInitializeState.LocalInitialized;
                    }
                    else
                    {
                        ReleaseProviders();
                        _packageSessions.Clear();
                        _setting = null;
                        _manifestIndex = null;
                        _initializeState = ResourceInitializeState.Failed;
                    }
                }

                _initializeError = exception;
                completionSource.TrySetResult();
                throw;
            }
            finally
            {
                if (ReferenceEquals(_initializeCompletion, completionSource))
                {
                    _initializeCompletion = null;
                }
            }
        }

        public string GetPublishAddress(ResourceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return settings.GetPublishAddress();
        }

        public string GetManifestAddress(ResourceSettings settings, string version)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return settings.GetManifestAddress(version);
        }

        public string GetAssetAddress(ResourceSettings settings, string name, string version)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return settings.GetAssetAddress(name, version);
        }

        public async UniTask PreloadDefaultPackagesAsync()
        {
            EnsureReady();
            if (_setting?.DefaultPackages == null)
            {
                return;
            }

            for (var i = 0; i < _setting.DefaultPackages.Length; i++)
            {
                var package = _setting.DefaultPackages[i];
                if (string.IsNullOrWhiteSpace(package))
                {
                    continue;
                }

                if (HasPackage(package))
                {
                    continue;
                }

                var operation = await InitializePackageAsync(package);
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    throw new GameException($"Default package initialize failed: {package}", operation.Error);
                }
            }
        }

        /// <summary>
        /// 显式反初始化资源模块。
        /// </summary>
        /// <returns>反初始化任务。</returns>
        public UniTask UninitializeAsync()
        {
            return UninitializeInternalAsync();
        }

        /// <summary>
        /// 关闭资源模块。
        /// </summary>
        public override void Shutdown()
        {
            ReleaseProviders();
            _packageSessions.Clear();
            _manifestIndex = null;
            _setting = null;
            _initializeCompletion = null;
            _initializeError = null;
            _initializeState = ResourceInitializeState.NotInitialized;
        }

        /// <summary>
        /// 初始化资源包。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源包初始化操作句柄。</returns>
        public async UniTask<OperationHandle> InitializePackageAsync(string package)
        {
            ValidateKey(package, nameof(package));
            EnsureReady();
            await _packageLifecycleGate.WaitAsync();
            try
            {
                EnsureReady();
                return await App.Operation.WaitCompletionWithKeyAsync<InitializePackageOperationHandle>(package, package, this);
            }
            finally
            {
                _packageLifecycleGate.Release();
            }
        }

        /// <summary>
        /// 卸载资源包。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源包卸载操作句柄。</returns>
        public async UniTask<OperationHandle> UninitializePackageAsync(string package)
        {
            ValidateKey(package, nameof(package));
            EnsureReady();
            await _packageLifecycleGate.WaitAsync();
            try
            {
                EnsureReady();
                return await App.Operation.WaitCompletionWithKeyAsync<UninitializePackageOperationHandle>(package, package, this);
            }
            finally
            {
                _packageLifecycleGate.Release();
            }
        }

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>资源加载句柄。</returns>
        public UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            ValidateKey(location, nameof(location));
            EnsureReady();
            if (NetworkAssetProvider.IsNetworkLocation(location))
            {
                return _network.LoadAssetAsync(location);
            }

            var provider = ResolveProvider(location, "Asset", out var resolvedLocation);
            return provider.LoadAssetAsync(resolvedLocation);
        }

        /// <summary>
        /// 根据资源标签异步加载资源。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>资源加载句柄列表。</returns>
        public async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            EnsureReady();
            var providers = GetProvidersByLabel(label);
            if (providers.Count == 0)
            {
                throw new GameException($"No provider contains assets with label: {label}");
            }

            var items = CreateBatchItems(providers, provider => provider.GetAssetsByLabel(label));
            return await ResourceBatchLoader.LoadAsync(
                items,
                _setting.MaxConcurrentBatchLoads,
                item => item.Provider.LoadAssetByInfoAsync(item.Asset),
                "asset");
        }

        /// <summary>
        /// 根据资源类型加载资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <returns>加载的资源列表。</returns>
        public async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>() where T : UnityEngine.Object
        {
            EnsureReady();
            var assetTypeName = typeof(T).Name;
            var providers = GetProvidersByType(assetTypeName);
            if (providers.Count == 0)
            {
                throw new GameException($"No provider contains assets of type: {typeof(T).FullName}");
            }

            var items = CreateBatchItems(providers, provider => provider.GetAssetsByType(assetTypeName));
            return await ResourceBatchLoader.LoadAsync(
                items,
                _setting.MaxConcurrentBatchLoads,
                item => item.Provider.LoadAssetByInfoAsync(item.Asset),
                "asset");
        }

        /// <summary>
        /// 异步加载二进制资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>二进制资源句柄。</returns>
        public UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            ValidateKey(location, nameof(location));
            EnsureReady();
            if (NetworkAssetProvider.IsNetworkLocation(location))
            {
                return _network.LoadRawAssetAsync(location);
            }

            var provider = ResolveProvider(location, "Raw asset", out var resolvedLocation);
            return provider.LoadRawAssetAsync(resolvedLocation);
        }

        /// <summary>
        /// 根据资源标签加载二进制资源。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>二进制资源句柄列表。</returns>
        public async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            EnsureReady();
            var providers = GetProvidersByLabel(label);
            if (providers.Count == 0)
            {
                throw new GameException($"No provider contains assets with label: {label}");
            }

            var items = CreateBatchItems(providers, provider => provider.GetAssetsByLabel(label));
            return await ResourceBatchLoader.LoadAsync(
                items,
                _setting.MaxConcurrentBatchLoads,
                item => item.Provider.LoadRawAssetByInfoAsync(item.Asset),
                "raw asset");
        }

        private static IReadOnlyList<ResourceBatchItem> CreateBatchItems(
            IReadOnlyList<ProviderBase> providers,
            Func<ProviderBase, IReadOnlyList<AssetInfo>> selector)
        {
            var items = new List<ResourceBatchItem>();
            foreach (var provider in providers)
            {
                foreach (var asset in selector(provider))
                {
                    items.Add(new ResourceBatchItem(provider, asset));
                }
            }

            return items;
        }

        /// <summary>
        /// 加载场景资源。
        /// </summary>
        /// <param name="name">场景名称。</param>
        /// <returns>场景资源句柄。</returns>
        public UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name)
        {
            ValidateKey(name, nameof(name));
            EnsureReady();
            var provider = ResolveProvider(name, "Scene", out var resolvedLocation);
            return provider.LoadSceneAssetAsync(resolvedLocation);
        }

        /// <summary>
        /// 卸载未使用的资源。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async UniTask UnloadUnusedAssetAsync()
        {
            EnsureReady();
            await _network.UnloadUnusedAssetAsync();
            foreach (var provider in _providers.ToArray())
            {
                await provider.UnloadUnusedAssetAsync();
                if (provider.IsReferenced || provider.HasLoadedAssets)
                {
                    continue;
                }

                var operation = await provider.UninitializeProviderAsync();
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    throw new GameException($"Bundle uninitialize failed: {provider.Info?.Name}", operation.Error);
                }

                provider.Release();
                _providers.Remove(provider);
            }

            await UnityEngine.Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 卸载资源。
        /// </summary>
        /// <param name="handle">资源句柄。</param>
        /// <returns>异步任务。</returns>
        public UniTask UnloadAsset(AssetHandle handle)
        {
            if (handle != null && ReferenceEquals(handle.Owner, _network))
            {
                return _network.UnloadAsset(handle);
            }

            return UnloadHandle(handle, (p, h) => p.UnloadAsset(h), "Asset");
        }

        /// <summary>
        /// 卸载二进制资源。
        /// </summary>
        /// <param name="handle">二进制资源句柄。</param>
        /// <returns>异步任务。</returns>
        public UniTask UnloadRawAsset(RawAssetHandle handle)
        {
            if (handle != null && ReferenceEquals(handle.Owner, _network))
            {
                return _network.UnloadRawAsset(handle);
            }

            return UnloadHandle(handle, (p, h) => p.UnloadRawAsset(h), "Raw asset");
        }

        /// <summary>
        /// 卸载场景资源。
        /// </summary>
        /// <param name="handle">场景资源句柄。</param>
        /// <returns>异步任务。</returns>
        public UniTask UnloadSceneAsset(SceneAssetHandle handle)
        {
            return UnloadHandle(handle, (p, h) => p.UnloadSceneAsset(h), "Scene");
        }
        //__APPEND_MARKER2__

        /// <summary>
        /// 判断资源包是否已经初始化。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>如果资源包已经初始化，则返回true；否则返回false。</returns>
        public bool HasPackage(string package)
        {
            return string.IsNullOrWhiteSpace(package) is false && _packageSessions.ContainsKey(package);
        }

        private UniTask UnloadHandle<THandle>(THandle handle, Func<ProviderBase, THandle, UniTask> unloader, string assetTypeLabel)
            where THandle : ResourceHandle
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            EnsureReady();
            if (handle.Info == null)
            {
                return UniTask.CompletedTask;
            }

            var location = handle.Info.Location;
            var provider = GetProvider(location);
            if (provider == null)
            {
                throw new GameException($"{assetTypeLabel} not found: {location}");
            }

            return unloader(provider, handle);
        }

        private ProviderBase GetProvider(string location)
        {
            if (_manifestIndex == null ||
                _manifestIndex.TryResolveAssetAddress(location, out var bundleName, out _) is false)
            {
                return null;
            }

            return GetProviderByBundleName(bundleName);
        }

        private ProviderBase ResolveProvider(string address, string assetType, out string location)
        {
            if (_manifestIndex == null ||
                _manifestIndex.TryResolveAssetAddress(address, out var bundleName, out location) is false)
            {
                if (_manifestIndex?.IsAssetAddressAmbiguous(address) == true)
                {
                    throw new GameException($"{assetType} address is ambiguous: {address}");
                }

                throw new GameException($"{assetType} not found at address: {address}");
            }

            var provider = GetProviderByBundleName(bundleName);
            if (provider == null)
            {
                throw new GameException(
                    $"{assetType} address belongs to a bundle that is not initialized: {address}. Bundle: {bundleName}");
            }

            return provider;
        }

        private List<ProviderBase> GetProvidersByLabel(string label)
        {
            return GetProvidersByBundleNames(_manifestIndex?.GetBundleNamesByLabel(label));
        }

        private List<ProviderBase> GetProvidersByType(string typeName)
        {
            return GetProvidersByBundleNames(_manifestIndex?.GetBundleNamesByType(typeName));
        }

        private List<ProviderBase> GetProvidersByBundleNames(IReadOnlyList<string> bundleNames)
        {
            var providers = new List<ProviderBase>();
            if (bundleNames == null)
            {
                return providers;
            }

            foreach (var bundleName in bundleNames)
            {
                var provider = GetProviderByBundleName(bundleName);
                if (provider != null)
                {
                    providers.Add(provider);
                }
            }

            return providers;
        }

        private ProviderBase GetProviderByBundleName(string bundleName)
        {
            return _providers.FirstOrDefault(provider =>
                provider.Info != null &&
                string.Equals(provider.Info.Name, bundleName, StringComparison.Ordinal));
        }



        /// <summary>
        /// 解析资源初始化设置。
        /// </summary>
        private static ResourceSettings ResolveSettings(ResourceSettings settings)
        {
            if (settings == null)
            {
                throw new GameException("ResourceSettings is required. Configure FrameworkStartupModuleOptions.ResourceSettings or pass ResourceSettings to InitializeAsync.");
            }

            ResourceSettings.ValidateBatchLoadConcurrency(settings.MaxConcurrentBatchLoads);
            return settings;
        }

        /// <summary>
        /// 验证参数。
        /// </summary>
        private static void ValidateKey(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }

        /// <summary>
        /// 确保资源模块已同步准备。
        /// </summary>
        private void EnsureReady()
        {
            if ((_initializeState != ResourceInitializeState.Initializing && IsLocalInitialized is false) ||
                _setting == null ||
                _manifestIndex == null)
            {
                if (_startupError != null)
                {
                    throw new GameException("ResourceModule startup resource initialization failed.", _startupError);
                }

                throw new GameException("ResourceModule is not initialized. Call InitializeAsync first, or ensure the startup manifest is available.");
            }
        }

        /// <summary>
        /// 释放所有资源提供者。
        /// </summary>
        private void ReleaseProviders()
        {
            ReleaseProviders(_providers);
            _network.Release();
        }

        private static void ReleaseProviders(List<ProviderBase> providers)
        {
            foreach (var provider in providers.ToArray())
            {
                provider.Release();
            }

            providers.Clear();
        }

        private static async UniTask UnloadAllProviderScenesAsync(IReadOnlyList<ProviderBase> providers)
        {
            foreach (var provider in providers)
            {
                provider.ValidateCanUnloadAllScenes();
            }

            foreach (var provider in providers)
            {
                await provider.UnloadAllSceneAssetsAsync();
            }
        }

        private static async UniTask StopAndDrainProviderLoadsAsync(IReadOnlyList<ProviderBase> providers)
        {
            foreach (var provider in providers)
            {
                await provider.StopAndDrainPendingLoadsAsync();
            }
        }

        private static void ResumeProviderLoads(IReadOnlyList<ProviderBase> providers)
        {
            foreach (var provider in providers)
            {
                provider.ResumeLoadsAfterTeardownFailure();
            }
        }

        /// <summary>
        /// 执行显式反初始化流程。
        /// </summary>
        private async UniTask UninitializeInternalAsync()
        {
            if (_initializeState == ResourceInitializeState.Initializing && _initializeCompletion != null)
            {
                await _initializeCompletion.Task;
            }

            await _packageLifecycleGate.WaitAsync();
            try
            {
                try
                {
                    await _network.StopAndDrainPendingLoadsAsync();
                    await StopAndDrainProviderLoadsAsync(_providers);
                    await UnloadAllProviderScenesAsync(_providers);
                }
                catch
                {
                    _network.ResumeLoadsAfterTeardownFailure();
                    ResumeProviderLoads(_providers);
                    throw;
                }

                ReleaseProviders();
                _packageSessions.Clear();
                _manifestIndex = null;
                _setting = null;
                _initializeState = ResourceInitializeState.NotInitialized;
                _initializeCompletion = null;
                _initializeError = null;
            }
            finally
            {
                _packageLifecycleGate.Release();
            }
        }

    }
}

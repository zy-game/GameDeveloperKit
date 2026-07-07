using System;
using System.Collections.Generic;
using System.Linq;
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
        private ManifestInfo _manifest;
        private ResourceSettings _setting;
        private ResourceMode _mode = ResourceMode.Offline;
        private readonly List<ProviderBase> _providers = new List<ProviderBase>();
        private readonly NetworkAssetProvider _network = new NetworkAssetProvider();
        private ResourceInitializeState _initializeState = ResourceInitializeState.NotInitialized;
        private UniTaskCompletionSource _initializeCompletion;
        private Exception _startupError;
        internal List<ProviderBase> Providers => _providers;

        internal ManifestInfo ManifestInternal => _manifest;

        internal ResourceMode Mode => _mode;

        /// <summary>
        /// 资源清单
        /// </summary>
        public ManifestInfo Manifest => _manifest;

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
            _manifest = null;
            _mode = ResourceMode.Offline;
            ReleaseProviders();
            _initializeCompletion = null;
            _initializeState = ResourceInitializeState.NotInitialized;
            _startupError = null;
            try
            {
                var manifest = LoadStartupManifest();
                if (manifest != null)
                {
                    ApplyStartupManifest(manifest);
                }
            }
            catch (Exception exception)
            {
                _startupError = exception;
                ReleaseProviders();
                _setting = null;
                _manifest = null;
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
            }

            var setting = ResolveSettings(settings);
            var completionSource = new UniTaskCompletionSource();
            _initializeCompletion = completionSource;
            _initializeState = ResourceInitializeState.Initializing;
            try
            {
                await LoadAndApplyManifestAsync(setting);

                if (!ReferenceEquals(_initializeCompletion, completionSource) || _initializeState != ResourceInitializeState.Initializing)
                {
                    throw new GameException("ResourceModule initialization was interrupted.");
                }

                _initializeState = ResourceInitializeState.Initialized;
                completionSource.TrySetResult();
            }
            catch (Exception exception)
            {
                ReleaseProviders();
                _setting = null;
                _manifest = null;
                if (ReferenceEquals(_initializeCompletion, completionSource) && _initializeState == ResourceInitializeState.Initializing)
                {
                    _initializeState = ResourceInitializeState.Failed;
                }

                completionSource.TrySetException(exception);
                completionSource.Task.Forget(_ => { });
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
            _manifest = null;
            _setting = null;
            _initializeCompletion = null;
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
            var operation = await App.Operation.WaitCompletionWithKeyAsync<InitializePackageOperationHandle>(package, package, this);
            return operation;
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
            var operation = await App.Operation.WaitCompletionWithKeyAsync<UninitializePackageOperationHandle>(package, package, this);
            return operation;
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

            var provider = GetProvider(location);
            if (provider == null)
            {
                throw new GameException($"Asset not found at location: {location}");
            }

            return provider.LoadAssetAsync(location);
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
            var providers = GetProviders(label);
            if (providers.Count == 0)
            {
                throw new GameException($"No provider contains assets with label: {label}");
            }

            var handles = new List<AssetHandle>();
            foreach (var provider in providers)
            {
                handles.AddRange(await provider.LoadAssetsByLabelAsync(label));
            }

            return handles;
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
            var providers = GetProviders(assetTypeName);
            if (providers.Count == 0)
            {
                throw new GameException($"No provider contains assets of type: {typeof(T).FullName}");
            }

            var handles = new List<AssetHandle>();
            foreach (var provider in providers)
            {
                handles.AddRange(await provider.LoadAssetsByTypeAsync<T>());
            }

            return handles;
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

            var provider = GetProvider(location);
            if (provider == null)
            {
                throw new GameException($"Asset not found at location: {location}");
            }

            return provider.LoadRawAssetAsync(location);
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
            var providers = GetProviders(label);
            if (providers.Count == 0)
            {
                throw new GameException($"No provider contains assets with label: {label}");
            }

            var handles = new List<RawAssetHandle>();
            foreach (var provider in providers)
            {
                handles.AddRange(await provider.LoadRawAssetsByLabelAsync(label));
            }

            return handles;
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
            var provider = GetProvider(name);
            if (provider == null)
            {
                throw new GameException($"Scene not found: {name}");
            }

            return provider.LoadSceneAssetAsync(name);
        }

        /// <summary>
        /// 卸载未使用的资源。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async UniTask UnloadUnusedAssetAsync()
        {
            EnsureReady();
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
            return UnloadHandle(handle, (p, h) => p.UnloadAsset(h), "Asset");
        }

        /// <summary>
        /// 卸载二进制资源。
        /// </summary>
        /// <param name="handle">二进制资源句柄。</param>
        /// <returns>异步任务。</returns>
        public UniTask UnloadRawAsset(RawAssetHandle handle)
        {
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
            if (string.IsNullOrWhiteSpace(package) || _manifest == null)
            {
                return false;
            }

            var bundles = _manifest.GetPackageBundles(package);
            if (bundles == null || bundles.Count == 0)
            {
                return false;
            }

            var bundleNames = new HashSet<string>(bundles.Select(x => x.Name));
            return _providers.Any(x => x.Info != null && bundleNames.Contains(x.Info.Name));
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
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            return _providers.FirstOrDefault(provider => provider.HasAsset(location));
        }

        private List<ProviderBase> GetProviders(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return new List<ProviderBase>();
            }

            return _providers.Where(provider => provider.HasAsset(location)).ToList();
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

            return settings;
        }

        private async UniTask LoadAndApplyManifestAsync(ResourceSettings setting)
        {
            var operation = await App.Operation.WaitCompletionWithKeyAsync<LoadManifestOperationHandle>(setting, setting);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                throw operation.Error ?? new GameException($"Resource manifest initialize failed. Mode: {setting.Mode}");
            }

            ReleaseProviders();
            _setting = setting;
            _mode = setting.Mode;
            _manifest = operation.Value;
        }

        private static ManifestInfo LoadStartupManifest()
        {
            return LoadManifestOperationHandle.ReadStartupManifest();
        }

        private void ApplyStartupManifest(ManifestInfo manifest)
        {
            _setting = new ResourceSettings
            {
                Mode = ResourceMode.Offline,
                ManifestName = ResourceSettings.MANIFEST_NAME,
                DefaultPackages = Array.Empty<string>()
            };
            _manifest = manifest;
            _mode = ResourceMode.Offline;
            _initializeState = ResourceInitializeState.LocalInitialized;
            if (HasBuiltinPackage(_manifest))
            {
                var operation = InitializePackageAsync(ResourceConstants.BUILTIN_PACKAGE_NAME).GetAwaiter().GetResult();
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    throw new GameException($"{ResourceConstants.BUILTIN_PACKAGE_NAME} initialize failed.", operation.Error);
                }
            }
        }

        private static bool HasBuiltinPackage(ManifestInfo manifest)
        {
            return manifest?.Packages?.Any(package => package != null && package.Name == ResourceConstants.BUILTIN_PACKAGE_NAME) == true;
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
            if ((_initializeState != ResourceInitializeState.Initializing && IsLocalInitialized is false) || _setting == null || _manifest == null)
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
            foreach (var provider in _providers.ToArray())
            {
                provider.Release();
            }

            _providers.Clear();
            _network.Release();
        }

        /// <summary>
        /// 执行显式反初始化流程。
        /// </summary>
        private async UniTask UninitializeInternalAsync()
        {
            if (_initializeState == ResourceInitializeState.Initializing && _initializeCompletion != null)
            {
                try
                {
                    await _initializeCompletion.Task;
                }
                catch
                {
                }
            }

            ReleaseProviders();
            _manifest = null;
            _setting = null;
            _initializeState = ResourceInitializeState.NotInitialized;
            _initializeCompletion = null;
        }

    }
}

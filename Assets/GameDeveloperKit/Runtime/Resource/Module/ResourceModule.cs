using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源模块，提供资源加载、管理和更新功能。
    /// </summary>
    public sealed partial class ResourceModule : IGameFrameworkLifecycleModule
    {
        private readonly Dictionary<string, IResourcePackage> _packages = new(StringComparer.Ordinal);
        private readonly ResourceModuleDriver _driver;
        private readonly ResourceProviderFacade _provider;
        private readonly ResourceCatalogFacade _catalog;
        private readonly ResourceUpdateServiceFacade _updateService;
        private ResourcePlayMode _playMode;
        private bool _isInitialized;
        private bool _diagnosticsRegistered;
        private string _lastReleasedAssetPackage;
        private string _lastReleasedAssetLocation;

        /// <summary>
        /// 初始化资源模块的新实例。
        /// </summary>
        public ResourceModule()
        {
            _playMode = ResourcePlayMode.Offline;
            var driverObject = new GameObject("[GameDeveloperKit.ResourceModule]");
            UnityEngine.Object.DontDestroyOnLoad(driverObject);
            _driver = driverObject.AddComponent<ResourceModuleDriver>();
            _driver.Initialize(this);
            _provider = new ResourceProviderFacade(this);
            _catalog = new ResourceCatalogFacade(this);
            _updateService = new ResourceUpdateServiceFacade(this);
        }

        /// <summary>
        /// 获取资源播放模式。
        /// </summary>
        public ResourcePlayMode PlayMode => _playMode;

        /// <summary>
        /// 获取模块状态。
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 获取资源提供者门面。
        /// </summary>
        public IResourceProvider Provider => _provider;

        /// <summary>
        /// 获取资源目录门面。
        /// </summary>
        public IResourceCatalog Catalog => _catalog;

        /// <summary>
        /// 获取资源更新服务门面。
        /// </summary>
        public IResourceUpdateService UpdateService => _updateService;

        public string GatewayServerUrl { get; private set; }

        /// <summary>
        /// 异步初始化资源模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
            {
                return UniTask.CompletedTask;
            }

            try
            {
                RegisterDiagnosticsSnapshotProviders();
                _isInitialized = true;
                return UniTask.CompletedTask;
            }
            catch
            {
                _isInitialized = false;
                throw;
            }
        }

        /// <summary>
        /// 异步关闭资源模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                return UniTask.CompletedTask;
            }

            Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 使用极简启动参数初始化资源模块。
        /// </summary>
        /// <param name="playMode">资源运行模式。</param>
        /// <param name="defaultPackageName">默认资源包名。</param>
        /// <param name="gatewayServerUrl">网关服务器地址。</param>
        /// <exception cref="GameFrameworkException">当设置无效时抛出。</exception>
        public void Initialize(ResourcePlayMode playMode, string defaultPackageName, string gatewayServerUrl = null)
        {
            if (_isInitialized)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(defaultPackageName))
            {
                _isInitialized = false;
                throw GameFrameworkException.Create("ResourcePackageNameMissing", "Default resource package name can not be empty.", "Configuration");
            }

            try
            {
                _packages.Clear();
                _playMode = playMode;
                GatewayServerUrl = gatewayServerUrl;

                var definition = CreateDefaultPackageDefinition(playMode, defaultPackageName, gatewayServerUrl);
                var options = new ResourcePackageOptions
                {
                    RootPath = definition.PersistentRoot,
                    Entries = definition.Entries
                };

                var context = new ResourcePackageContext(playMode, definition);
                var runtime = CreateRuntime(playMode);
                var package = new ResourcePackage(definition.PackageName, options, runtime, context);
                _packages[definition.PackageName] = package;

                _isInitialized = true;
            }
            catch
            {
                _isInitialized = false;
                throw;
            }
        }

        /// <summary>
        /// 初始化指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="options">资源包选项。</param>
        /// <exception cref="ArgumentException">当包名为空时抛出。</exception>
        public void InitializePackage(string packageName, ResourcePackageOptions options)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("Package name can not be empty.", nameof(packageName));
            }

            var definition = new ResourcePackageDefinition
            {
                PackageName = packageName,
                Role = ResourcePackageRole.Builtin,
                PersistentRoot = options?.RootPath,
                Entries = options?.Entries == null ? new List<ResourceEntry>() : new List<ResourceEntry>(options.Entries)
            };

            var context = new ResourcePackageContext(_playMode, definition);
            var package = new ResourcePackage(packageName, options, CreateRuntime(_playMode), context);
            _packages[packageName] = package;
        }

        /// <summary>
        /// 检查是否存在指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>如果存在返回true，否则返回false。</returns>
        public bool HasPackage(string packageName)
        {
            return !string.IsNullOrWhiteSpace(packageName) && _packages.ContainsKey(packageName);
        }

        /// <summary>
        /// 尝试获取指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="package">输出的资源包。</param>
        /// <returns>如果找到返回true，否则返回false。</returns>
        public bool TryGetPackage(string packageName, out IResourcePackage package)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                package = null;
                return false;
            }

            return _packages.TryGetValue(packageName, out package);
        }

        /// <summary>
        /// 获取指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源包。</returns>
        /// <exception cref="InvalidOperationException">当包未初始化时抛出。</exception>
        public IResourcePackage GetPackage(string packageName)
        {
            if (!TryGetPackage(packageName, out var package))
            {
                throw new InvalidOperationException($"Package '{packageName}' is not initialized.");
            }

            return package;
        }

        /// <summary>
        /// 异步准备指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称，为null时使用默认包。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask PreparePackageAsync(string packageName = null, CancellationToken cancellationToken = default)
        {
            return ResolveTargetPackage(packageName).PrepareAsync(cancellationToken);
        }

        /// <summary>
        /// 异步初始化指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称，为null时使用默认包。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask InitializePackageAsync(string packageName = null, CancellationToken cancellationToken = default)
        {
            return ResolveTargetPackage(packageName).InitializeAsync(cancellationToken);
        }

        /// <summary>
        /// 异步初始化所有资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public async UniTask InitializeAllPackagesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var package in _packages.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await package.InitializeAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 异步准备所有资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public async UniTask PrepareAllPackagesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var package in _packages.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await package.PrepareAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 异步更新指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称，为null时使用默认包。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源更新结果。</returns>
        /// <exception cref="InvalidOperationException">当包不支持更新时抛出。</exception>
        public async UniTask<ResourceUpdateResult> UpdatePackageAsync(string packageName = null, CancellationToken cancellationToken = default)
        {
            if (ResolveTargetPackage(packageName) is not ResourcePackage package)
            {
                throw new InvalidOperationException($"Package '{packageName}' does not support runtime update lifecycle.");
            }

            return await package.UpdateAsync(cancellationToken);
        }

        /// <summary>
        /// 异步更新所有资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源更新结果列表。</returns>
        public async UniTask<IReadOnlyList<ResourceUpdateResult>> UpdateAllPackagesAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<ResourceUpdateResult>(_packages.Count);
            foreach (var package in _packages.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (package is not ResourcePackage typedPackage)
                {
                    continue;
                }

                results.Add(await typedPackage.UpdateAsync(cancellationToken));
            }

            return results;
        }

        /// <summary>
        /// 获取指定资源包的状态。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源包状态。</returns>
        public ResourcePackageState GetPackageState(string packageName)
        {
            return GetPackage(packageName).State;
        }

        /// <summary>
        /// 获取指定资源包的最后错误信息。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>错误信息字符串。</returns>
        public string GetPackageLastError(string packageName)
        {
            return GetPackage(packageName).LastError;
        }

        /// <summary>
        /// 获取指定资源包的最后更新报告。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源更新报告。</returns>
        public ResourceUpdateReport GetPackageUpdateReport(string packageName)
        {
            return GetPackage(packageName).LastUpdateReport;
        }

        /// <summary>
        /// 移除指定的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>如果移除成功返回true，否则返回false。</returns>
        public bool RemovePackage(string packageName)
        {
            if (!_packages.Remove(packageName, out var package))
            {
                return false;
            }

            package.CollectUnused(true);
            return true;
        }

        /// <summary>
        /// 同步加载指定名称的资源。
        /// </summary>
        /// <param name="name">资源名称。</param>
        /// <returns>资源句柄。</returns>
        public AssetHandle LoadAsset(string name)
        {
            if (IsResourcesPath(name))
            {
                return LoadFromResourcesPath(name);
            }

            var byName = FindByName(name);
            if (byName.Count == 1)
            {
                return LoadAsset(CreateLoadLocation(byName[0]));
            }

            var byPath = FindByPath(name);
            if (byPath.Count == 1)
            {
                return LoadAsset(CreateLoadLocation(byPath[0]));
            }

            var byLabel = FindByLabel(name);
            if (byLabel.Count == 1)
            {
                return LoadAsset(CreateLoadLocation(byLabel[0]));
            }

            var totalMatches = byName.Count + byPath.Count + byLabel.Count;
            if (totalMatches == 0)
            {
                throw new InvalidOperationException($"Failed to find resource by key '{name}'.");
            }

            throw new InvalidOperationException($"LoadAsset key '{name}' is ambiguous. Use LoadByName/LoadByType/LoadByLabel/LoadByPath.");
        }

        /// <summary>
        /// 同步加载指定名称和类型的资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <param name="name">资源名称。</param>
        /// <returns>资源句柄。</returns>
        public AssetHandle LoadAsset<TAsset>(string name)
            where TAsset : UnityEngine.Object
        {
            return LoadAsset(new ResourceLocation { Name = name, AssetType = typeof(TAsset) });
        }

        /// <summary>
        /// 异步加载指定名称的资源。
        /// </summary>
        /// <param name="name">资源名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄的异步任务。</returns>
        public UniTask<AssetHandle> LoadAssetAsync(string name, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(LoadAsset(name));
        }

        /// <summary>
        /// 异步加载指定名称和类型的资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <param name="name">资源名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄的异步任务。</returns>
        public UniTask<AssetHandle> LoadAssetAsync<TAsset>(string name, CancellationToken cancellationToken = default)
            where TAsset : UnityEngine.Object
        {
            return LoadAssetAsync(new ResourceLocation { Name = name, AssetType = typeof(TAsset) }, cancellationToken);
        }

        /// <summary>
        /// 同步加载指定标签的所有资源。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>资源句柄列表。</returns>
        public IReadOnlyList<AssetHandle> LoadAssetsByLabel(string label)
        {
            return LoadAssets(new ResourceLocation { Labels = new[] { label } });
        }

        /// <summary>
        /// 同步加载指定标签和类型的所有资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <param name="label">资源标签。</param>
        /// <returns>资源句柄列表。</returns>
        public IReadOnlyList<AssetHandle> LoadAssetsByLabel<TAsset>(string label)
            where TAsset : UnityEngine.Object
        {
            return LoadAssets(new ResourceLocation { Labels = new[] { label }, AssetType = typeof(TAsset) });
        }

        /// <summary>
        /// 异步加载指定标签的所有资源。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄列表的异步任务。</returns>
        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label, CancellationToken cancellationToken = default)
        {
            return LoadAssetsAsync(new ResourceLocation { Labels = new[] { label } }, cancellationToken);
        }

        /// <summary>
        /// 异步加载指定标签和类型的所有资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <param name="label">资源标签。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄列表的异步任务。</returns>
        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync<TAsset>(string label, CancellationToken cancellationToken = default)
            where TAsset : UnityEngine.Object
        {
            return LoadAssetsAsync(new ResourceLocation { Labels = new[] { label }, AssetType = typeof(TAsset) }, cancellationToken);
        }

        /// <summary>
        /// 同步加载指定类型的所有资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <returns>资源句柄列表。</returns>
        public IReadOnlyList<AssetHandle> LoadAssetsByType<TAsset>()
            where TAsset : UnityEngine.Object
        {
            return LoadAssets(new ResourceLocation { AssetType = typeof(TAsset) });
        }

        /// <summary>
        /// 异步加载指定类型的所有资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄列表的异步任务。</returns>
        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<TAsset>(CancellationToken cancellationToken = default)
            where TAsset : UnityEngine.Object
        {
            return LoadAssetsAsync(new ResourceLocation { AssetType = typeof(TAsset) }, cancellationToken);
        }

        /// <summary>
        /// 同步加载指定位置的资源。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>资源句柄。</returns>
        public AssetHandle LoadAsset(ResourceLocation location)
        {
            if (IsResourcesLocation(location))
            {
                return LoadFromResourcesLocation(location);
            }

            return ResolvePackage(location, null).LoadAsset(EnsurePackageBound(location));
        }

        /// <summary>
        /// 异步加载指定位置的资源。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄的异步任务。</returns>
        public UniTask<AssetHandle> LoadAssetAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            if (IsResourcesLocation(location))
            {
                return UniTask.FromResult(LoadFromResourcesLocation(location));
            }

            return ResolvePackage(location, null).LoadAssetAsync(EnsurePackageBound(location), cancellationToken);
        }

        /// <summary>
        /// 同步加载指定位置的资源集合。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>资源句柄列表。</returns>
        /// <exception cref="ArgumentNullException">当位置为null时抛出。</exception>
        public IReadOnlyList<AssetHandle> LoadAssets(ResourceLocation location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (!string.IsNullOrWhiteSpace(location.PackageName))
            {
                return GetPackage(location.PackageName).LoadAssets(location);
            }

            var handles = new List<AssetHandle>();
            foreach (var package in _packages.Values)
            {
                var packageHandles = package.LoadAssets(location);
                if (packageHandles == null || packageHandles.Count == 0)
                {
                    continue;
                }

                for (var i = 0; i < packageHandles.Count; i++)
                {
                    handles.Add(packageHandles[i]);
                }
            }

            return handles;
        }

        /// <summary>
        /// 异步加载指定位置的资源集合。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄列表的异步任务。</returns>
        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return LoadAssetsAsyncInternal(location, cancellationToken);
        }

        /// <summary>
        /// 同步加载指定名称的场景。
        /// </summary>
        /// <param name="name">场景名称。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <returns>场景句柄。</returns>
        public SceneHandle LoadScene(string name, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            return LoadScene(new ResourceLocation { Name = name }, loadMode);
        }

        /// <summary>
        /// 异步加载指定名称的场景。
        /// </summary>
        /// <param name="name">场景名称。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>场景句柄的异步任务。</returns>
        public UniTask<SceneHandle> LoadSceneAsync(string name, LoadSceneMode loadMode = LoadSceneMode.Single, CancellationToken cancellationToken = default)
        {
            return LoadSceneAsync(new ResourceLocation { Name = name }, loadMode, cancellationToken);
        }

        /// <summary>
        /// 同步加载指定位置的场景。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <returns>场景句柄。</returns>
        public SceneHandle LoadScene(ResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            return ResolvePackage(location, null).LoadScene(EnsurePackageBound(location), loadMode);
        }

        /// <summary>
        /// 异步加载指定位置的场景。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>场景句柄的异步任务。</returns>
        public UniTask<SceneHandle> LoadSceneAsync(ResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, CancellationToken cancellationToken = default)
        {
            return ResolvePackage(location, null).LoadSceneAsync(EnsurePackageBound(location), loadMode, cancellationToken);
        }

        /// <summary>
        /// 同步加载指定路径的原始文件。
        /// </summary>
        /// <param name="fullPath">文件完整路径。</param>
        /// <returns>原始文件句柄。</returns>
        public RawFileHandle LoadRawFile(string fullPath)
        {
            return LoadRawFile(new ResourceLocation { FullPath = fullPath });
        }

        /// <summary>
        /// 异步加载指定路径的原始文件。
        /// </summary>
        /// <param name="fullPath">文件完整路径。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>原始文件句柄的异步任务。</returns>
        public UniTask<RawFileHandle> LoadRawFileAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            return LoadRawFileAsync(new ResourceLocation { FullPath = fullPath }, cancellationToken);
        }

        /// <summary>
        /// 同步加载指定位置的原始文件。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>原始文件句柄。</returns>
        public RawFileHandle LoadRawFile(ResourceLocation location)
        {
            return ResolvePackage(location, null).LoadRawFile(EnsurePackageBound(location));
        }

        /// <summary>
        /// 异步加载指定位置的原始文件。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>原始文件句柄的异步任务。</returns>
        public UniTask<RawFileHandle> LoadRawFileAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return ResolvePackage(location, null).LoadRawFileAsync(EnsurePackageBound(location), cancellationToken);
        }

        /// <summary>
        /// 收集未使用的资源。
        /// </summary>
        /// <param name="force">是否强制收集。</param>
        public void CollectUnused(bool force = false)
        {
            foreach (var package in _packages.Values)
            {
                package.CollectUnused(force);
            }
        }

        /// <summary>
        /// 查找指定位置的资源条目。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源条目类型。</param>
        /// <returns>资源条目列表。</returns>
        /// <exception cref="ArgumentNullException">当位置为null时抛出。</exception>
        public IReadOnlyList<ResourceEntry> Find(ResourceLocation location, ResourceEntryKind? kind = null)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (!string.IsNullOrWhiteSpace(location.PackageName))
            {
                return GetPackage(location.PackageName).Find(location, kind);
            }

            var results = new List<ResourceEntry>();
            foreach (var package in _packages.Values)
            {
                var packageResults = package.Find(location, kind);
                if (packageResults == null || packageResults.Count == 0)
                {
                    continue;
                }

                for (var i = 0; i < packageResults.Count; i++)
                {
                    results.Add(packageResults[i]);
                }
            }

            return results;
        }

        /// <summary>
        /// 注册资源条目。
        /// </summary>
        /// <param name="entry">资源条目。</param>
        /// <exception cref="InvalidOperationException">此方法需要指定目标包。</exception>
        public void RegisterEntry(ResourceEntry entry)
        {
            throw new InvalidOperationException("RegisterEntry requires an explicit target package. Use GetPackage(packageName).RegisterEntry(...) instead.");
        }

        /// <summary>
        /// 批量注册资源条目。
        /// </summary>
        /// <param name="entries">资源条目集合。</param>
        /// <exception cref="InvalidOperationException">此方法需要指定目标包。</exception>
        public void RegisterEntries(IEnumerable<ResourceEntry> entries)
        {
            throw new InvalidOperationException("RegisterEntries requires an explicit target package. Use GetPackage(packageName).RegisterEntries(...) instead.");
        }

        /// <summary>
        /// 移除指定位置的资源条目。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源条目类型。</param>
        /// <returns>移除的资源条目数量。</returns>
        /// <exception cref="ArgumentNullException">当位置为null时抛出。</exception>
        /// <exception cref="InvalidOperationException">当未指定包名时抛出。</exception>
        public int RemoveEntries(ResourceLocation location, ResourceEntryKind? kind = null)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (string.IsNullOrWhiteSpace(location.PackageName))
            {
                throw new InvalidOperationException("RemoveEntries requires ResourceLocation.PackageName to avoid ambiguous cross-package mutations.");
            }

            return GetPackage(location.PackageName).RemoveEntries(location, kind);
        }

        /// <summary>
        /// 清除所有资源条目。
        /// </summary>
        /// <param name="kind">资源条目类型。</param>
        /// <exception cref="InvalidOperationException">此方法需要指定目标包。</exception>
        public void ClearEntries(ResourceEntryKind? kind = null)
        {
            throw new InvalidOperationException("ClearEntries requires an explicit target package. Use GetPackage(packageName).ClearEntries(...) instead.");
        }

        /// <summary>
        /// 释放资源模块占用的所有资源。
        /// </summary>
        public void Dispose()
        {
            RemoveDiagnosticsSnapshotProviders();
            CollectUnused(true);
            _packages.Clear();
            _isInitialized = false;
            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver.gameObject);
            }
        }

        /// <summary>
        /// 根据播放模式创建资源运行时实例。
        /// </summary>
        /// <param name="playMode">资源播放模式。</param>
        /// <returns>资源运行时实例。</returns>
        private static IResourceRuntime CreateRuntime(ResourcePlayMode playMode)
        {
            switch (playMode)
            {
                case ResourcePlayMode.EditorSimulate:
#if UNITY_EDITOR
                    return new EditorSimulateResourceRuntime();
#else
                    return new OfflineResourceRuntime();
#endif
                case ResourcePlayMode.Offline:
                    return new OfflineResourceRuntime();
                case ResourcePlayMode.Host:
                    return new HostResourceRuntime();
                case ResourcePlayMode.Web:
                    return new WebResourceRuntime();
                default:
                    throw new ArgumentOutOfRangeException(nameof(playMode), playMode, null);
            }
        }

        private static ResourcePackageDefinition CreateDefaultPackageDefinition(ResourcePlayMode playMode, string packageName, string gatewayServerUrl)
        {
            var role = playMode == ResourcePlayMode.Host || playMode == ResourcePlayMode.Web
                ? ResourcePackageRole.HotUpdate
                : ResourcePackageRole.Builtin;
            var simulateRoots = playMode == ResourcePlayMode.EditorSimulate
                ? new List<string> { "Assets" }
                : new List<string>();
            return new ResourcePackageDefinition
            {
                PackageName = packageName,
                Role = role,
                ManifestRelativePath = "manifest.json",
                StreamingAssetsRoot = $"GameDeveloperKit/Packages/{packageName}",
                PersistentRoot = $"GameDeveloperKit/Packages/{packageName}",
                RemoteBaseUrl = gatewayServerUrl,
                SimulateSearchRoots = simulateRoots,
                Entries = new List<ResourceEntry>()
            };
        }

        /// <summary>
        /// 注册诊断快照提供程序。
        /// </summary>
        private void RegisterDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Resource.PackageCount", () => _packages.Count.ToString());
            diagnostics.RegisterSnapshotProvider("Resource.PlayMode", () => _playMode.ToString());
            diagnostics.RegisterSnapshotProvider("Resource.PreparedPackageCount", GetPreparedPackageCountSnapshot);
            diagnostics.RegisterSnapshotProvider("Resource.FailedPackageCount", GetFailedPackageCountSnapshot);
            diagnostics.RegisterSnapshotProvider("Resource.UpdatedPackageCount", GetUpdatedPackageCountSnapshot);
            diagnostics.RegisterSnapshotProvider("Resource.LastReleasedAssetPackage", () => _lastReleasedAssetPackage ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Resource.LastReleasedAssetLocation", () => _lastReleasedAssetLocation ?? string.Empty);
            _diagnosticsRegistered = true;
        }

        /// <summary>
        /// 移除诊断快照提供程序。
        /// </summary>
        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Resource.PackageCount");
            diagnostics.RemoveSnapshotProvider("Resource.PlayMode");
            diagnostics.RemoveSnapshotProvider("Resource.PreparedPackageCount");
            diagnostics.RemoveSnapshotProvider("Resource.FailedPackageCount");
            diagnostics.RemoveSnapshotProvider("Resource.UpdatedPackageCount");
            diagnostics.RemoveSnapshotProvider("Resource.LastReleasedAssetPackage");
            diagnostics.RemoveSnapshotProvider("Resource.LastReleasedAssetLocation");
            _diagnosticsRegistered = false;
        }

        /// <summary>
        /// 获取已准备好的资源包数量快照。
        /// </summary>
        /// <returns>已准备好的资源包数量字符串。</returns>
        private string GetPreparedPackageCountSnapshot()
        {
            var preparedCount = 0;
            foreach (var package in _packages.Values)
            {
                if (package != null && package.IsReady)
                {
                    preparedCount++;
                }
            }

            return preparedCount.ToString();
        }

        /// <summary>
        /// 获取失败资源包数量快照。
        /// </summary>
        /// <returns>失败资源包数量字符串。</returns>
        private string GetFailedPackageCountSnapshot()
        {
            var failedCount = 0;
            foreach (var package in _packages.Values)
            {
                if (package != null && package.State == ResourcePackageState.Failed)
                {
                    failedCount++;
                }
            }

            return failedCount.ToString();
        }

        /// <summary>
        /// 获取已更新资源包数量快照。
        /// </summary>
        /// <returns>已更新资源包数量字符串。</returns>
        private string GetUpdatedPackageCountSnapshot()
        {
            var updatedCount = 0;
            foreach (var package in _packages.Values)
            {
                if (package != null && package.State == ResourcePackageState.Updated)
                {
                    updatedCount++;
                }
            }

            return updatedCount.ToString();
        }

        /// <summary>
        /// 解析目标资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源包。</returns>
        /// <exception cref="InvalidOperationException">当未初始化任何资源包时抛出。</exception>
        private IResourcePackage ResolveTargetPackage(string packageName)
        {
            if (!string.IsNullOrWhiteSpace(packageName))
            {
                return GetPackage(packageName);
            }

            foreach (var package in _packages.Values)
            {
                return package;
            }

            throw new InvalidOperationException("No resource package is initialized.");
        }

        /// <summary>
        /// 根据资源位置解析资源包。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源条目类型。</param>
        /// <returns>资源包。</returns>
        /// <exception cref="ArgumentNullException">当位置为null时抛出。</exception>
        /// <exception cref="InvalidOperationException">当无法解析资源包时抛出。</exception>
        public IResourcePackage ResolvePackage(ResourceLocation location, ResourceEntryKind? kind = null)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (!string.IsNullOrWhiteSpace(location.PackageName))
            {
                return GetPackage(location.PackageName);
            }

            foreach (var pair in _packages)
            {
                var package = pair.Value;
                if (package.Find(location, kind).Count > 0)
                {
                    return package;
                }
            }

            throw new InvalidOperationException($"Failed to resolve resource package for location '{GetLocationLabel(location)}'.");
        }

        /// <summary>
        /// 获取资源位置的标签。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>位置标签字符串。</returns>
        private static string GetLocationLabel(ResourceLocation location)
        {
            if (location == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(location.Name))
            {
                return location.Name;
            }

            if (!string.IsNullOrWhiteSpace(location.FullPath))
            {
                return location.FullPath;
            }

            return location.AssetType?.Name ?? "<unknown>";
        }

        /// <summary>
        /// 确保资源位置绑定了包名。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>绑定了包名的资源位置。</returns>
        private ResourceLocation EnsurePackageBound(ResourceLocation location)
        {
            var resolvedPackage = ResolvePackage(location);
            var copy = location.Clone();
            copy.PackageName = resolvedPackage.PackageName;
            return copy;
        }

        public AssetHandle LoadByName(string name, string packageName = null)
        {
            if (IsResourcesPath(name))
            {
                return LoadFromResourcesPath(name, packageName);
            }

            var matches = FindByName(name, packageName);
            return LoadStrictSingle(matches, "LoadByName", name);
        }

        public AssetHandle LoadByLabel(string label, string packageName = null)
        {
            var matches = FindByLabel(label, packageName);
            return LoadStrictSingle(matches, "LoadByLabel", label);
        }

        public AssetHandle LoadByPath(string fullPath, string packageName = null)
        {
            if (IsResourcesPath(fullPath))
            {
                return LoadFromResourcesPath(fullPath, packageName);
            }

            var matches = FindByPath(fullPath, packageName);
            return LoadStrictSingle(matches, "LoadByPath", fullPath);
        }

        public IReadOnlyList<AssetHandle> LoadByType<TAsset>(string packageName = null)
            where TAsset : UnityEngine.Object
        {
            var location = new ResourceLocation
            {
                AssetType = typeof(TAsset),
                PackageName = packageName
            };

            return LoadAssets(location);
        }

        private AssetHandle LoadStrictSingle(IReadOnlyList<ResourceEntry> matches, string operation, string key)
        {
            if (matches == null || matches.Count == 0)
            {
                throw new InvalidOperationException($"{operation} failed to find resource '{key}'.");
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"{operation} matched multiple resources for '{key}'.");
            }

            return LoadAsset(CreateLoadLocation(matches[0]));
        }

        private IReadOnlyList<ResourceEntry> FindByName(string name, string packageName = null)
        {
            return Find(new ResourceLocation
            {
                Name = name,
                PackageName = packageName
            }, null);
        }

        private IReadOnlyList<ResourceEntry> FindByLabel(string label, string packageName = null)
        {
            return Find(new ResourceLocation
            {
                Labels = new[] { label },
                PackageName = packageName
            }, null);
        }

        private IReadOnlyList<ResourceEntry> FindByPath(string fullPath, string packageName = null)
        {
            return Find(new ResourceLocation
            {
                FullPath = fullPath,
                PackageName = packageName
            }, null);
        }

        private static ResourceLocation CreateLoadLocation(ResourceEntry entry)
        {
            return new ResourceLocation
            {
                Name = entry?.Name,
                FullPath = entry?.FullPath
            };
        }

        private AssetHandle LoadFromResourcesPath(string resourcesPath, string packageName = null)
        {
            var package = ResolveTargetPackage(packageName);
            var location = new ResourceLocation
            {
                PackageName = package.PackageName,
                Name = resourcesPath,
                FullPath = resourcesPath
            };

            return package.LoadAsset(location);
        }

        private AssetHandle LoadFromResourcesLocation(ResourceLocation location)
        {
            var package = ResolveTargetPackage(location?.PackageName);
            var copy = location?.Clone() ?? new ResourceLocation();
            copy.PackageName = package.PackageName;
            return package.LoadAsset(copy);
        }

        private static bool IsResourcesPath(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Replace('\\', '/').StartsWith("Resources/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsResourcesLocation(ResourceLocation location)
        {
            return location != null && (IsResourcesPath(location.Name) || IsResourcesPath(location.FullPath));
        }

        /// <summary>
        /// 异步加载资源集合的内部实现。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄列表的异步任务。</returns>
        /// <exception cref="ArgumentNullException">当位置为null时抛出。</exception>
        private async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsAsyncInternal(ResourceLocation location, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (!string.IsNullOrWhiteSpace(location.PackageName))
            {
                return await GetPackage(location.PackageName).LoadAssetsAsync(location, cancellationToken);
            }

            var handles = new List<AssetHandle>();
            foreach (var package in _packages.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var packageHandles = await package.LoadAssetsAsync(location, cancellationToken);
                if (packageHandles == null || packageHandles.Count == 0)
                {
                    continue;
                }

                for (var i = 0; i < packageHandles.Count; i++)
                {
                    handles.Add(packageHandles[i]);
                }
            }

            return handles;
        }

        /// <summary>
        /// 通知资源句柄已释放。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="location">资源位置。</param>
        internal void NotifyHandleReleased(string packageName, ResourceLocation location)
        {
            _lastReleasedAssetPackage = packageName ?? string.Empty;
            _lastReleasedAssetLocation = GetLocationLabel(location);

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Resource.LastReleasedAssetPackage", _lastReleasedAssetPackage);
                diagnostics.CaptureSnapshot("Resource.LastReleasedAssetLocation", _lastReleasedAssetLocation);
            }
        }
    }
}





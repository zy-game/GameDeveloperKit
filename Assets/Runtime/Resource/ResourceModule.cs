using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源模块
    /// </summary>
    public class ResourceModule : IModule, IResourceManager
    {
        private readonly Dictionary<string, ResourcePackage> _packages = new Dictionary<string, ResourcePackage>();
        private readonly BundleLoaderService _bundleService;
        private readonly VersionManager _versionManager;

        private float _autoUnloadInterval = 5f;
        private float _lastUnloadTime = 0f;
        private EResourceMode _resourceMode = EResourceMode.None;

        public ResourceModule()
        {
            _versionManager = new VersionManager();
            _bundleService = new BundleLoaderService();
        }

        public void OnStartup()
        {
            // 注册资源调试面板
            Log.DebugConsole.Instance?.RegisterPanel(new ResourceDebugPanel());
        }

        public void OnUpdate(float elapseSeconds)
        {
            if (_autoUnloadInterval > 0)
            {
                _lastUnloadTime += elapseSeconds;

                if (_lastUnloadTime >= _autoUnloadInterval * 60f)
                {
                    UnloadUnusedAssets();
                    _lastUnloadTime = 0f;
                }
            }
        }

        public void OnClearup()
        {
            foreach (var package in _packages.Values)
            {
                package.OnClearup();
            }

            _packages.Clear();
            _bundleService.Clear();
            _lastUnloadTime = 0f;

            Game.Debug.Debug("ResourceModule cleared");
        }

        /// <summary>
        /// 设置资源模式
        /// </summary>
        public void SetMode(EResourceMode mode)
        {
            _resourceMode = mode;
            CreateBuiltinPackage();
            CreateRemotePackage();
            Game.Debug.Info($"Resource mode set to: {mode}");
        }

        /// <summary>
        /// 获取资源模式
        /// </summary>
        public EResourceMode GetMode()
        {
            return _resourceMode;
        }

        /// <summary>
        /// 为 Package 初始化 Provider 系统（根据模式）
        /// </summary>
        private void InitializePackageProviders(ResourcePackage package)
        {
            var providerSystem = package.GetProviderSystem();
            providerSystem.Clear();

            if (_resourceMode == EResourceMode.EditorSimulator)
            {
#if UNITY_EDITOR
                // 编辑器模式：使用 EditorAssetProvider 和 EditorSceneProvider
                var editorAssetProvider = new EditorAssetProvider();
                var editorSceneProvider = new EditorSceneProvider();
                providerSystem.SetDefaultProvider(ResourceLocationType.Bundle, editorAssetProvider);
                providerSystem.SetSceneProvider(ResourceLocationType.Bundle, editorSceneProvider);
#else
                Game.Debug.Error("EditorSimulator mode only available in Unity Editor");
#endif
            }
            else
            {
                // Offline 和 Online 模式：使用 BundleAssetProvider 和 BundleSceneProvider
                var bundleAssetProvider = new BundleAssetProvider(_bundleService);
                var bundleSceneProvider = new BundleSceneProvider(_bundleService);
                providerSystem.SetDefaultProvider(ResourceLocationType.Bundle, bundleAssetProvider);
                providerSystem.SetSceneProvider(ResourceLocationType.Bundle, bundleSceneProvider);
            }

            // Builtin 和 Remote Provider 不受模式影响
            var builtinAssetProvider = new BuiltinAssetProvider();
            providerSystem.SetDefaultProvider(ResourceLocationType.Builtin, builtinAssetProvider);

            var remoteAssetProvider = new RemoteAssetProvider();
            providerSystem.SetDefaultProvider(ResourceLocationType.Remote, remoteAssetProvider);

            Game.Debug.Debug($"[{package.Name}] Provider system initialized");
        }

        /// <summary>
        /// 创建内置资源包
        /// </summary>
        private void CreateBuiltinPackage()
        {
            // 避免重复创建
            if (_packages.ContainsKey("BuiltinPackage"))
            {
                return;
            }

            var loader = new BuiltinPackageLoader("BuiltinPackage");
            var package = new ResourcePackage("BuiltinPackage", "builtin", loader);
            InitializePackageProviders(package);

            _packages.Add("BuiltinPackage", package);

            // 内置资源包立即初始化
            package.Initialization(this).Forget();
        }

        /// <summary>
        /// 创建远程资源包
        /// </summary>
        private void CreateRemotePackage()
        {
            // 避免重复创建
            if (_packages.ContainsKey("RemotePackage"))
            {
                return;
            }

            var loader = new RemotePackageLoader("RemotePackage");
            var package = new ResourcePackage("RemotePackage", "remote", loader);
            InitializePackageProviders(package);

            _packages.Add("RemotePackage", package);

            // 远程资源包立即初始化
            package.Initialization(this).Forget();
        }

        /// <summary>
        /// 获取版本管理器
        /// </summary>
        public VersionManager GetVersionManager()
        {
            return _versionManager;
        }

        /// <summary>
        /// 设置资源服务器地址
        /// </summary>
        /// <param name="url">资源服务器基础 URL</param>
        public UniTask<bool> SetResourceServerUrl(string url)
        {
            _versionManager.SetBaseUrl(url);
            Game.Debug.Info($"Resource server URL set to: {url}");

            // 根据资源模式决定版本获取策略
            switch (_resourceMode)
            {
                case EResourceMode.EditorSimulator:
                    // 编辑器模拟模式：跳过网络请求，直接返回成功
                    Game.Debug.Info("[VersionManager] EditorSimulator mode: skipping manifest loading");
                    return UniTask.FromResult(true);

                case EResourceMode.Offline:
                    // 离线模式：从 StreamingAssets 加载清单
                    Game.Debug.Info("[VersionManager] Offline mode: loading manifest from StreamingAssets");
                    return _versionManager.LoadGlobalManifestFromStreamingAssetsAsync();

                case EResourceMode.Online:
                    // 在线模式：联网获取清单
                    Game.Debug.Info("[VersionManager] Online mode: loading manifest from remote server");
                    return _versionManager.LoadGlobalManifestAsync();

                default:
                    Game.Debug.Warning($"[VersionManager] Unknown resource mode: {_resourceMode}, skipping manifest loading");
                    return UniTask.FromResult(false);
            }
        }

        /// <summary>
        /// 获取资源服务器地址
        /// </summary>
        public string GetResourceServerUrl()
        {
            return _versionManager.GetBaseUrl();
        }

        /// <summary>
        /// 设置package版本
        /// </summary>
        /// <param name="packageName">Package名称</param>
        /// <param name="version">版本号</param>
        public void SetPackageVersion(string packageName, string version)
        {
            _versionManager.SetPackageVersion(packageName, version);
        }

        /// <summary>
        /// 获取 Bundle 加载服务（内部使用）
        /// </summary>
        internal BundleLoaderService GetBundleService()
        {
            return _bundleService;
        }

        /// <summary>
        /// 异步加载资源包
        /// </summary>
        public async UniTask<IPackageManager> LoadPackageAsync(string packageName, string version = "")
        {
            if (_resourceMode == EResourceMode.None)
            {
                throw new System.Exception("Resource mode not set. Call SetMode() first.");
            }

            if (_packages.TryGetValue(packageName, out var existingPackage))
            {
                if (existingPackage.Status == PackageStatus.Ready)
                {
                    Game.Debug.Warning($"Package '{packageName}' is already loaded");
                    return existingPackage;
                }

                if (existingPackage.Status == PackageStatus.Initializing)
                {
                    Game.Debug.Warning($"Package '{packageName}' is initializing");
                    return null;
                }
            }

            // 根据模式创建不同的 Loader
            IPackageLoader loader = _resourceMode switch
            {
                EResourceMode.EditorSimulator => CreateEditorSimulatorLoader(packageName),
                EResourceMode.Offline => CreateOfflineLoader(packageName),
                EResourceMode.Online => CreateOnlineLoader(packageName),
                _ => throw new System.Exception($"Unsupported resource mode: {_resourceMode}")
            };
            if (string.IsNullOrEmpty(version) is false)
                _versionManager.SetPackageVersion(packageName, version);
            var package = new ResourcePackage(packageName, version, loader);
            InitializePackageProviders(package);

            _packages[packageName] = package;

            // 初始化
            var success = await package.Initialization(this);
            if (!success)
            {
                Game.Debug.Error($"Failed to load package: {packageName}");
                _packages.Remove(packageName);
                return null;
            }

            Game.Debug.Debug($"Package loaded: {packageName}");
            return package;
        }

        private IPackageLoader CreateEditorSimulatorLoader(string packageName)
        {
#if UNITY_EDITOR
            return new EditorSimulatorLoader(packageName);
#else
            throw new System.Exception("EditorSimulator mode only available in Unity Editor");
#endif
        }

        private IPackageLoader CreateOfflineLoader(string packageName)
        {
            // Offline 模式：仅从 StreamingAssets 加载，不下载
            return new BundlePackageLoader(packageName, _versionManager, enableRemote: false);
        }

        private IPackageLoader CreateOnlineLoader(string packageName)
        {
            if (string.IsNullOrEmpty(_versionManager.GetBaseUrl()))
            {
                throw new System.Exception("Resource server URL not set. Call SetResourceServerUrl() first.");
            }

            // Online 模式：支持 StreamingAssets（母包资源）+ 网络下载
            return new BundlePackageLoader(packageName, _versionManager, enableRemote: true);
        }

        /// <summary>
        /// 卸载资源包
        /// </summary>
        public void UnloadPackage(string packageName)
        {
            if (_packages.TryGetValue(packageName, out var package))
            {
                package.OnClearup();
                _packages.Remove(packageName);

                Game.Debug.Debug($"Package unloaded: {packageName} {package.Version}");
            }
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public async UniTask<AssetHandle<T>> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            // 遍历所有包查找资源
            foreach (var package in _packages.Values)
            {
                if (package.Status != PackageStatus.Ready)
                    continue;

                if (package.Contains(address))
                {
                    return await package.LoadAssetAsync<T>(address);
                }
            }

            // 未找到，尝试从内置资源或远程资源加载
            if (address.StartsWith("http://") || address.StartsWith("https://"))
            {
                if (_packages.TryGetValue("RemotePackage", out var remotePackage))
                {
                    return await remotePackage.LoadAssetAsync<T>(address);
                }
            }
            else if (address.StartsWith("Resources/"))
            {
                if (_packages.TryGetValue("BuiltinPackage", out var builtinPackage))
                {
                    return await builtinPackage.LoadAssetAsync<T>(address);
                }
            }

            Game.Debug.Warning($"[Resource] Asset '{address}' not found in any package");
            return AssetHandle<T>.Failure(address);
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public async UniTask<SceneHandle> LoadSceneAsync(string name, LoadSceneMode mode = LoadSceneMode.Additive, Action<float> progressCallback = null)
        {
            foreach (var package in _packages.Values)
            {
                if (package.Status != PackageStatus.Ready)
                    continue;

                if (package.Contains(name))
                {
                    return await package.LoadSceneAsync(name, mode, progressCallback);
                }
            }

            Game.Debug.Warning($"Scene '{name}' not found in any package");
            return default;
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(BaseHandle handle)
        {
            foreach (var package in _packages.Values)
            {
                if (package.Contains(handle.Address))
                {
                    package.Unload(handle);
                    return;
                }
            }
        }

        /// <summary>
        /// 卸载未使用的资源
        /// </summary>
        public void UnloadUnusedAssets()
        {
            foreach (var package in _packages.Values)
            {
                package.UnloadUnusedAssets();
            }

            _bundleService.UnloadUnusedBundles();

            Game.Debug.Debug("Unused assets unloaded");
        }

        /// <summary>
        /// 设置自动卸载间隔
        /// </summary>
        public void SetAutoUnloadInterval(float minutes)
        {
            _autoUnloadInterval = minutes;
            _lastUnloadTime = 0f;
        }

        /// <summary>
        /// 通过 Label 批量加载资源
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAssetsByLabelAsync<T>(string label)
            where T : UnityEngine.Object
        {
            var allHandles = new List<AssetHandle<T>>();

            foreach (var package in _packages.Values)
            {
                if (package.Status != PackageStatus.Ready)
                    continue;

                var handles = await package.LoadAssetsByLabelAsync<T>(label);
                allHandles.AddRange(handles);
            }

            return allHandles;
        }

        /// <summary>
        /// 通过多个 Label 批量加载资源（交集）
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAssetsByLabelsAsync<T>(params string[] labels)
            where T : UnityEngine.Object
        {
            var allHandles = new List<AssetHandle<T>>();

            foreach (var package in _packages.Values)
            {
                if (package.Status != PackageStatus.Ready)
                    continue;

                var handles = await package.LoadAssetsByLabelsAsync<T>(labels);
                allHandles.AddRange(handles);
            }

            return allHandles;
        }

        /// <summary>
        /// 加载 Bundle 中的所有资源
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAllAssetsInBundleAsync<T>(string bundleName)
            where T : UnityEngine.Object
        {
            foreach (var package in _packages.Values)
            {
                if (package.Status != PackageStatus.Ready)
                    continue;

                var handles = await package.LoadAllAssetsInBundleAsync<T>(bundleName);
                if (handles.Count > 0)
                    return handles;
            }

            return new List<AssetHandle<T>>();
        }

        /// <summary>
        /// 加载子资源（如 Atlas 中的 Sprite）
        /// </summary>
        public async UniTask<AssetHandle<T>> LoadSubAssetAsync<T>(string address, string subAssetName)
            where T : UnityEngine.Object
        {
            foreach (var package in _packages.Values)
            {
                if (package.Status != PackageStatus.Ready)
                    continue;

                if (package.Contains(address))
                {
                    return await package.LoadSubAssetAsync<T>(address, subAssetName);
                }
            }

            Game.Debug.Warning($"Asset '{address}' not found in any package");
            return default;
        }

        /// <summary>
        /// 通过类型批量加载资源（支持继承类型匹配）
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAssetsByTypeAsync<T>()
            where T : UnityEngine.Object
        {
            var allHandles = new List<AssetHandle<T>>();

            foreach (var package in _packages.Values)
            {
                if (package.Status != PackageStatus.Ready)
                    continue;

                var handles = await package.LoadAssetsByTypeAsync<T>();
                allHandles.AddRange(handles);
            }

            return allHandles;
        }
    }
}
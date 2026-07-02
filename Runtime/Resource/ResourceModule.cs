using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;

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
        private readonly List<ModeBase> _modes = new List<ModeBase>();
        private readonly HashSet<string> _localPackages = new HashSet<string>(StringComparer.Ordinal);
        private ResourceInitializeState _initializeState = ResourceInitializeState.NotInitialized;
        private UniTaskCompletionSource _initializeCompletion;
        private Exception _startupError;

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
            ReleaseModes();
            _localPackages.Clear();
            _initializeCompletion = null;
            _initializeState = ResourceInitializeState.NotInitialized;
            _startupError = null;
            var operation = App.Operation.ExecuteWithKey<StartupOperationHandle>("ResourceModule:startup", this);
            if (operation.Status is OperationStatus.Succeeded)
            {
                return;
            }

            _startupError = operation.Error;
            ReleaseModes();
            _localPackages.Clear();
            _setting = null;
            _manifest = null;
            _initializeState = ResourceInitializeState.NotInitialized;
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
            var upgradeFromLocal = _initializeState == ResourceInitializeState.LocalInitialized;
            var preserveStartupModes = upgradeFromLocal && CanReuseStartupModes(setting);
            var startupSetting = _setting;
            var startupManifest = _manifest;
            var startupLocalPackages = preserveStartupModes
                ? new HashSet<string>(_localPackages, StringComparer.Ordinal)
                : null;
            var completionSource = new UniTaskCompletionSource();
            _initializeCompletion = completionSource;
            _initializeState = ResourceInitializeState.Initializing;
            try
            {
                var operation = await App.Operation.WaitCompletionWithKeyAsync<FullReadyOperationHandle>(
                    $"{setting.Mode}:full",
                    this,
                    setting,
                    preserveStartupModes);
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    throw operation.Error ?? new GameException($"Resource manifest initialize failed. Mode: {setting.Mode}");
                }

                if (!ReferenceEquals(_initializeCompletion, completionSource) || _initializeState != ResourceInitializeState.Initializing)
                {
                    throw new GameException("ResourceModule initialization was interrupted.");
                }

                _initializeState = ResourceInitializeState.Initialized;
                completionSource.TrySetResult();
            }
            catch (Exception exception)
            {
                if (preserveStartupModes)
                {
                    ReleaseNonStartupModes();
                    _setting = startupSetting;
                    _manifest = startupManifest;
                    _localPackages.Clear();
                    foreach (var package in startupLocalPackages)
                    {
                        _localPackages.Add(package);
                    }

                    _initializeState = ResourceInitializeState.LocalInitialized;
                }
                else
                {
                    ReleaseModes();
                    _localPackages.Clear();
                    _setting = null;
                    _manifest = null;
                    if (ReferenceEquals(_initializeCompletion, completionSource) && _initializeState == ResourceInitializeState.Initializing)
                    {
                        _initializeState = ResourceInitializeState.Failed;
                    }
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
            ReleaseModes();
            _localPackages.Clear();
            _manifest = null;
            _setting = null;
            _initializeCompletion = null;
            _initializeState = ResourceInitializeState.NotInitialized;
        }

        /// <summary>
        /// 解析资源初始化设置。
        /// </summary>
        /// <param name="settings">资源设置。</param>
        /// <returns>资源设置。</returns>
        private static ResourceSettings ResolveSettings(ResourceSettings settings)
        {
            if (settings == null)
            {
                throw new GameException("ResourceSettings is required. Configure FrameworkStartupModuleOptions.ResourceSettings or pass ResourceSettings to InitializeAsync.");
            }

            return settings;
        }

        private static bool CanReuseStartupModes(ResourceSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            var manifestName = string.IsNullOrWhiteSpace(settings.ManifestName)
                ? ResourceSettings.MANIFEST_NAME
                : settings.ManifestName.Replace('\\', '/');
            if (System.IO.Path.IsPathRooted(manifestName))
            {
                var startupManifestPath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, ResourceSettings.MANIFEST_NAME).Replace('\\', '/');
                return string.Equals(manifestName, startupManifestPath, StringComparison.Ordinal);
            }

            return string.Equals(manifestName, ResourceSettings.MANIFEST_NAME, StringComparison.Ordinal);
        }

        /// <summary>
        /// 初始化资源包
        /// </summary>
        /// <param name="package">资源包名</param>
        /// <returns>资源包句柄</returns>
        /// <exception cref="GameException">资源包初始化异常</exception>
        public UniTask<OperationHandle> InitializePackageAsync(string package)
        {
            ValidateKey(package, nameof(package));
            EnsureReady();
            if (_modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = GetModeByPackage(package);
            if (playmode == null)
            {
                throw new GameException($"No play mode contains assets with package: {package}");
            }

            return playmode.InitializePackageAsync(package);
        }

        /// <summary>
        /// 卸载资源包
        /// </summary>
        /// <param name="package">资源包名</param>
        /// <returns>资源包句柄</returns>
        /// <exception cref="GameException">资源包卸载异常</exception>
        public UniTask<OperationHandle> UninitializePackageAsync(string package)
        {
            ValidateKey(package, nameof(package));
            EnsureReady();
            if (_modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this._modes.FirstOrDefault(x => x.HasPackage(package));
            if (playmode == null)
            {
                throw new GameException($"No play mode contains assets with package: {package}");
            }

            return playmode.UninitializePackageAsync(package);
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <returns>资源加载任务</returns>
        /// <exception cref="GameException">资源加载错误</exception>
        public UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            ValidateKey(location, nameof(location));
            EnsureReady();
            if (_modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this._modes.FirstOrDefault(x => x.HasAsset(location));
            if (playmode == null)
            {
                throw new GameException($"Asset not found at location: {location}");
            }

            return playmode.LoadAssetAsync(location);
        }

        /// <summary>
        /// 根据资源标签异步加载资源
        /// </summary>
        /// <param name="label">资源标签</param>
        /// <returns>资源加载任务</returns>
        /// <exception cref="GameException">资源加载错误</exception>
        public async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            EnsureReady();
            if (_modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this._modes.Where(pm => pm.HasAsset(label)).ToArray();
            if (playmode.Length == 0)
            {
                throw new GameException($"No play mode contains assets with label: {label}");
            }

            List<AssetHandle> handles = new List<AssetHandle>();
            foreach (var mode in playmode)
            {
                var results = await mode.LoadAssetsByLabelAsync(label);
                handles.AddRange(results);
            }

            return handles;
        }

        /// <summary>
        /// 根据资源类型加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns>加载的资源列表</returns>
        /// <exception cref="GameException">资源加载错误</exception>
        public async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>() where T : UnityEngine.Object
        {
            EnsureReady();
            if (_modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var assetTypeName = typeof(T).Name;
            var playmode = this._modes.Where(pm => pm.HasAsset(assetTypeName)).ToArray();
            if (playmode.Length == 0)
            {
                throw new GameException($"No play mode contains assets of type: {typeof(T).FullName}");
            }

            List<AssetHandle> handles = new List<AssetHandle>();
            foreach (var mode in playmode)
            {
                var results = await mode.LoadAssetsByTypeAsync<T>();
                handles.AddRange(results);
            }

            return handles;
        }

        /// <summary>
        /// 架子原始资源
        /// </summary>
        /// <param name="location">寻址参数</param>
        /// <returns>资源句柄</returns>
        /// <exception cref="GameException">资源加载错误</exception>
        public UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            ValidateKey(location, nameof(location));
            EnsureReady();
            if (_modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this._modes.FirstOrDefault(x => x.HasAsset(location));
            if (playmode == null)
            {
                throw new GameException($"Asset not found at location: {location}");
            }

            return playmode.LoadRawAssetAsync(location);
        }

        /// <summary>
        /// 根据资源标签加载资源列表
        /// </summary>
        /// <param name="label">资源标签</param>
        /// <returns>资源列表</returns>
        /// <exception cref="GameException">资源加载错误</exception>
        public async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            EnsureReady();
            if (_modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this._modes.Where(pm => pm.HasAsset(label)).ToArray();
            if (playmode.Length == 0)
            {
                throw new GameException($"No play mode contains assets with label: {label}");
            }

            List<RawAssetHandle> handles = new List<RawAssetHandle>();
            foreach (var mode in playmode)
            {
                var results = await mode.LoadRawAssetsByLabelAsync(label);
                handles.AddRange(results);
            }

            return handles;
        }

        /// <summary>
        /// 加载场景资源
        /// </summary>
        /// <param name="name">场景名称</param>
        /// <returns>场景资源句柄</returns>
        /// <exception cref="GameException">场景加载异常</exception>
        public UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name)
        {
            ValidateKey(name, nameof(name));
            EnsureReady();
            if (_modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this._modes.FirstOrDefault(x => x.HasAsset(name));
            if (playmode == null)
            {
                throw new GameException($"Scene not found: {name}");
            }

            return playmode.LoadSceneAssetAsync(name);
        }

        /// <summary>
        /// 卸载未使用的资源
        /// </summary>
        /// <returns>异步任务</returns>
        /// <exception cref="GameException">卸载异常</exception>
        public async UniTask UnloadUnusedAssetAsync()
        {
            EnsureReady();
            if (_modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            List<UniTask> unloadTasks = new List<UniTask>();
            foreach (var playMode in _modes)
            {
                unloadTasks.Add(playMode.UnloadUnusedAssetAsync());
            }

            await UniTask.WhenAll(unloadTasks);
            await UnityEngine.Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        /// <param name="handle">资源列表</param>
        /// <returns>异步任务</returns>
        /// <exception cref="ArgumentNullException">空参数异常</exception>
        /// <exception cref="GameException">资源加载异常</exception>
        public UniTask UnloadAsset(AssetHandle handle)
        {
            return UnloadHandle(handle, (m, h) => m.UnloadAsset(h), "Asset");
        }

        /// <summary>
        /// 卸载二进制资源。
        /// </summary>
        /// <param name="handle">二进制资源句柄。</param>
        /// <returns>异步任务。</returns>
        /// <exception cref="ArgumentNullException">空参数异常。</exception>
        /// <exception cref="GameException">资源加载异常。</exception>
        public UniTask UnloadRawAsset(RawAssetHandle handle)
        {
            return UnloadHandle(handle, (m, h) => m.UnloadRawAsset(h), "Raw asset");
        }

        /// <summary>
        /// 卸载场景资源。
        /// </summary>
        /// <param name="handle">场景资源句柄。</param>
        /// <returns>异步任务。</returns>
        /// <exception cref="ArgumentNullException">空参数异常。</exception>
        /// <exception cref="GameException">资源加载异常。</exception>
        public UniTask UnloadSceneAsset(SceneAssetHandle handle)
        {
            return UnloadHandle(handle, (m, h) => m.UnloadSceneAsset(h), "Scene");
        }

        /// <summary>
        /// 卸载资源句柄的通用逻辑。
        /// </summary>
        private UniTask UnloadHandle<THandle>(THandle handle, Func<ModeBase, THandle, UniTask> unloader, string assetTypeLabel)
            where THandle : ResourceHandle
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            EnsureReady();
            if (_modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            if (handle.Info == null)
            {
                return UniTask.CompletedTask;
            }

            var location = handle.Info.Location;
            var playmode = _modes.FirstOrDefault(x => x.HasAsset(location));
            if (playmode == null)
            {
                throw new GameException($"{assetTypeLabel} not found: {location}");
            }

            return unloader(playmode, handle);
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="value">参数</param>
        /// <param name="parameterName">参数名</param>
        /// <exception cref="ArgumentNullException">空参数异常</exception>
        /// <exception cref="ArgumentException">空参数异常</exception>
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
        /// 根据资源模式获取资源模块
        /// </summary>
        /// <param name="mode">资源模式</param>
        /// <returns>资源模块</returns>
        private ModeBase GetModeByType(ResourceMode mode)
        {
            return mode switch
            {
                ResourceMode.EditorSimulator => this._modes.FirstOrDefault(x => x is EditorSimulatorMode),
                ResourceMode.Offline => this._modes.FirstOrDefault(x => x is StreamingAssetMode),
                ResourceMode.Online => this._modes.FirstOrDefault(x => x is BundleMode),
                ResourceMode.Web => this._modes.FirstOrDefault(x => x is WebGLMode),
                _ => null
            };
        }

        /// <summary>
        /// 根据资源包获取资源模式。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源模式。</returns>
        private ModeBase GetModeByPackage(string package)
        {
            if (string.Equals(package, BuiltinMode.BUILTIN_PACKAGE_NAME, StringComparison.Ordinal))
            {
                return this._modes.FirstOrDefault(x => x is BuiltinMode);
            }

            if (_setting != null && _setting.Mode == ResourceMode.EditorSimulator)
            {
                var editorSimulatorMode = this._modes.FirstOrDefault(x => x is EditorSimulatorMode);
                if (editorSimulatorMode != null)
                {
                    return editorSimulatorMode;
                }
            }

            var streamingAssetMode = _localPackages.Contains(package)
                ? this._modes.FirstOrDefault(x => x is StreamingAssetMode)
                : null;
            if (streamingAssetMode != null)
            {
                return streamingAssetMode;
            }

            return _setting == null ? null : GetModeByType(this._setting.Mode);
        }

        /// <summary>
        /// 创建资源模块
        /// </summary>
        /// <param name="mode">资源模式</param>
        /// <returns>资源模块</returns>
        private ModeBase CreateModeByType(ResourceMode mode)
        {
            return mode switch
            {
                ResourceMode.EditorSimulator => new EditorSimulatorMode(_manifest),
                ResourceMode.Offline => new StreamingAssetMode(_manifest),
                ResourceMode.Online => new BundleMode(_manifest),
                ResourceMode.Web => new WebGLMode(_manifest),
                _ => null
            };
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
        /// 释放所有资源模式。
        /// </summary>
        private void ReleaseModes()
        {
            foreach (var mode in _modes)
            {
                mode.Release();
            }

            _modes.Clear();
        }

        private void ReleaseNonStartupModes()
        {
            foreach (var mode in _modes.Where(x => x is not BuiltinMode && x is not StreamingAssetMode).ToArray())
            {
                mode.Release();
                _modes.Remove(mode);
            }
        }


        /// <summary>
        /// 执行显式反初始化流程。
        /// </summary>
        /// <returns>反初始化任务。</returns>
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

            ReleaseModes();
            _localPackages.Clear();
            _manifest = null;
            _setting = null;
            _initializeState = ResourceInitializeState.NotInitialized;
            _initializeCompletion = null;
        }
    }
}

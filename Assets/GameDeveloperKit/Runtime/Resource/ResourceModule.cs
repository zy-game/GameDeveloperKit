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
        /// <summary>
        /// 存储 manifest。
        /// </summary>
        private ManifestInfo _manifest;
        /// <summary>
        /// 存储 setting。
        /// </summary>
        private ResourceSettings _setting;
        /// <summary>
        /// 存储 modes。
        /// </summary>
        private readonly List<ModeBase> modes = new List<ModeBase>();
        /// <summary>
        /// 存储 initialize State。
        /// </summary>
        private ResourceInitializeState _initializeState = ResourceInitializeState.NotInitialized;
        /// <summary>
        /// 存储 initialize Completion。
        /// </summary>
        private UniTaskCompletionSource _initializeCompletion;

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
            _initializeCompletion = null;
            _initializeState = ResourceInitializeState.NotInitialized;
        }

        /// <summary>
        /// 显式初始化资源模块。
        /// </summary>
        /// <param name="options">初始化参数。</param>
        /// <returns>初始化任务。</returns>
        public async UniTask InitializeAsync(ResourceInitializeOptions options = null)
        {
            if (_initializeState == ResourceInitializeState.Initialized)
            {
                return;
            }

            if (_initializeState == ResourceInitializeState.Initializing && _initializeCompletion != null)
            {
                await _initializeCompletion.Task;
                return;
            }

            var completionSource = new UniTaskCompletionSource();
            _initializeCompletion = completionSource;
            _initializeState = ResourceInitializeState.Initializing;
            try
            {
                await InitializeInternalAsync(options);
                if (!ReferenceEquals(_initializeCompletion, completionSource) || _initializeState != ResourceInitializeState.Initializing)
                {
                    throw new GameException("ResourceModule initialization was interrupted.");
                }

                _initializeState = ResourceInitializeState.Initialized;
                completionSource.TrySetResult();
            }
            catch (Exception exception)
            {
                ReleaseModes();
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
            _manifest = null;
            _setting = null;
            _initializeCompletion = null;
            _initializeState = ResourceInitializeState.NotInitialized;
        }

        /// <summary>
        /// 执行资源模块初始化流程。
        /// </summary>
        /// <param name="options">初始化参数。</param>
        /// <returns>初始化任务。</returns>
        private async UniTask InitializeInternalAsync(ResourceInitializeOptions options)
        {
            var setting = ResolveSettings(options);
            var operation = await App.Operation.WaitCompletionWithKeyAsync<InitializeOperationHandle>(this, setting);
            if (operation.Status is not OperationStatus.Succeeded || operation.Value == null)
            {
                throw new GameException($"Resource manifest initialize failed. Mode: {setting.Mode}", operation.Error);
            }

            _setting = setting;
            _manifest = operation.Value;

            var builtinMode = new BuiltinMode(_manifest);
            modes.Add(builtinMode);

            var selectedMode = CreateModeByType(setting.Mode);
            if (selectedMode == null)
            {
                throw new GameException($"Unsupported resource mode: {setting.Mode}");
            }

            if (modes.Any(x => x.GetType() == selectedMode.GetType()) is false)
            {
                modes.Add(selectedMode);
            }

            await InitializeBuiltinModeAsync(builtinMode);
            await InitializeDefaultPackagesAsync(setting);
        }

        /// <summary>
        /// 解析资源初始化设置。
        /// </summary>
        /// <param name="options">初始化参数。</param>
        /// <returns>资源设置。</returns>
        private static ResourceSettings ResolveSettings(ResourceInitializeOptions options)
        {
            var setting = options?.Settings ?? Resources.Load<ResourceSettings>("ResourceSettings");
            if (setting == null)
            {
                throw new GameException("ResourceSettings not found.");
            }

            return setting;
        }

        /// <summary>
        /// 初始化 Builtin Mode Async。
        /// </summary>
        /// <param name="builtinMode">builtin Mode 参数。</param>
        /// <returns>操作完成任务。</returns>
        private async UniTask InitializeBuiltinModeAsync(BuiltinMode builtinMode)
        {
            if (_manifest.GetBundle(BuiltinMode.BUILTIN_PACKAGE_NAME) == null)
            {
                return;
            }

            var builtinOperation = await builtinMode.InitializePackageAsync(BuiltinMode.BUILTIN_PACKAGE_NAME);
            if (builtinOperation.Status is not OperationStatus.Succeeded)
            {
                throw new GameException($"{BuiltinMode.BUILTIN_PACKAGE_NAME} initialize failed.", builtinOperation.Error);
            }
        }

        /// <summary>
        /// 初始化默认资源包。
        /// </summary>
        /// <param name="setting">资源设置。</param>
        /// <returns>初始化任务。</returns>
        private async UniTask InitializeDefaultPackagesAsync(ResourceSettings setting)
        {
            if (setting.DefaultPackages == null || setting.DefaultPackages.Length == 0)
            {
                return;
            }

            for (var i = 0; i < setting.DefaultPackages.Length; i++)
            {
                var package = setting.DefaultPackages[i];
                if (string.IsNullOrWhiteSpace(package))
                {
                    continue;
                }

                var packageOperation = await InitializePackageInternalAsync(package);
                if (packageOperation.Status is not OperationStatus.Succeeded)
                {
                    throw new GameException($"Default package initialize failed: {package}", packageOperation.Error);
                }
            }
        }

        /// <summary>
        /// 初始化资源包内部入口。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源包句柄。</returns>
        private UniTask<OperationHandle> InitializePackageInternalAsync(string package)
        {
            if (modes.Count == 0)
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
        /// 初始化资源包
        /// </summary>
        /// <param name="package">资源包名</param>
        /// <returns>资源包句柄</returns>
        /// <exception cref="GameException">资源包初始化异常</exception>
        public UniTask<OperationHandle> InitializePackageAsync(string package)
        {
            ValidateKey(package, nameof(package));
            EnsureReady();
            return InitializePackageInternalAsync(package);
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
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this.modes.FirstOrDefault(x => x.HasPackage(package));
            if (playmode == null)
            {
                throw new GameException($"No play mode contains assets with package: {package}");
            }

            return playmode.UninitializePackageAsync(package);
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="location">location 参数。</param>
        /// <returns>资源加载任务</returns>
        /// <exception cref="GameException">资源加载错误</exception>
        public UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            ValidateKey(location, nameof(location));
            EnsureReady();
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this.modes.FirstOrDefault(x => x.HasAsset(location));
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
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this.modes.Where(pm => pm.HasAsset(label));
            if (playmode == null)
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
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var assetTypeName = typeof(T).Name;
            var playmode = this.modes.Where(pm => pm.HasAsset(assetTypeName));
            if (playmode is null)
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
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this.modes.FirstOrDefault(x => x.HasAsset(location));
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
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this.modes.Where(pm => pm.HasAsset(label)).ToArray();
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
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this.modes.FirstOrDefault(x => x.HasAsset(name));
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
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            List<UniTask> unloadTasks = new List<UniTask>();
            foreach (var playMode in modes)
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
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            EnsureReady();
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            if (handle.Info == null)
            {
                return UniTask.CompletedTask;
            }

            var location = handle.Info.Location;
            var playmode = this.modes.FirstOrDefault(x => x.HasAsset(location));
            if (playmode == null)
            {
                throw new GameException($"Asset not found: {location}");
            }

            return playmode.UnloadAsset(handle);
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
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            EnsureReady();
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            if (handle.Info == null)
            {
                return UniTask.CompletedTask;
            }

            var location = handle.Info.Location;
            var playmode = this.modes.FirstOrDefault(x => x.HasAsset(location));
            if (playmode == null)
            {
                throw new GameException($"Raw asset not found: {location}");
            }

            return playmode.UnloadRawAsset(handle);
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
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            EnsureReady();
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            if (handle.Info == null)
            {
                return UniTask.CompletedTask;
            }

            var location = handle.Info.Location;
            var playmode = this.modes.FirstOrDefault(x => x.HasAsset(location));
            if (playmode == null)
            {
                throw new GameException($"Scene not found: {location}");
            }

            return playmode.UnloadSceneAsset(handle);
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
                ResourceMode.EditorSimulator => this.modes.FirstOrDefault(x => x is EditorSimulatorMode),
                ResourceMode.Offline => this.modes.FirstOrDefault(x => x is StreamingAssetMode),
                ResourceMode.Online => this.modes.FirstOrDefault(x => x is BundleMode),
                ResourceMode.Web => this.modes.FirstOrDefault(x => x is WebGLMode),
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
                return this.modes.FirstOrDefault(x => x is BuiltinMode);
            }

            return GetModeByType(this._setting.Mode);
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
            if (IsInitialized is false || _setting == null || _manifest == null)
            {
                throw new GameException("ResourceModule is not initialized. Call InitializeAsync first.");
            }
        }

        /// <summary>
        /// 释放所有资源模式。
        /// </summary>
        private void ReleaseModes()
        {
            foreach (var mode in modes)
            {
                mode.Release();
            }

            modes.Clear();
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
            _manifest = null;
            _setting = null;
            _initializeState = ResourceInitializeState.NotInitialized;
            _initializeCompletion = null;
        }
    }
}

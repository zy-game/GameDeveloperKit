using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源模块
    /// </summary>
    public sealed partial class ResourceModule : GameModuleBase
    {
        private ManifestInfo _manifest;
        private ResourceSettings _setting;
        private readonly List<ModeBase> modes = new List<ModeBase>();

        public override async UniTask Startup()
        {
            var handle = await LoadAssetAsync("Resources/ResourceSettings");
            if (handle == null)
            {
                throw new GameException("Failed to load resource settings.");
            }

            var operationHandle = Super.Operation.Execute<ManifestOperationHandle>(_setting);
            await operationHandle.WaitCompletionAsync();
            _manifest = operationHandle.Value;
            if (_manifest == null)
            {
                throw new GameException("Failed to load resource settings.");
            }

            BuiltinMode builtinMode = null;
            _setting = handle.GetAsset<ResourceSettings>();
            modes.Add(new StreamingAssetMode(_manifest));
            modes.Add(builtinMode = new BuiltinMode(_manifest));
            modes.Add(CreateModeByType(_setting.Mode));
            await builtinMode.InitializePackageAsync(BuiltinMode.BUILTIN_PACKAGE_NAME);

            if (_setting.DefaultPackages == null || _setting.DefaultPackages.Length == 0)
            {
                return;
            }

            for (int i = 0; i < _setting.DefaultPackages.Length; i++)
            {
                await InitializePackageAsync(_setting.DefaultPackages[i]);
            }
        }

        public override UniTask Shutdown()
        {
            foreach (var mode in modes)
            {
                mode.Release();
            }

            return UniTask.CompletedTask;
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
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = GetModeByType(this._setting.Mode);
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
        /// <param name="location"></param>
        /// <returns>资源加载任务</returns>
        /// <exception cref="GameException">资源加载错误</exception>
        public UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            ValidateKey(location, nameof(location));
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
        public UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this.modes.FirstOrDefault(x => x.HasAsset(label));
            if (playmode == null)
            {
                throw new GameException($"No play mode contains assets with label: {label}");
            }

            return playmode.LoadRawAssetsByLabelAsync(label);
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
        public UniTask UnloadUnusedAssetAsync()
        {
            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            List<UniTask> unloadTasks = new List<UniTask>();
            foreach (var playMode in modes)
            {
                unloadTasks.Add(playMode.UnloadUnusedAssetAsync());
            }

            return UniTask.WhenAll(unloadTasks);
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

            if (modes.Count == 0)
            {
                throw new GameException("No resource play mode is available.");
            }

            var playmode = this.modes.FirstOrDefault(x => x.HasAsset(handle.Info.Location));
            if (playmode == null)
            {
                throw new GameException($"Asset not found: {handle.Info.Location}");
            }

            return playmode.UnloadAsset(handle);
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
            return this._setting.Mode switch
            {
                ResourceMode.EditorSimulator => this.modes.FirstOrDefault(x => x is EditorSimulatorMode),
                ResourceMode.Offline => this.modes.FirstOrDefault(x => x is StreamingAssetMode),
                ResourceMode.Online => this.modes.FirstOrDefault(x => x is BundleMode),
                ResourceMode.Web => this.modes.FirstOrDefault(x => x is WebGLMode),
                _ => null
            };
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
    }
}

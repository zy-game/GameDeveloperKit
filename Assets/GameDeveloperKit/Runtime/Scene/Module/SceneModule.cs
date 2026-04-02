using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 场景模块，提供场景加载、卸载、切换和历史管理功能
    /// </summary>
    public sealed partial class SceneModule : IGameFrameworkLifecycleModule
    {
        /// <summary>
        /// 场景加载开始事件名称
        /// </summary>
        public const string SceneLoadStartedEventName = "GameDeveloperKit.Scene.LoadStarted";

        /// <summary>
        /// 场景切换开始事件名称
        /// </summary>
        public const string SceneTransitionStartedEventName = "GameDeveloperKit.Scene.TransitionStarted";

        /// <summary>
        /// 场景切换完成事件名称
        /// </summary>
        public const string SceneTransitionCompletedEventName = "GameDeveloperKit.Scene.TransitionCompleted";

        /// <summary>
        /// 场景切换失败事件名称
        /// </summary>
        public const string SceneTransitionFailedEventName = "GameDeveloperKit.Scene.TransitionFailed";

        /// <summary>
        /// 场景切换进度变化事件名称
        /// </summary>
        public const string SceneTransitionProgressChangedEventName = "GameDeveloperKit.Scene.TransitionProgressChanged";

        private readonly List<SceneHistoryEntry> _history = new();
        private readonly HashSet<string> _persistentScenes = new(StringComparer.Ordinal);
        private bool _isInitialized;
        private bool _diagnosticsRegistered;
        private int _transitionCount;
        private int _transitionFailureCount;
        private long _lastTransitionDurationMilliseconds;
        private float _lastTransitionProgress;

        /// <summary>
        /// 初始化场景模块并注册场景相关回调
        /// </summary>
        public SceneModule()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        /// <summary>
        /// 获取已加载的场景数量
        /// </summary>
        public int LoadedSceneCount => SceneManager.sceneCount;

        /// <summary>
        /// 获取历史记录数量
        /// </summary>
        public int HistoryCount => _history.Count;

        /// <summary>
        /// 获取是否可以返回上一个场景
        /// </summary>
        public bool CanGoBack => _history.Count > 1;

        /// <summary>
        /// 获取当前活动场景
        /// </summary>
        public Scene ActiveScene => SceneManager.GetActiveScene();

        /// <summary>
        /// 获取持久化场景数量
        /// </summary>
        public int PersistentSceneCount => _persistentScenes.Count;

        /// <summary>
        /// 获取模块状态
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 场景加载开始事件
        /// </summary>
        public event Action<SceneLoadInfo> SceneLoadStarted;

        /// <summary>
        /// 场景加载完成事件
        /// </summary>
        public event Action<Scene, LoadSceneMode> SceneLoaded;

        /// <summary>
        /// 场景卸载事件
        /// </summary>
        public event Action<Scene> SceneUnloaded;

        /// <summary>
        /// 活动场景改变事件
        /// </summary>
        public event Action<Scene, Scene> ActiveSceneChanged;

        /// <summary>
        /// 场景转换开始事件
        /// </summary>
        public event Action<SceneTransitionInfo> SceneTransitionStarted;

        /// <summary>
        /// 场景转换完成事件
        /// </summary>
        public event Action<SceneTransitionInfo> SceneTransitionCompleted;

        /// <summary>
        /// 场景转换失败事件
        /// </summary>
        public event Action<SceneTransitionInfo, Exception> SceneTransitionFailed;

        /// <summary>
        /// 场景转换进度改变事件
        /// </summary>
        public event Action<SceneTransitionInfo, float> SceneTransitionProgressChanged;

        /// <summary>
        /// 获取切换前的流程状态名称
        /// </summary>
        public string BeforeSwitchProcedureStateName { get; private set; }

        /// <summary>
        /// 获取切换后的流程状态名称
        /// </summary>
        public string AfterSwitchProcedureStateName { get; private set; }

        /// <summary>
        /// 异步初始化场景模块
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
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
        /// 异步关闭场景模块
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
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
        /// 获取历史记录
        /// </summary>
        /// <returns>场景键数组</returns>
        public string[] GetHistory()
        {
            var results = new string[_history.Count];
            for (var i = 0; i < _history.Count; i++)
            {
                results[i] = _history[i].SceneKey;
            }

            return results;
        }

        /// <summary>
        /// 获取持久化场景列表
        /// </summary>
        /// <returns>持久化场景键数组</returns>
        public string[] GetPersistentScenes()
        {
            var results = new string[_persistentScenes.Count];
            _persistentScenes.CopyTo(results);
            return results;
        }

        /// <summary>
        /// 通过场景名称或路径注册持久化场景
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <returns>如果注册成功返回true，否则返回false</returns>
        public bool RegisterPersistentScene(string sceneNameOrPath)
        {
            if (!TryGetLoadedScene(sceneNameOrPath, out var scene))
            {
                return false;
            }

            return RegisterPersistentScene(scene);
        }

        /// <summary>
        /// 注册持久化场景
        /// </summary>
        /// <param name="scene">场景</param>
        /// <returns>如果注册成功返回true，否则返回false</returns>
        public bool RegisterPersistentScene(Scene scene)
        {
            var sceneKey = GetSceneKey(scene);
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                return false;
            }

            return _persistentScenes.Add(sceneKey);
        }

        /// <summary>
        /// 注销持久化场景
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool UnregisterPersistentScene(string sceneNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(sceneNameOrPath))
            {
                return false;
            }

            return _persistentScenes.Remove(sceneNameOrPath)
                || (TryGetLoadedScene(sceneNameOrPath, out var scene) && _persistentScenes.Remove(GetSceneKey(scene)));
        }

        /// <summary>
        /// 清除所有持久化场景
        /// </summary>
        public void ClearPersistentScenes()
        {
            _persistentScenes.Clear();
        }

        /// <summary>
        /// 检查是否为持久化场景
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <returns>如果是持久化场景返回true，否则返回false</returns>
        public bool IsPersistentScene(string sceneNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(sceneNameOrPath))
            {
                return false;
            }

            if (_persistentScenes.Contains(sceneNameOrPath))
            {
                return true;
            }

            return TryGetLoadedScene(sceneNameOrPath, out var scene) && _persistentScenes.Contains(GetSceneKey(scene));
        }

        /// <summary>
        /// 检查场景是否已加载
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <returns>如果已加载返回true，否则返回false</returns>
        public bool IsLoaded(string sceneNameOrPath)
        {
            return TryGetLoadedScene(sceneNameOrPath, out _);
        }

        /// <summary>
        /// 尝试获取已加载的场景
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <param name="scene">输出的场景</param>
        /// <returns>如果获取成功返回true，否则返回false</returns>
        public bool TryGetLoadedScene(string sceneNameOrPath, out Scene scene)
        {
            if (string.IsNullOrWhiteSpace(sceneNameOrPath))
            {
                scene = default;
                return false;
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var loadedScene = SceneManager.GetSceneAt(i);
                if (IsSceneMatch(loadedScene, sceneNameOrPath))
                {
                    scene = loadedScene;
                    return true;
                }
            }

            scene = default;
            return false;
        }

        /// <summary>
        /// 获取已加载的场景
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <returns>场景</returns>
        /// <exception cref="InvalidOperationException">场景未加载</exception>
        public Scene GetLoadedScene(string sceneNameOrPath)
        {
            if (!TryGetLoadedScene(sceneNameOrPath, out var scene))
            {
                throw new InvalidOperationException($"Scene '{sceneNameOrPath}' is not loaded.");
            }

            return scene;
        }

        /// <summary>
        /// 设置活动场景
        /// </summary>
        /// <param name="scene">场景</param>
        /// <exception cref="ArgumentException">场景无效</exception>
        /// <exception cref="InvalidOperationException">设置活动场景失败</exception>
        public void SetActiveScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                throw new ArgumentException("Scene is invalid.", nameof(scene));
            }

            if (!SceneManager.SetActiveScene(scene))
            {
                throw new InvalidOperationException($"Failed to set scene '{GetSceneKey(scene)}' as active scene.");
            }
        }

        /// <summary>
        /// 尝试设置活动场景
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <returns>如果设置成功返回true，否则返回false</returns>
        public bool TrySetActiveScene(string sceneNameOrPath)
        {
            return TryGetLoadedScene(sceneNameOrPath, out var scene) && SceneManager.SetActiveScene(scene);
        }

        /// <summary>
        /// 加载场景
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <param name="loadMode">加载模式</param>
        /// <param name="remember">是否记录历史</param>
        /// <returns>场景句柄</returns>
        /// <exception cref="ArgumentException">场景名称或路径为空</exception>
        public SceneHandle Load(string sceneNameOrPath, LoadSceneMode loadMode = LoadSceneMode.Single, bool remember = true)
        {
            if (string.IsNullOrWhiteSpace(sceneNameOrPath))
            {
                throw new ArgumentException("Scene name or path can not be empty.", nameof(sceneNameOrPath));
            }

            return LoadInternal(CreateSceneLocation(sceneNameOrPath), loadMode, null, remember, false);
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <param name="loadMode">加载模式</param>
        /// <param name="remember">是否记录历史</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>场景句柄的异步任务</returns>
        /// <exception cref="ArgumentException">场景名称或路径为空</exception>
        public async UniTask<SceneHandle> LoadAsync(string sceneNameOrPath, LoadSceneMode loadMode = LoadSceneMode.Single, bool remember = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sceneNameOrPath))
            {
                throw new ArgumentException("Scene name or path can not be empty.", nameof(sceneNameOrPath));
            }

            return await LoadAsyncInternal(CreateSceneLocation(sceneNameOrPath), loadMode, null, remember, false, cancellationToken);
        }

        /// <summary>
        /// 从资源加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="loadMode">加载模式</param>
        /// <param name="packageName">包名</param>
        /// <param name="remember">是否记录历史</param>
        /// <returns>场景句柄</returns>
        /// <exception cref="ArgumentException">场景名称为空</exception>
        public SceneHandle LoadFromResource(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single, string packageName = null, bool remember = true)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name can not be empty.", nameof(sceneName));
            }

            return LoadInternal(new ResourceLocation { Name = sceneName }, loadMode, packageName, remember, true);
        }

        /// <summary>
        /// 异步从资源加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="loadMode">加载模式</param>
        /// <param name="packageName">包名</param>
        /// <param name="remember">是否记录历史</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>场景句柄的异步任务</returns>
        /// <exception cref="ArgumentException">场景名称为空</exception>
        public async UniTask<SceneHandle> LoadFromResourceAsync(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single, string packageName = null, bool remember = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name can not be empty.", nameof(sceneName));
            }

            return await LoadAsyncInternal(new ResourceLocation { Name = sceneName }, loadMode, packageName, remember, true, cancellationToken);
        }

        /// <summary>
        /// 异步卸载场景
        /// </summary>
        /// <param name="handle">场景句柄</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        /// <exception cref="ArgumentNullException">场景句柄为空</exception>
        public async UniTask UnloadAsync(SceneHandle handle, CancellationToken cancellationToken = default)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            cancellationToken.ThrowIfCancellationRequested();
            await handle.UnloadAsync(cancellationToken);
        }

        /// <summary>
        /// 异步卸载场景
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务，如果卸载成功返回true，否则返回false</returns>
        public async UniTask<bool> UnloadAsync(string sceneNameOrPath, CancellationToken cancellationToken = default)
        {
            if (!TryGetLoadedScene(sceneNameOrPath, out var scene))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await SceneManager.UnloadSceneAsync(scene);
            return true;
        }

        /// <summary>
        /// 异步返回上一个场景
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>场景句柄的异步任务</returns>
        /// <exception cref="InvalidOperationException">没有可返回的场景</exception>
        public async UniTask<SceneHandle> GoBackAsync(CancellationToken cancellationToken = default)
        {
            if (!CanGoBack)
            {
                throw new InvalidOperationException("No previous scene is available.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var currentIndex = _history.Count - 1;
            var targetIndex = currentIndex - 1;
            var target = _history[targetIndex];

            _history.RemoveAt(currentIndex);
            _history.RemoveAt(targetIndex);

            return await LoadFromHistoryAsync(target, cancellationToken);
        }

        /// <summary>
        /// 异步切换场景
        /// </summary>
        /// <param name="sceneNameOrPath">场景名称或路径</param>
        /// <param name="remember">是否记录历史</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>场景句柄的异步任务</returns>
        /// <exception cref="ArgumentException">场景名称或路径为空</exception>
        public UniTask<SceneHandle> SwitchAsync(string sceneNameOrPath, bool remember = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sceneNameOrPath))
            {
                throw new ArgumentException("Scene name or path can not be empty.", nameof(sceneNameOrPath));
            }

            return SwitchAsyncInternal(CreateSceneLocation(sceneNameOrPath), null, remember, false, cancellationToken);
        }

        /// <summary>
        /// 异步从资源切换场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="packageName">包名</param>
        /// <param name="remember">是否记录历史</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>场景句柄的异步任务</returns>
        /// <exception cref="ArgumentException">场景名称为空</exception>
        public UniTask<SceneHandle> SwitchFromResourceAsync(string sceneName, string packageName = null, bool remember = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name can not be empty.", nameof(sceneName));
            }

            return SwitchAsyncInternal(new ResourceLocation { Name = sceneName }, packageName, remember, true, cancellationToken);
        }

        /// <summary>
        /// 配置流程集成
        /// </summary>
        /// <param name="beforeSwitchProcedureStateName">切换前的流程状态名称</param>
        /// <param name="afterSwitchProcedureStateName">切换后的流程状态名称</param>
        public void ConfigureProcedureIntegration(string beforeSwitchProcedureStateName = null, string afterSwitchProcedureStateName = null)
        {
            BeforeSwitchProcedureStateName = beforeSwitchProcedureStateName;
            AfterSwitchProcedureStateName = afterSwitchProcedureStateName;
        }

        /// <summary>
        /// 清除历史记录
        /// </summary>
        public void ClearHistory()
        {
            _history.Clear();
        }

        /// <summary>
        /// 释放场景模块占用的所有资源
        /// </summary>
        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            RemoveDiagnosticsSnapshotProviders();
            _history.Clear();
            _persistentScenes.Clear();
            SceneLoadStarted = null;
            SceneLoaded = null;
            SceneUnloaded = null;
            ActiveSceneChanged = null;
            SceneTransitionStarted = null;
            SceneTransitionCompleted = null;
            SceneTransitionFailed = null;
            SceneTransitionProgressChanged = null;
            _isInitialized = false;
        }

        private static string GetSceneKey(Scene scene)
        {
            if (!scene.IsValid())
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path;
        }

        private static bool IsSceneMatch(Scene scene, string sceneNameOrPath)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            return string.Equals(scene.path, sceneNameOrPath, StringComparison.Ordinal)
                || string.Equals(scene.name, sceneNameOrPath, StringComparison.Ordinal);
        }

        private void RegisterHistory(SceneHistoryEntry entry, LoadSceneMode loadMode, bool remember)
        {
            if (!remember || entry == null || string.IsNullOrWhiteSpace(entry.SceneKey))
            {
                return;
            }

            if (_history.Count > 0 && string.Equals(_history[_history.Count - 1].SceneKey, entry.SceneKey, StringComparison.Ordinal))
            {
                _history[_history.Count - 1] = entry;
                return;
            }

            _history.Add(entry);
        }

        private void NotifySceneLoadStarted(SceneLoadInfo info)
        {
            SceneLoadStarted?.Invoke(info);

            if (Game.TryGetModule<EventModule>(out var eventModule))
            {
                eventModule.Raise(SceneLoadStartedEventName, this, info);
            }
        }

        private void NotifySceneTransitionStarted(SceneTransitionInfo info)
        {
            SceneTransitionStarted?.Invoke(info);

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Scene.LastTransitionTarget", GetSceneLocationLabel(info.Location));
                diagnostics.CaptureSnapshot("Scene.LastTransitionMode", info.RequestedLoadMode.ToString());
            }

            if (Game.TryGetModule<EventModule>(out var eventModule))
            {
                eventModule.Raise(SceneTransitionStartedEventName, this, info);
            }
        }

        private void NotifySceneTransitionProgress(SceneTransitionInfo info, float progress)
        {
            _lastTransitionProgress = progress;
            SceneTransitionProgressChanged?.Invoke(info, progress);

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Scene.LastTransitionProgress", progress.ToString("F2"));
            }

            if (Game.TryGetModule<EventModule>(out var eventModule))
            {
                eventModule.Raise(SceneTransitionProgressChangedEventName, this, info, progress);
            }
        }

        private void NotifySceneTransitionCompleted(SceneTransitionInfo info)
        {
            SceneTransitionCompleted?.Invoke(info);

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Scene.LastTransitionDurationMs", info.DurationMilliseconds.ToString());
                diagnostics.CaptureSnapshot("Scene.LastSceneKey", info.SceneKey ?? string.Empty);
            }

            if (Game.TryGetModule<EventModule>(out var eventModule))
            {
                eventModule.Raise(SceneTransitionCompletedEventName, this, info);
            }
        }

        private void NotifySceneTransitionFailed(SceneTransitionInfo info, Exception exception)
        {
            SceneTransitionFailed?.Invoke(info, exception);

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Scene.LastTransitionDurationMs", info.DurationMilliseconds.ToString());
                diagnostics.CaptureSnapshot("Scene.LastTransitionError", exception?.Message ?? string.Empty);
            }

            if (Game.TryGetModule<EventModule>(out var eventModule))
            {
                eventModule.Raise(SceneTransitionFailedEventName, this, info);
            }
        }

        private async UniTask UnloadNonPersistentScenesAsync(string targetSceneKey, CancellationToken cancellationToken)
        {
            var scenesToUnload = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                var sceneKey = GetSceneKey(scene);
                if (!scene.IsValid() || !scene.isLoaded || string.Equals(sceneKey, targetSceneKey, StringComparison.Ordinal) || _persistentScenes.Contains(sceneKey))
                {
                    continue;
                }

                scenesToUnload.Add(scene);
            }

            for (var i = 0; i < scenesToUnload.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SceneManager.UnloadSceneAsync(scenesToUnload[i]);
            }
        }

        private void RegisterDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Scene.ActiveScene", () => GetSceneKey(ActiveScene));
            diagnostics.RegisterSnapshotProvider("Scene.LoadedSceneCount", () => LoadedSceneCount.ToString());
            diagnostics.RegisterSnapshotProvider("Scene.PersistentSceneCount", () => PersistentSceneCount.ToString());
            diagnostics.RegisterSnapshotProvider("Scene.TransitionCount", () => _transitionCount.ToString());
            diagnostics.RegisterSnapshotProvider("Scene.TransitionFailureCount", () => _transitionFailureCount.ToString());
            diagnostics.RegisterSnapshotProvider("Scene.LastTransitionDurationMs", () => _lastTransitionDurationMilliseconds.ToString());
            diagnostics.RegisterSnapshotProvider("Scene.LastTransitionProgress", () => _lastTransitionProgress.ToString("F2"));
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Scene.ActiveScene");
            diagnostics.RemoveSnapshotProvider("Scene.LoadedSceneCount");
            diagnostics.RemoveSnapshotProvider("Scene.PersistentSceneCount");
            diagnostics.RemoveSnapshotProvider("Scene.TransitionCount");
            diagnostics.RemoveSnapshotProvider("Scene.TransitionFailureCount");
            diagnostics.RemoveSnapshotProvider("Scene.LastTransitionDurationMs");
            diagnostics.RemoveSnapshotProvider("Scene.LastTransitionProgress");
            _diagnosticsRegistered = false;
        }

        private static string GetSceneLocationLabel(ResourceLocation location)
        {
            if (location == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(location.FullPath)
                ? location.FullPath
                : location.Name ?? string.Empty;
        }

        private static async UniTask ChangeProcedureStateIfConfiguredAsync(string stateName, ResourceLocation location, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(stateName) || !Game.HasModule<ProcedureModule>())
            {
                return;
            }

            if (!Game.Procedure.HasState(stateName))
            {
                return;
            }

            await Game.Procedure.ChangeStateAsync(stateName, location, cancellationToken);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            SceneLoaded?.Invoke(scene, loadMode);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            _persistentScenes.Remove(GetSceneKey(scene));
            SceneUnloaded?.Invoke(scene);
        }

        private void OnActiveSceneChanged(Scene current, Scene next)
        {
            ActiveSceneChanged?.Invoke(current, next);
        }
    }
}


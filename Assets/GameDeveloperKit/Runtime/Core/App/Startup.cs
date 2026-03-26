using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 游戏启动管理器，负责初始化游戏框架和启动游戏流程。
    /// </summary>
    /// <remarks>
    /// 此组件应放置在游戏的启动场景中，负责按顺序初始化所有框架模块、
    /// 加载资源、执行启动任务并进入初始场景或流程。
    /// 提供了启动进度显示和错误处理机制。
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class Startup : MonoBehaviour
    {
        private static Startup _instance;

        [SerializeField] private ResourceSettings _resourceSettings;
        [SerializeField] private bool _prepareResourcesOnStartup = true;
        [SerializeField] private bool _initializeUI = true;
        [SerializeField] private bool _initializeSceneModule = true;
        [SerializeField] private bool _initializeProcedureModule = true;
        [SerializeField] private bool _persistAcrossScenes = true;
        [SerializeField] private bool _showOverlay = true;
        [SerializeField] private bool _hideOverlayOnComplete = true;
        [SerializeField] private string _overlayTitle = "GameDeveloperKit Startup";
        [SerializeField] private string _overrideLanguage;
        [SerializeField] private string _initialScene;
        [SerializeField] private LoadSceneMode _initialSceneLoadMode = LoadSceneMode.Single;
        [SerializeField] private string _initialProcedure;
        [SerializeField] private MonoBehaviour[] _startupTasks = Array.Empty<MonoBehaviour>();
        [SerializeField] private bool _useStructuredConfiguration;
        [SerializeField] private StartupConfiguration _configuration = new();

        private const float OverlayWidth = 520f;
        private const float OverlayHeight = 140f;
        private const string StartupOrderSnapshotKey = "Startup.ModuleInitializationOrder";
        private const string ShutdownOrderSnapshotKey = "Startup.ModuleShutdownOrder";

        private bool _isRunning;
        private bool _isCompleted;
        private bool _hasFailed;
        private string _currentStage = "Idle";
        private FrameworkError _lastError;
        private readonly FrameworkComposition _composition = new();

        /// <summary>
        /// 获取资源系统配置。
        /// </summary>
        /// <remarks>
        /// 根据 UseStructuredConfiguration 设置，从 StartupConfiguration 或直接配置中获取。
        /// </remarks>
        /// <summary>
        /// 异步运行启动流程。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示启动流程的异步任务。</returns>
        public ResourceSettings ResourceSettings => _useStructuredConfiguration ? _configuration?.ResourceSettings : _resourceSettings;

        /// <summary>
        /// 获取启动流程是否正在运行。
        /// </summary>
        /// <remarks>
        /// 在启动开始后到启动完成或失败前，此属性为 true。
        /// </remarks>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 获取启动流程是否已完成。
        /// </summary>
        /// <remarks>
        /// 当所有初始化步骤成功完成后，此属性为 true。
        /// </remarks>
        public bool IsCompleted => _isCompleted;

        /// <summary>
        /// 获取启动流程是否失败。
        /// </summary>
        /// <remarks>
        /// 当启动过程中出现错误时，此属性为 true。
        /// 可以通过 LastError 获取详细错误信息。
        /// </remarks>
        public bool HasFailed => _hasFailed;

        /// <summary>
        /// 获取当前启动阶段的名称。
        /// </summary>
        /// <remarks>
        /// 此值反映了启动流程的当前执行阶段，如 "Starting"、"Initializing Core Modules" 等。
        /// 可用于显示启动进度。
        /// </remarks>
        public string CurrentStage => _currentStage;

        /// <summary>
        /// 获取最后一次错误的详细信息。
        /// </summary>
        /// <remarks>
        /// 如果启动失败，此属性包含错误的代码、消息和其他诊断信息。
        /// 如果启动成功或尚未运行，此属性为 null。
        /// </remarks>
        public FrameworkError LastError => _lastError;

        /// <summary>
        /// 获取框架组合对象。
        /// </summary>
        /// <remarks>
        /// 此对象管理所有框架模块的初始化顺序和依赖关系。
        /// 可用于访问已初始化的模块实例。
        /// </remarks>
        public FrameworkComposition Composition => _composition;

        /// <summary>
        /// 在启动流程开始时触发。
        /// </summary>
        public event Action Started;

        /// <summary>
        /// 在启动流程成功完成时触发。
        /// </summary>
        public event Action Completed;

        /// <summary>
        /// 在启动流程失败时触发。
        /// </summary>
        /// <param name="exception">导致失败的具体异常对象。</param>
        public event Action<Exception> Failed;

        /// <summary>
        /// 在启动阶段发生变化时触发。
        /// </summary>
        /// <param name="stage">新阶段的名称。</param>
        public event Action<string> StageChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureConfiguration();
            if (PersistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            RunStartupAsync(this.GetCancellationTokenOnDestroy()).ForgetWithDiagnostics("Startup.RunFailed", nameof(Startup), nameof(Startup));
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            _instance = null;
            Game.ShutdownAllAsync().ForgetWithDiagnostics("Startup.ShutdownFailed", nameof(Startup), nameof(Startup));
        }

        /// <summary>
        /// 异步运行启动流程。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示启动流程的异步任务。</returns>
        public async UniTask RunStartupAsync(CancellationToken cancellationToken = default)
        {
            if (_isCompleted || _isRunning)
            {
                return;
            }

            _isRunning = true;
            _hasFailed = false;
            _lastError = null;
            SetStage("Starting");
            Started?.Invoke();

            try
            {
                Game.Diagnostics.LogInfo("Startup started.", nameof(Startup));
                SetStage("Initializing Core Modules");
                await InitializeCoreModulesAsync(cancellationToken);
                ValidateInitialSceneDependencies();
                await InitializeResourceAsync(cancellationToken);
                await InitializeFeatureModulesAsync(cancellationToken);
                await ExecuteStartupTasksAsync(cancellationToken);
                await EnterInitialFlowAsync(cancellationToken);

                _isCompleted = true;
                SetStage("Completed");
                Game.Diagnostics.LogInfo("Startup completed.", nameof(Startup));
                Completed?.Invoke();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetStage("Cancelled");
                Game.Diagnostics.LogWarning("Startup cancelled.", nameof(Startup));
            }
            catch (Exception exception)
            {
                HandleStartupException(exception);
                Failed?.Invoke(exception);
            }
            finally
            {
                _isRunning = false;
            }
        }

        private static async UniTask InitializeCoreModulesAsync(CancellationToken cancellationToken)
        {
            CaptureModuleOrderSnapshot(StartupOrderSnapshotKey, FrameworkComposition.GetCoreModuleOrder());
            var composition = _instance?._composition;
            if (composition == null)
            {
                await Game.InitializeModuleAsync(static () => new DiagnosticsModule(), cancellationToken);
                await Game.InitializeModuleAsync(static () => new DataModule(), cancellationToken);
                await Game.InitializeModuleAsync(static () => new PlatformModule(), cancellationToken);
                return;
            }

            await composition.InitializeCoreAsync(cancellationToken);
        }

        private async UniTask InitializeResourceAsync(CancellationToken cancellationToken)
        {
            SetStage("Initializing Resource");
            if (ResourceSettings == null)
            {
                return;
            }

            await _composition.InitializeResourceAsync(ResourceSettings, PrepareResourcesOnStartup, cancellationToken);
        }

        private async UniTask InitializeFeatureModulesAsync(CancellationToken cancellationToken)
        {
            CaptureModuleOrderSnapshot(StartupOrderSnapshotKey, FrameworkComposition.GetFeatureModuleOrder(InitializeUI, InitializeSceneModule, InitializeProcedureModule), append: true);
            await _composition.InitializeFeaturesAsync(OverrideLanguage, InitializeUI, InitializeSceneModule, InitializeProcedureModule, cancellationToken);
        }

        private async UniTask ExecuteStartupTasksAsync(CancellationToken cancellationToken)
        {
            SetStage("Executing Startup Tasks");
            var startupTasks = StartupTasks;
            if (startupTasks == null || startupTasks.Length == 0)
            {
                return;
            }

            for (var i = 0; i < startupTasks.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var behaviour = startupTasks[i];
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour is not IStartupTask task)
                {
                    Game.Diagnostics.LogWarning($"Startup task '{behaviour.GetType().FullName}' does not implement IStartupTask.", nameof(Startup));
                    continue;
                }

                SetStage($"Executing {behaviour.GetType().Name}");
                await task.ExecuteAsync(this, cancellationToken);
            }
        }

        private async UniTask EnterInitialFlowAsync(CancellationToken cancellationToken)
        {
            SetStage("Entering Initial Flow");
            if (AutoEnterInitialProcedure && !string.IsNullOrWhiteSpace(InitialProcedure))
            {
                var procedure = _composition.RequireReady(_composition.Procedure, nameof(ProcedureModule));
                if (!procedure.HasState(InitialProcedure))
                {
                    throw new FrameworkException(FrameworkError.Create("StartupInitialProcedureMissing", $"Initial procedure '{InitialProcedure}' is not registered.", FrameworkFailureCategory.Configuration, context: InitialProcedure, stage: FrameworkOperationStage.Validating));
                }

                await procedure.ChangeStateFromStartupAsync(InitialProcedure, nameof(Startup), cancellationToken: cancellationToken);
            }

            if (!AutoEnterInitialScene || string.IsNullOrWhiteSpace(InitialScene))
            {
                return;
            }

            if (IsCurrentScene(InitialScene))
            {
                return;
            }

            var scene = _composition.RequireReady(_composition.Scene, nameof(SceneModule));
            await scene.LoadAsync(InitialScene, InitialSceneLoadMode, true, cancellationToken);
        }

        private void OnGUI()
        {
            if (!ShowOverlay)
            {
                return;
            }

            if (!_isRunning && !_hasFailed && (HideOverlayOnComplete || !_isCompleted))
            {
                return;
            }

            var area = new Rect(16f, 16f, OverlayWidth, OverlayHeight);
            GUI.Box(area, GUIContent.none);

            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 12f, area.width - 24f, area.height - 24f));
            GUILayout.Label(OverlayTitle);
            GUILayout.Space(8f);
            GUILayout.Label($"State: {ResolveOverlayState()}");
            GUILayout.Label($"Stage: {_currentStage}");

            if (_lastError != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Error: {_lastError.Code}");
                GUILayout.Label(_lastError.Message);
            }

            GUILayout.EndArea();
        }

        private static bool IsCurrentScene(string sceneNameOrPath)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return false;
            }

            return string.Equals(scene.name, sceneNameOrPath, StringComparison.Ordinal)
                || string.Equals(scene.path, sceneNameOrPath, StringComparison.Ordinal);
        }

        private void ValidateInitialSceneDependencies()
        {
            SetStage("Validating Startup Scene");
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return;
            }

            var invalidDependencies = CollectInvalidFrameworkSceneDependencies(scene);
            if (invalidDependencies.Count == 0)
            {
                return;
            }

            var dependencyList = string.Join(", ", invalidDependencies);
            if (Game.HasModule<DiagnosticsModule>())
            {
                Game.Diagnostics.CaptureSnapshot("Startup.SceneDependencies", dependencyList);
            }

            throw new FrameworkException(FrameworkError.Create(
                "StartupSceneDependencyInvalid",
                $"Initial scene '{scene.name}' should only depend on Startup, but found framework scene components: {dependencyList}.",
                FrameworkFailureCategory.Configuration,
                context: scene.path,
                stage: FrameworkOperationStage.Validating));
        }

        private List<string> CollectInvalidFrameworkSceneDependencies(Scene scene)
        {
            var results = new List<string>();
            var rootObjects = scene.GetRootGameObjects();
            for (var i = 0; i < rootObjects.Length; i++)
            {
                var behaviours = rootObjects[i].GetComponentsInChildren<MonoBehaviour>(true);
                for (var j = 0; j < behaviours.Length; j++)
                {
                    var behaviour = behaviours[j];
                    if (behaviour == null || ReferenceEquals(behaviour, this))
                    {
                        continue;
                    }

                    var type = behaviour.GetType();
                    if (!IsFrameworkSceneDependency(type))
                    {
                        continue;
                    }

                    results.Add($"{type.FullName}@{GetHierarchyPath(behaviour.transform)}");
                }
            }

            return results;
        }

        private static bool IsFrameworkSceneDependency(Type type)
        {
            if (type == null)
            {
                return false;
            }

            var fullName = type.FullName;
            return !string.IsNullOrWhiteSpace(fullName)
                && fullName.StartsWith("GameDeveloperKit.Runtime.", StringComparison.Ordinal)
                && fullName != typeof(Startup).FullName;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var path = transform.name;
            var current = transform.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }

        private void SetStage(string stage)
        {
            _currentStage = string.IsNullOrWhiteSpace(stage) ? "Unknown" : stage;
            StageChanged?.Invoke(_currentStage);
            if (Game.HasModule<DiagnosticsModule>())
            {
                Game.Diagnostics.CaptureStage("Startup", _currentStage);
            }
        }

        private static void CaptureModuleOrderSnapshot(string key, IReadOnlyList<string> moduleNames, bool append = false)
        {
            if (moduleNames == null || moduleNames.Count == 0 || !Game.HasModule<DiagnosticsModule>())
            {
                return;
            }

            var text = string.Join(" -> ", moduleNames);
            if (append && Game.Diagnostics.TryGetSnapshot(key, out var existing) && !string.IsNullOrWhiteSpace(existing))
            {
                text = $"{existing} -> {text}";
            }

            Game.Diagnostics.CaptureSnapshot(key, text);
        }

        private static IReadOnlyList<string> GetShutdownModuleOrder()
        {
            var results = new List<string>();
            var modules = Game.AllModules;
            if (modules == null)
            {
                return results;
            }

            foreach (var moduleName in FrameworkComposition.GetFeatureModuleOrder(true, true, true).Reverse())
            {
                if (HasModuleByName(modules, moduleName))
                {
                    results.Add(moduleName);
                }
            }

            var sharedModules = new[]
            {
                nameof(DownloadModule),
                nameof(NetworkModule),
                nameof(EventModule),
                nameof(CommandModule),
                nameof(SchedulerModule),
                nameof(PoolModule),
                nameof(PlatformModule),
                nameof(DataModule),
                nameof(DiagnosticsModule)
            };

            for (var i = 0; i < sharedModules.Length; i++)
            {
                if (HasModuleByName(modules, sharedModules[i]))
                {
                    results.Add(sharedModules[i]);
                }
            }

            return results;
        }

        private static bool HasModuleByName(IReadOnlyCollection<IGameFrameworkModule> modules, string moduleName)
        {
            foreach (var module in modules)
            {
                if (string.Equals(module?.GetType().Name, moduleName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private string ResolveOverlayState()
        {
            if (_hasFailed)
            {
                return "Failed";
            }

            if (_isCompleted)
            {
                return "Completed";
            }

            return _isRunning ? "Running" : "Idle";
        }

        private void HandleStartupException(Exception exception)
        {
            _hasFailed = true;
            _lastError = exception is FrameworkException frameworkException
                ? frameworkException.Error
                : FrameworkError.FromException("StartupFailed", exception, FrameworkFailureCategory.Startup, false, _currentStage);
            SetStage("Failed");

            if (Game.HasModule<DiagnosticsModule>())
            {
                Game.Diagnostics.LogError($"Startup failed: {exception.Message}", exception.GetType().FullName);
                Game.Diagnostics.CaptureSnapshot("Startup.Error", _lastError.Code);
                Game.Diagnostics.CaptureSnapshot("Startup.ErrorCode", _lastError.Code);
                Game.Diagnostics.CaptureSnapshot("Startup.ErrorMessage", _lastError.Message);
            }
            else
            {
                Debug.LogError($"Startup failed: {exception}");
            }
        }

        private void OnApplicationQuit()
        {
            if (_instance == this && Game.HasModule<DiagnosticsModule>())
            {
                CaptureModuleOrderSnapshot(ShutdownOrderSnapshotKey, GetShutdownModuleOrder());
            }
        }

        private bool PrepareResourcesOnStartup => _useStructuredConfiguration ? _configuration.Modules.PrepareResourcesOnStartup : _prepareResourcesOnStartup;

        private bool InitializeUI => _useStructuredConfiguration ? _configuration.Modules.InitializeUI : _initializeUI;

        private bool InitializeSceneModule => _useStructuredConfiguration ? _configuration.Modules.InitializeSceneModule : _initializeSceneModule;

        private bool InitializeProcedureModule => _useStructuredConfiguration ? _configuration.Modules.InitializeProcedureModule : _initializeProcedureModule;

        private bool PersistAcrossScenes => _useStructuredConfiguration ? _configuration.PersistAcrossScenes : _persistAcrossScenes;

        private bool ShowOverlay => _useStructuredConfiguration ? _configuration.Overlay.ShowOverlay : _showOverlay;

        private bool HideOverlayOnComplete => _useStructuredConfiguration ? _configuration.Overlay.HideOverlayOnComplete : _hideOverlayOnComplete;

        private string OverlayTitle => _useStructuredConfiguration ? _configuration.Overlay.OverlayTitle : _overlayTitle;

        private string OverrideLanguage => _useStructuredConfiguration ? _configuration.Modules.OverrideLanguage : _overrideLanguage;

        private bool AutoEnterInitialScene => _useStructuredConfiguration ? _configuration.InitialFlow.AutoEnterInitialScene : !string.IsNullOrWhiteSpace(_initialScene);

        private string InitialScene => _useStructuredConfiguration ? _configuration.InitialFlow.InitialScene : _initialScene;

        private LoadSceneMode InitialSceneLoadMode => _useStructuredConfiguration ? _configuration.InitialFlow.InitialSceneLoadMode : _initialSceneLoadMode;

        private bool AutoEnterInitialProcedure => _useStructuredConfiguration ? _configuration.InitialFlow.AutoEnterInitialProcedure : !string.IsNullOrWhiteSpace(_initialProcedure);

        private string InitialProcedure => _useStructuredConfiguration ? _configuration.InitialFlow.InitialProcedure : _initialProcedure;

        private MonoBehaviour[] StartupTasks => _useStructuredConfiguration ? _configuration.StartupTasks : _startupTasks;

        private void EnsureConfiguration()
        {
            _configuration ??= new StartupConfiguration();
            _configuration.Modules ??= new StartupModuleConfiguration();
            _configuration.InitialFlow ??= new StartupInitialFlowConfiguration();
            _configuration.Overlay ??= new StartupOverlayConfiguration();
            _configuration.StartupTasks ??= Array.Empty<MonoBehaviour>();
        }
    }
}

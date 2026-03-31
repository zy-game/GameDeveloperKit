using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class Startup : MonoBehaviour
    {
        private const string FrameworkInitializeProcedureName = GameFrameworkInitializeProcedure.StateName;

        private static Startup _instance;

        [SerializeField]
        private GameFrameworkConfiguration _configuration;

        private bool _isRunning;
        private bool _isCompleted;
        private bool _hasFailed;
        private string _currentStage = "Idle";
        private GameFrameworkException _lastError;
        public GameFrameworkConfiguration Configuration => _configuration;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _configuration ??= GameFrameworkConfiguration.CreateRuntimeDefault();
            DontDestroyOnLoad(gameObject);
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

            try
            {
                await InitializeCoreModulesAsync(cancellationToken);
                await InitializeResourceAsync(cancellationToken);
                await InitializeFeatureModulesAsync(cancellationToken);
                await EnterFrameworkInitializeProcedureAsync(cancellationToken);

                _isCompleted = true;
                SetStage("Completed");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetStage("Cancelled");
            }
            catch (Exception exception)
            {
                HandleStartupException(exception);
            }
            finally
            {
                _isRunning = false;
            }
        }

        private static async UniTask InitializeCoreModulesAsync(CancellationToken cancellationToken)
        {
            await Game.InitializeModuleAsync(static () => new DiagnosticsModule(), cancellationToken);
            await Game.InitializeModuleAsync(static () => new DataModule(), cancellationToken);
            await Game.InitializeModuleAsync(static () => new PlatformModule(), cancellationToken);
        }

        private async UniTask InitializeResourceAsync(CancellationToken cancellationToken)
        {
            SetStage("Initializing Resource");
            if (string.IsNullOrWhiteSpace(_configuration.DefaultResourcePackageName))
            {
                return;
            }

            var resource = Game.Resource;
            resource.Initialize(_configuration.ResourcePlayMode, _configuration.DefaultResourcePackageName, _configuration.GatewayServerUrl);
            await resource.InitializeAllPackagesAsync(cancellationToken);
        }

        private static async UniTask InitializeFeatureModulesAsync(CancellationToken cancellationToken)
        {
            await Game.InitializeModuleAsync(static () => new LocalizationModule(), cancellationToken);
            await Game.InitializeModuleAsync(static () => new AudioModule(), cancellationToken);
            await Game.InitializeModuleAsync(static () => new InputModule(), cancellationToken);
            await Game.InitializeModuleAsync(static () => new UIModule(), cancellationToken);
            await Game.InitializeModuleAsync(static () => new SceneModule(), cancellationToken);

            var procedure = await Game.InitializeModuleAsync(static () => new ProcedureModule(), cancellationToken);
            if (!procedure.HasState(GameFrameworkInitializeProcedure.StateName))
            {
                procedure.RegisterState(new GameFrameworkInitializeProcedure());
            }
        }

        private static async UniTask EnterFrameworkInitializeProcedureAsync(CancellationToken cancellationToken)
        {
            var procedure = Game.Procedure;
            Game.EnsureModuleReady<ProcedureModule>();

            if (!procedure.HasState(FrameworkInitializeProcedureName))
            {
                throw GameFrameworkException.Create(
                    "FrameworkInitializeProcedureMissing",
                    $"Procedure '{FrameworkInitializeProcedureName}' is not registered.",
                    "Configuration",
                    context: FrameworkInitializeProcedureName,
                    stage: "Validating");
            }

            await procedure.ChangeStateFromStartupAsync(
                FrameworkInitializeProcedureName,
                nameof(Startup),
                userData: null,
                cancellationToken: cancellationToken);
        }

        private void SetStage(string stage)
        {
            _currentStage = string.IsNullOrWhiteSpace(stage) ? "Unknown" : stage;
            if (Game.HasModule<DiagnosticsModule>())
            {
                Game.Diagnostics.CaptureStage("Startup", _currentStage);
            }
        }

        private void HandleStartupException(Exception exception)
        {
            _hasFailed = true;
            _lastError = exception is GameFrameworkException frameworkException
                ? frameworkException
                : GameFrameworkException.FromException("StartupFailed", exception, "Startup", false, _currentStage);
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
    }
}




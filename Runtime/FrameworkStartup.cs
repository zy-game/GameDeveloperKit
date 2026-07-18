using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Procedure;
using UnityEngine;

namespace GameDeveloperKit
{
    /// <summary>
    /// Scene-mounted framework startup entry.
    /// </summary>
    public sealed class FrameworkStartup : MonoBehaviour
    {
        [SerializeField]
        private string m_TargetProcedureTypeName;

        [SerializeField]
        private UnityEngine.Object m_TargetUserData;

        [SerializeField]
        private FrameworkStartupModuleOptions m_Modules = new FrameworkStartupModuleOptions();

        [SerializeField]
        private bool m_ShutdownAppOnDestroy;

        private UniTaskCompletionSource m_StartupCompletion;
        private readonly CancellationTokenSource m_DestroyCancellation = new CancellationTokenSource();

        /// <summary>
        /// Whether a startup pass is currently running.
        /// </summary>
        public bool IsRunning => m_StartupCompletion != null;

        /// <summary>
        /// Whether startup has completed successfully.
        /// </summary>
        public bool HasStarted { get; private set; }

        /// <summary>
        /// Last startup error.
        /// </summary>
        public Exception LastError { get; private set; }

        /// <summary>
        /// Resolved target procedure type.
        /// </summary>
        public Type TargetProcedureType => ResolveTargetProcedureType();

        /// <summary>
        /// Runs framework startup and enters the configured target procedure.
        /// </summary>
        /// <returns>Startup task.</returns>
        public async UniTask StartupAsync()
        {
            var cancellationToken = m_DestroyCancellation.Token;
            cancellationToken.ThrowIfCancellationRequested();
            if (HasStarted)
            {
                return;
            }

            if (m_StartupCompletion != null)
            {
                await m_StartupCompletion.Task;
                if (LastError != null)
                {
                    throw LastError;
                }

                return;
            }

            var completion = new UniTaskCompletionSource();
            m_StartupCompletion = completion;
            LastError = null;
            try
            {
                await App.Initialize();
                cancellationToken.ThrowIfCancellationRequested();
                var targetProcedureType = ResolveTargetProcedureType();
                await PrepareModulesAsync(cancellationToken);
                await App.UI.OpenAsync<LoadingWindow>();
                cancellationToken.ThrowIfCancellationRequested();
                await App.Procedure.ChangeAsync(targetProcedureType, m_TargetUserData);
                cancellationToken.ThrowIfCancellationRequested();
                HasStarted = true;
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                LastError = exception;
                completion.TrySetResult();
                throw;
            }
            finally
            {
                if (ReferenceEquals(m_StartupCompletion, completion))
                {
                    m_StartupCompletion = null;
                }
            }
        }

        private void Start()
        {
            StartupAsync().Forget(LogStartupException);
        }

        private void OnDestroy()
        {
            m_DestroyCancellation.Cancel();
            if (!m_ShutdownAppOnDestroy)
            {
                return;
            }

            ShutdownAfterStartupAsync().Forget(Debug.LogException);
        }

        private async UniTask ShutdownAfterStartupAsync()
        {
            var startupCompletion = m_StartupCompletion;
            if (startupCompletion != null)
            {
                await startupCompletion.Task;
            }

            await App.Shutdown();
        }

        private async UniTask PrepareModulesAsync(CancellationToken cancellationToken)
        {
            var options = m_Modules ?? new FrameworkStartupModuleOptions();
            if (options.InitializeResource)
            {
                await App.Resource.InitializeAsync(options.ResourceSettings);
                cancellationToken.ThrowIfCancellationRequested();
                await App.Resource.PreloadDefaultPackagesAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (options.ResolveConfigModule)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = App.Config;
            }

            if (options.ResolveDataModule)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = App.Data;
            }

            if (options.ResolvePlayableModule)
            {
                cancellationToken.ThrowIfCancellationRequested();
                App.Playable.Audio.ConfigureMixer(options.AudioMixerSettings);
            }
        }

        private Type ResolveTargetProcedureType()
        {
            if (string.IsNullOrWhiteSpace(m_TargetProcedureTypeName))
            {
                throw new GameException("FrameworkStartup target procedure type is not configured.");
            }

            var procedureType = ProcedureRegistry.Resolve(m_TargetProcedureTypeName);

            if (!typeof(ProcedureBase).IsAssignableFrom(procedureType))
            {
                throw new GameException($"FrameworkStartup target procedure type '{procedureType.FullName}' must inherit ProcedureBase.");
            }

            if (procedureType.IsAbstract || procedureType.ContainsGenericParameters)
            {
                throw new GameException($"FrameworkStartup target procedure type '{procedureType.FullName}' cannot be created.");
            }

            return procedureType;
        }

        private void LogStartupException(Exception exception)
        {
            LastError = exception;
            if (exception is OperationCanceledException && m_DestroyCancellation.IsCancellationRequested)
            {
                return;
            }

            Debug.LogException(exception);
        }
    }
}

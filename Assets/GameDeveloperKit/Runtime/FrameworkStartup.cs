using System;
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
            if (HasStarted)
            {
                return;
            }

            if (m_StartupCompletion != null)
            {
                await m_StartupCompletion.Task;
                return;
            }

            var completion = new UniTaskCompletionSource();
            m_StartupCompletion = completion;
            LastError = null;
            try
            {
                await App.Startup();
                var targetProcedureType = ResolveTargetProcedureType();
                await PrepareModulesAsync();
                await App.Procedure.ChangeAsync(targetProcedureType, m_TargetUserData);
                HasStarted = true;
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                LastError = exception;
                completion.TrySetException(exception);
                completion.Task.Forget(_ => { });
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
            if (!m_ShutdownAppOnDestroy)
            {
                return;
            }

            App.Shutdown().Forget(Debug.LogException);
        }

        private async UniTask PrepareModulesAsync()
        {
            var options = m_Modules ?? new FrameworkStartupModuleOptions();
            if (options.InitializeResource)
            {
                await App.Resource.InitializeAsync(options.ResourceSettings);
            }

            if (options.ResolveConfigModule)
            {
                _ = App.Config;
            }

            if (options.ResolveDataModule)
            {
                _ = App.Data;
            }

            if (options.ResolveSoundModule)
            {
                App.Sound.ConfigureMixer(options.SoundMixerSettings);
            }
        }

        private Type ResolveTargetProcedureType()
        {
            if (string.IsNullOrWhiteSpace(m_TargetProcedureTypeName))
            {
                throw new GameException("FrameworkStartup target procedure type is not configured.");
            }

            var procedureType = Type.GetType(m_TargetProcedureTypeName);
            if (procedureType == null)
            {
                throw new GameException($"FrameworkStartup target procedure type '{m_TargetProcedureTypeName}' cannot be resolved.");
            }

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
            Debug.LogException(exception);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 全局顶层流程状态机模块。
    /// </summary>
    public sealed class ProcedureModule : GameModuleBase
    {
        internal const string RootName = "GameDeveloperKit.ProcedureRoot";

        private readonly Dictionary<Type, ProcedureBase> m_Procedures = new Dictionary<Type, ProcedureBase>();
        private GameObject m_Root;
        private ProcedureRuntimeDriver m_Driver;

        /// <summary>
        /// 流程变化事件。
        /// </summary>
        public event Action<ProcedureChangedEventArgs> ProcedureChanged;

        /// <summary>
        /// 当前流程。
        /// </summary>
        public ProcedureBase Current { get; private set; }

        /// <summary>
        /// 当前流程类型。
        /// </summary>
        public Type CurrentType => Current?.GetType();

        /// <summary>
        /// 当前是否正在切换流程。
        /// </summary>
        public bool IsChanging { get; private set; }

        /// <summary>
        /// 启动流程模块。
        /// </summary>
        /// <returns>模块启动任务。</returns>
        public override UniTask Startup()
        {
            if (m_Root != null)
            {
                return UniTask.CompletedTask;
            }

            m_Root = new GameObject(RootName);
            Object.DontDestroyOnLoad(m_Root);
            m_Driver = m_Root.AddComponent<ProcedureRuntimeDriver>();
            m_Driver.Initialize(this);
            Current = null;
            IsChanging = false;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 关闭流程模块。
        /// </summary>
        /// <returns>模块关闭任务。</returns>
        public override async UniTask Shutdown()
        {
            Exception firstException = null;
            IsChanging = true;
            try
            {
                if (Current != null)
                {
                    try
                    {
                        await Current.OnLeaveAsync(null, null);
                    }
                    catch (Exception exception)
                    {
                        firstException = exception;
                    }
                    finally
                    {
                        Current = null;
                    }
                }

                firstException = ReleaseProcedures(firstException);
                ProcedureChanged = null;
                m_Driver = null;
                DestroyGameObject(m_Root);
                m_Root = null;
            }
            finally
            {
                IsChanging = false;
            }

            if (firstException != null)
            {
                ExceptionDispatchInfo.Capture(firstException).Throw();
            }
        }

        /// <summary>
        /// 注册流程实例。
        /// </summary>
        /// <param name="procedure">流程实例。</param>
        public void RegisterProcedure(ProcedureBase procedure)
        {
            if (procedure == null)
            {
                throw new ArgumentNullException(nameof(procedure));
            }

            var procedureType = procedure.GetType();
            ValidateProcedureType(procedureType);
            if (m_Procedures.ContainsKey(procedureType))
            {
                throw new GameException($"Procedure '{procedureType.Name}' has already been registered.");
            }

            procedure.OnInitializeAsync().GetAwaiter().GetResult();
            m_Procedures.Add(procedureType, procedure);
        }

        /// <summary>
        /// 检查流程是否已注册。
        /// </summary>
        /// <typeparam name="TProcedure">流程类型。</typeparam>
        /// <returns>如果流程已注册，则返回 true；否则返回 false。</returns>
        public bool HasProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            return m_Procedures.ContainsKey(typeof(TProcedure));
        }

        /// <summary>
        /// 获取已注册流程。
        /// </summary>
        /// <typeparam name="TProcedure">流程类型。</typeparam>
        /// <param name="procedure">流程实例。</param>
        /// <returns>如果流程已注册，则返回 true；否则返回 false。</returns>
        public bool TryGetProcedure<TProcedure>(out TProcedure procedure) where TProcedure : ProcedureBase
        {
            if (m_Procedures.TryGetValue(typeof(TProcedure), out var value) && value is TProcedure typedProcedure)
            {
                procedure = typedProcedure;
                return true;
            }

            procedure = null;
            return false;
        }

        /// <summary>
        /// 切换流程。
        /// </summary>
        /// <typeparam name="TProcedure">目标流程类型。</typeparam>
        /// <param name="userData">切换参数。</param>
        /// <returns>切换任务。</returns>
        public UniTask ChangeAsync<TProcedure>(object userData = null) where TProcedure : ProcedureBase
        {
            return ChangeAsync(typeof(TProcedure), userData);
        }

        /// <summary>
        /// 切换流程。
        /// </summary>
        /// <param name="procedureType">目标流程类型。</param>
        /// <param name="userData">切换参数。</param>
        /// <returns>切换任务。</returns>
        public async UniTask ChangeAsync(Type procedureType, object userData = null)
        {
            ValidateProcedureType(procedureType);
            if (IsChanging)
            {
                throw new GameException("ProcedureModule is already changing procedure.");
            }

            IsChanging = true;
            try
            {
                var next = await GetOrCreateProcedureAsync(procedureType);
                if (ReferenceEquals(Current, next))
                {
                    return;
                }

                var previous = Current;
                if (previous != null)
                {
                    await previous.OnLeaveAsync(next, userData);
                    Current = null;
                }

                await next.OnEnterAsync(previous, userData);
                Current = next;
                ProcedureChanged?.Invoke(new ProcedureChangedEventArgs(previous, next, userData));
            }
            finally
            {
                IsChanging = false;
            }
        }

        private async UniTask<ProcedureBase> GetOrCreateProcedureAsync(Type procedureType)
        {
            ValidateProcedureType(procedureType);
            if (m_Procedures.TryGetValue(procedureType, out var procedure))
            {
                return procedure;
            }

            procedure = CreateProcedure(procedureType);
            try
            {
                await procedure.OnInitializeAsync();
            }
            catch
            {
                procedure.Release();
                throw;
            }

            m_Procedures.Add(procedureType, procedure);
            return procedure;
        }

        private static ProcedureBase CreateProcedure(Type procedureType)
        {
            try
            {
                return (ProcedureBase)Activator.CreateInstance(procedureType, true);
            }
            catch (Exception exception)
            {
                throw new GameException($"Procedure '{procedureType.FullName}' cannot be created. Register an instance with RegisterProcedure instead.", exception);
            }
        }

        private static void ValidateProcedureType(Type procedureType)
        {
            if (procedureType == null)
            {
                throw new ArgumentNullException(nameof(procedureType));
            }

            if (!typeof(ProcedureBase).IsAssignableFrom(procedureType))
            {
                throw new GameException($"Procedure type '{procedureType.FullName}' must inherit ProcedureBase.");
            }

            if (procedureType.IsAbstract || procedureType.ContainsGenericParameters)
            {
                throw new GameException($"Procedure type '{procedureType.FullName}' cannot be created.");
            }
        }

        private Exception ReleaseProcedures(Exception firstException)
        {
            foreach (var procedure in m_Procedures.Values)
            {
                try
                {
                    procedure.Release();
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }

            m_Procedures.Clear();
            return firstException;
        }

        private void UpdateCurrent(float deltaTime, float unscaledDeltaTime)
        {
            if (Current == null || IsChanging)
            {
                return;
            }

            Current.OnUpdate(deltaTime, unscaledDeltaTime);
        }

        private static void DestroyGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(gameObject);
            }
            else
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class ProcedureRuntimeDriver : MonoBehaviour
        {
            private ProcedureModule m_Module;

            public void Initialize(ProcedureModule module)
            {
                m_Module = module;
            }

            private void Update()
            {
                m_Module?.UpdateCurrent(Time.deltaTime, Time.unscaledDeltaTime);
            }
        }
    }
}

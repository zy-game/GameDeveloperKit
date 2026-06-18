using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Logger;
using GameDeveloperKit.Timer;
using UnityEngine;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 全局顶层流程状态机模块。
    /// </summary>
    [ModuleDependency(typeof(TimerModule))]
    public sealed partial class ProcedureModule : GameModuleBase
    {
        /// <summary>
        /// 定义 Root Name 常量。
        /// </summary>
        internal const string RootName = "GameDeveloperKit.ProcedureRoot";

        /// <summary>
        /// 存储 Procedures。
        /// </summary>
        private readonly Dictionary<Type, ProcedureBase> m_Procedures = new Dictionary<Type, ProcedureBase>();
        /// <summary>
        /// 存储 Update Handle。
        /// </summary>
        private ProcedureUpdateHandle m_UpdateHandle;
        /// <summary>
        /// 存储 Profile Handle。
        /// </summary>
        private readonly ProcedureProfileHandle m_ProfileHandle;
        /// <summary>
        /// 记录 Started 状态。
        /// </summary>
        private bool m_Started;
        /// <summary>
        /// 存储 Pending Change Request。
        /// </summary>
        private ProcedureChangeRequest m_PendingChange;

        /// <summary>
        /// 初始化 Procedure Module。
        /// </summary>
        public ProcedureModule()
        {
            m_ProfileHandle = new ProcedureProfileHandle(this);
        }

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
        /// 是否存在待处理流程切换请求。
        /// </summary>
        public bool HasPendingChange => m_PendingChange.IsValid;
        /// <summary>
        /// 待处理流程切换类型。
        /// </summary>
        public Type PendingChangeType => m_PendingChange.ProcedureType;

        /// <summary>
        /// 启动流程模块。
        /// </summary>
        public override void Startup()
        {
            if (m_Started)
            {
                return;
            }

            m_Started = true;
            Current = null;
            IsChanging = false;
            ClearPendingChange();
            RegisterUpdateHandle();
            TryRegisterDebugProfile();
        }

        /// <summary>
        /// 关闭流程模块。
        /// </summary>
        public override void Shutdown()
        {
            Exception firstException = null;
            UnregisterUpdateHandle();
            IsChanging = true;
            try
            {
                if (Current != null)
                {
                    try
                    {
                        Current.OnLeaveAsync(null, null).GetAwaiter().GetResult();
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
                ClearPendingChange();
                m_Started = false;
            }
            finally
            {
                TryUnregisterDebugProfile();
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
        /// 请求在当前流程切换完成后继续切换到目标流程。
        /// </summary>
        /// <typeparam name="TProcedure">目标流程类型。</typeparam>
        /// <param name="userData">切换参数。</param>
        public void RequestChange<TProcedure>(object userData = null) where TProcedure : ProcedureBase
        {
            RequestChange(typeof(TProcedure), userData);
        }

        /// <summary>
        /// 请求在当前流程切换完成后继续切换到目标流程。
        /// </summary>
        /// <param name="procedureType">目标流程类型。</param>
        /// <param name="userData">切换参数。</param>
        public void RequestChange(Type procedureType, object userData = null)
        {
            ValidateProcedureType(procedureType);
            if (!IsChanging)
            {
                throw new GameException("Procedure change can only be requested during a procedure change.");
            }

            m_PendingChange = new ProcedureChangeRequest(procedureType, userData);
        }

        /// <summary>
        /// 清空待处理流程切换请求。
        /// </summary>
        public void ClearPendingChange()
        {
            m_PendingChange = default;
        }

        /// <summary>
        /// 切换流程。
        /// </summary>
        /// <param name="procedureType">目标流程类型。</param>
        /// <param name="userData">切换参数。</param>
        /// <returns>切换任务。</returns>
        public async UniTask ChangeAsync(Type procedureType, object userData = null)
        {
            await ChangeOnceAsync(procedureType, userData);
            await DrainPendingChangeAsync();
        }

        /// <summary>
        /// 执行一次流程切换。
        /// </summary>
        /// <param name="procedureType">目标流程类型。</param>
        /// <param name="userData">切换参数。</param>
        /// <returns>切换任务。</returns>
        private async UniTask ChangeOnceAsync(Type procedureType, object userData)
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
            }
            catch
            {
                ClearPendingChange();
                throw;
            }
            finally
            {
                IsChanging = false;
            }
        }

        /// <summary>
        /// 执行待处理流程切换。
        /// </summary>
        /// <returns>切换任务。</returns>
        private async UniTask DrainPendingChangeAsync()
        {
            while (HasPendingChange)
            {
                var request = m_PendingChange;
                ClearPendingChange();
                await ChangeOnceAsync(request.ProcedureType, request.UserData);
            }
        }

        /// <summary>
        /// 获取 Or Create Procedure Async。
        /// </summary>
        /// <param name="procedureType">procedure Type 参数。</param>
        /// <returns>操作完成任务。</returns>
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

        /// <summary>
        /// 创建 Procedure。
        /// </summary>
        /// <param name="procedureType">procedure Type 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 校验 Procedure Type。
        /// </summary>
        /// <param name="procedureType">procedure Type 参数。</param>
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

        /// <summary>
        /// 执行 Release Procedures。
        /// </summary>
        /// <param name="firstException">first Exception 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 Update Current。
        /// </summary>
        /// <param name="deltaTime">delta Time 参数。</param>
        /// <param name="unscaledDeltaTime">unscaled Delta Time 参数。</param>
        private void UpdateCurrent(float deltaTime, float unscaledDeltaTime)
        {
            if (Current == null || IsChanging)
            {
                return;
            }

            Current.OnUpdate(deltaTime, unscaledDeltaTime);
        }

        /// <summary>
        /// 注册 Update Handle。
        /// </summary>
        private void RegisterUpdateHandle()
        {
            if (m_UpdateHandle != null &&
                !m_UpdateHandle.IsCancelled &&
                !m_UpdateHandle.IsCompleted &&
                m_UpdateHandle.Module != null)
            {
                return;
            }

            if (!App.TryGetRegistered<TimerModule>(out var timer))
            {
                return;
            }

            m_UpdateHandle = timer.Register(new ProcedureUpdateHandle(this), this, "ProcedureModule.Update");
        }

        /// <summary>
        /// 注销 Update Handle。
        /// </summary>
        private void UnregisterUpdateHandle()
        {
            if (m_UpdateHandle == null)
            {
                return;
            }

            m_UpdateHandle.Cancel();
            m_UpdateHandle = null;
        }

        /// <summary>
        /// 注册 Debug Profile。
        /// </summary>
        /// <param name="debug">debug 参数。</param>
        internal void RegisterDebugProfile(DebugModule debug)
        {
            if (debug == null)
            {
                throw new ArgumentNullException(nameof(debug));
            }

            debug.RegisterProfile(m_ProfileHandle);
        }

        /// <summary>
        /// 注销 Debug Profile。
        /// </summary>
        /// <param name="debug">debug 参数。</param>
        internal void UnregisterDebugProfile(DebugModule debug)
        {
            if (debug == null)
            {
                throw new ArgumentNullException(nameof(debug));
            }

            debug.UnregisterProfile(m_ProfileHandle);
        }

        /// <summary>
        /// 尝试注册 Debug Profile。
        /// </summary>
        private void TryRegisterDebugProfile()
        {
            if (App.TryGetRegistered<DebugModule>(out var debug))
            {
                RegisterDebugProfile(debug);
            }
        }

        /// <summary>
        /// 尝试注销 Debug Profile。
        /// </summary>
        private void TryUnregisterDebugProfile()
        {
            if (App.TryGetRegistered<DebugModule>(out var debug))
            {
                UnregisterDebugProfile(debug);
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Debugger;
using GameDeveloperKit.Timer;
using UnityEngine;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 全局顶层流程状态机模块。
    /// </summary>
    [ModuleDependency(typeof(TimerModule))]
    public sealed partial class ProcedureModule : GameModuleBase, IAsyncShutdownParticipant
    {
        internal const string RootName = "GameDeveloperKit.ProcedureRoot";

        private readonly Dictionary<Type, ProcedureBase> m_Procedures = new Dictionary<Type, ProcedureBase>();
        private ProcedureUpdateHandle m_UpdateHandle;
        private readonly ProcedureProfileHandle m_ProfileHandle;
        private bool m_Started;
        private bool m_IsPreparingShutdown;
        private bool m_TeardownPrepared;
        private ProcedureChangeRequest m_PendingChange;
        private ProcedureAsyncCompletion m_ChangeCompletion;
        private ProcedureAsyncCompletion m_PrepareCompletion;

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
            m_IsPreparingShutdown = false;
            m_TeardownPrepared = false;
            m_ChangeCompletion = null;
            m_PrepareCompletion = null;
            ClearPendingChange();
            RegisterUpdateHandle();
            TryRegisterDebugProfile();
        }

        /// <summary>
        /// 关闭流程模块。
        /// </summary>
        public override void Shutdown()
        {
            if ((Current != null || IsChanging) && !m_TeardownPrepared)
            {
                throw new GameException(
                    "ProcedureModule has active procedure work. Use App.Unregister<ProcedureModule>() or App.Shutdown() so asynchronous teardown can complete.");
            }

            var exceptions = new List<Exception>();
            UnregisterUpdateHandle();
            try
            {
                ReleaseProcedures(exceptions);
                Current = null;
                ClearPendingChange();
                m_Started = false;
            }
            finally
            {
                TryUnregisterDebugProfile();
                IsChanging = false;
                m_IsPreparingShutdown = false;
                m_TeardownPrepared = false;
                m_ChangeCompletion = null;
                m_PrepareCompletion = null;
            }

            if (exceptions.Count > 0)
            {
                var ex = exceptions.Count == 1
                    ? exceptions[0]
                    : new AggregateException($"{exceptions.Count} exceptions during procedure shutdown.", exceptions);
                ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        /// <summary>
        /// 注册流程实例。
        /// </summary>
        /// <param name="procedure">流程实例。</param>
        public void RegisterProcedure(ProcedureBase procedure)
        {
            ThrowIfPreparingShutdown();
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
            ValidateProcedureType(procedureType);
            ThrowIfPreparingShutdown();

            // 允许在流程生命周期（OnEnterAsync/OnLeaveAsync）内调用，
            // 此时记录为待处理请求，由外层 ChangeAsync 在当前切换完成后排空。
            if (IsChanging)
            {
                m_PendingChange = new ProcedureChangeRequest(procedureType, userData);
                return;
            }

            var completion = new ProcedureAsyncCompletion();
            m_ChangeCompletion = completion;
            try
            {
                await ChangeOnceAsync(procedureType, userData);
                await DrainPendingChangeAsync();
                completion.Source.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.Exception = exception;
                completion.Source.TrySetResult();
                throw;
            }
            finally
            {
                if (ReferenceEquals(m_ChangeCompletion, completion))
                {
                    m_ChangeCompletion = null;
                }
            }
        }

        async UniTask IAsyncShutdownParticipant.PrepareShutdownAsync()
        {
            if (m_PrepareCompletion != null)
            {
                var pendingPrepare = m_PrepareCompletion;
                await pendingPrepare.Source.Task;
                if (pendingPrepare.Exception != null)
                {
                    ExceptionDispatchInfo.Capture(pendingPrepare.Exception).Throw();
                }

                return;
            }

            if (m_TeardownPrepared)
            {
                return;
            }

            var prepareCompletion = new ProcedureAsyncCompletion();
            m_PrepareCompletion = prepareCompletion;
            m_IsPreparingShutdown = true;
            UnregisterUpdateHandle();
            ClearPendingChange();

            var exceptions = new List<Exception>();
            try
            {
                var changeCompletion = m_ChangeCompletion;
                if (changeCompletion != null)
                {
                    await changeCompletion.Source.Task;
                    if (changeCompletion.Exception != null)
                    {
                        exceptions.Add(changeCompletion.Exception);
                    }
                }

                ClearPendingChange();
                var current = Current;
                var leaveAlreadyAttempted =
                    current != null &&
                    changeCompletion != null &&
                    ReferenceEquals(changeCompletion.FailedLeaveProcedure, current);
                if (current != null && !leaveAlreadyAttempted)
                {
                    try
                    {
                        await current.OnLeaveAsync(null, null);
                    }
                    catch (Exception exception)
                    {
                        exceptions.Add(exception);
                    }
                    finally
                    {
                        if (ReferenceEquals(Current, current))
                        {
                            Current = null;
                        }
                    }
                }

                m_TeardownPrepared = true;
                if (exceptions.Count > 0)
                {
                    var exception = exceptions.Count == 1
                        ? exceptions[0]
                        : new AggregateException($"{exceptions.Count} exceptions during procedure teardown preparation.", exceptions);
                    prepareCompletion.Exception = exception;
                    prepareCompletion.Source.TrySetResult();
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                prepareCompletion.Source.TrySetResult();
            }
            finally
            {
                if (ReferenceEquals(m_PrepareCompletion, prepareCompletion))
                {
                    m_PrepareCompletion = null;
                }
            }
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
                    try
                    {
                        await previous.OnLeaveAsync(next, userData);
                    }
                    catch
                    {
                        if (m_ChangeCompletion != null)
                        {
                            m_ChangeCompletion.FailedLeaveProcedure = previous;
                        }

                        throw;
                    }

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
                if (m_IsPreparingShutdown)
                {
                    ClearPendingChange();
                    return;
                }

                var request = m_PendingChange;
                ClearPendingChange();
                await ChangeOnceAsync(request.ProcedureType, request.UserData);
            }
        }

        /// <summary>
        /// 获取 Or Create Procedure Async。
        /// </summary>
        /// <param name="procedureType">procedure Type 参数。</param>
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
        private static ProcedureBase CreateProcedure(Type procedureType)
        {
            try
            {
                return ProcedureRegistry.Create(procedureType);
            }
            catch (Exception exception)
            {
                throw new GameException(
                    $"Procedure '{procedureType.FullName}' cannot be created. Register an instance with RegisterProcedure instead.",
                    exception);
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

        private void ThrowIfPreparingShutdown()
        {
            if (m_IsPreparingShutdown)
            {
                throw new GameException("ProcedureModule is shutting down and cannot accept new procedure work.");
            }
        }

        private sealed class ProcedureAsyncCompletion
        {
            public UniTaskCompletionSource Source { get; } = new UniTaskCompletionSource();

            public Exception Exception { get; set; }

            public ProcedureBase FailedLeaveProcedure { get; set; }
        }

        /// <summary>
        /// 执行 Release Procedures。
        /// </summary>
        /// <param name="firstException">first Exception 参数。</param>
        private void ReleaseProcedures(List<Exception> exceptions)
        {
            foreach (var procedure in m_Procedures.Values)
            {
                try
                {
                    procedure.Release();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            m_Procedures.Clear();
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
            base.TryRegisterDebugProfile(m_ProfileHandle);
        }

        /// <summary>
        /// 尝试注销 Debug Profile。
        /// </summary>
        private void TryUnregisterDebugProfile()
        {
            base.TryUnregisterDebugProfile(m_ProfileHandle);
        }

    }
}

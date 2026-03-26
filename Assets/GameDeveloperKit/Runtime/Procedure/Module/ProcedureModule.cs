using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 流程模块，提供流程状态的管理、切换和转换保护功能
    /// </summary>
    public sealed partial class ProcedureModule : IGameFrameworkLifecycleModule
    {
        /// <summary>
        /// 流程状态变更完成事件名称
        /// </summary>
        public const string StateChangedEventName = "GameDeveloperKit.Procedure.StateChanged";

        /// <summary>
        /// 流程状态变更中事件名称
        /// </summary>
        public const string StateChangingEventName = "GameDeveloperKit.Procedure.StateChanging";

        /// <summary>
        /// 流程状态变更被阻止事件名称
        /// </summary>
        public const string StateBlockedEventName = "GameDeveloperKit.Procedure.StateBlocked";

        /// <summary>
        /// 流程状态变更请求事件名称
        /// </summary>
        public const string StateRequestedEventName = "GameDeveloperKit.Procedure.StateRequested";

        private readonly Dictionary<string, IProcedureState> _states = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _stateParents = new(StringComparer.Ordinal);
        private readonly List<IProcedureState> _activeStatePath = new();
        private readonly List<IProcedureTransitionGuard> _guards = new();
        private readonly ProcedureDriver _driver;
        private bool _isChangingState;
        private GameFrameworkModuleStatus _status = GameFrameworkModuleStatus.Created;
        private bool _diagnosticsRegistered;
        private int _transitionCount;
        private int _blockedTransitionCount;
        private string _lastBlockedReason;
        private string _lastStateName;
        private string _lastTransitionSource;

        /// <summary>
        /// 初始化流程模块并创建驱动组件
        /// </summary>
        public ProcedureModule()
        {
            var driverObject = new GameObject("[GameDeveloperKit.Procedure]");
            UnityEngine.Object.DontDestroyOnLoad(driverObject);
            _driver = driverObject.AddComponent<ProcedureDriver>();
            _driver.Initialize(this);
        }

        /// <summary>
        /// 获取当前流程状态
        /// </summary>
        public IProcedureState CurrentState { get; private set; }

        /// <summary>
        /// 获取当前流程状态名称
        /// </summary>
        public string CurrentStateName => CurrentState?.Name;

        /// <summary>
        /// 获取流程状态数量
        /// </summary>
        public int StateCount => _states.Count;

        /// <summary>
        /// 获取活动流程状态路径的只读列表
        /// </summary>
        public IReadOnlyList<IProcedureState> ActiveStatePath => _activeStatePath;

        /// <summary>
        /// 获取活动流程状态路径的文本表示
        /// </summary>
        public string ActiveStatePathText => _activeStatePath.Count == 0
            ? string.Empty
            : string.Join(" > ", _activeStatePath.ConvertAll(static state => state.Name));

        /// <summary>
        /// 获取模块状态
        /// </summary>
        public GameFrameworkModuleStatus Status => _status;

        /// <summary>
        /// 流程状态改变事件
        /// </summary>
        public event Action<IProcedureState, IProcedureState> StateChanged;

        /// <summary>
        /// 流程状态改变中事件
        /// </summary>
        public event Action<IProcedureState, IProcedureState> StateChanging;

        /// <summary>
        /// 流程状态转换被阻止事件
        /// </summary>
        public event Action<IProcedureState, IProcedureState, string> StateBlocked;

        /// <summary>
        /// 流程状态请求事件
        /// </summary>
        public event Action<ProcedureTransitionRequest> StateRequested;

        /// <summary>
        /// 异步初始化流程模块
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterInitialization(nameof(ProcedureModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            try
            {
                RegisterDiagnosticsSnapshotProviders();
                GameFrameworkModuleLifecycleUtility.CompleteInitialization(ref _status);
                return UniTask.CompletedTask;
            }
            catch
            {
                GameFrameworkModuleLifecycleUtility.FailInitialization(ref _status);
                throw;
            }
        }

        /// <summary>
        /// 异步关闭流程模块
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterShutdown(nameof(ProcedureModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 注册流程状态
        /// </summary>
        /// <param name="state">流程状态</param>
        /// <exception cref="ArgumentNullException">流程状态为空</exception>
        /// <exception cref="ArgumentException">流程状态名称为空</exception>
        public void RegisterState(IProcedureState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (string.IsNullOrWhiteSpace(state.Name))
            {
                throw new ArgumentException("Procedure state name can not be empty.", nameof(state));
            }

            _states[state.Name] = state;
            _stateParents.Remove(state.Name);
        }

        /// <summary>
        /// 注册子流程状态
        /// </summary>
        /// <param name="parentStateName">父状态名称</param>
        /// <param name="state">流程状态</param>
        /// <exception cref="ArgumentException">父状态名称为空或未注册</exception>
        public void RegisterSubState(string parentStateName, IProcedureState state)
        {
            if (string.IsNullOrWhiteSpace(parentStateName))
            {
                throw new ArgumentException("Parent state name can not be empty.", nameof(parentStateName));
            }

            if (!_states.ContainsKey(parentStateName))
            {
                throw new InvalidOperationException($"Parent procedure state '{parentStateName}' is not registered.");
            }

            RegisterState(state);
            _stateParents[state.Name] = parentStateName;
        }

        /// <summary>
        /// 注册流程状态
        /// </summary>
        /// <typeparam name="TState">流程状态类型</typeparam>
        public void RegisterState<TState>()
            where TState : class, IProcedureState, new()
        {
            RegisterState(new TState());
        }

        /// <summary>
        /// 检查是否存在指定的流程状态
        /// </summary>
        /// <param name="stateName">流程状态名称</param>
        /// <returns>如果存在返回true，否则返回false</returns>
        public bool HasState(string stateName)
        {
            return !string.IsNullOrWhiteSpace(stateName) && _states.ContainsKey(stateName);
        }

        /// <summary>
        /// 注册流程模板
        /// </summary>
        /// <param name="template">流程模板</param>
        public void RegisterFlowTemplate(ProcedureFlowTemplate template = null)
        {
            template ??= new ProcedureFlowTemplate();

            RegisterState(new StartupProcedureTemplateState(template.StartupStateName, template.LobbyStateName, template.ShowStartupLoading, template.StartupLoadingMessage));
            RegisterState(new LobbyProcedureTemplateState(template.LobbyStateName, template.LobbySceneName, template.LobbyPackageName, template.RememberScenes));
            RegisterState(new BattleProcedureTemplateState(template.BattleStateName, template.BattleSceneName, template.BattlePackageName, template.LobbyStateName, template.RememberScenes));
        }

        /// <summary>
        /// 获取流程状态
        /// </summary>
        /// <typeparam name="TState">流程状态类型</typeparam>
        /// <returns>流程状态实例</returns>
        /// <exception cref="InvalidOperationException">流程状态未注册</exception>
        public TState GetState<TState>()
            where TState : class, IProcedureState
        {
            foreach (var state in _states.Values)
            {
                if (state is TState typedState)
                {
                    return typedState;
                }
            }

            throw new InvalidOperationException($"Procedure state '{typeof(TState).FullName}' is not registered.");
        }

        /// <summary>
        /// 获取流程状态
        /// </summary>
        /// <param name="stateName">流程状态名称</param>
        /// <returns>流程状态实例</returns>
        /// <exception cref="InvalidOperationException">流程状态未注册</exception>
        public IProcedureState GetState(string stateName)
        {
            if (!_states.TryGetValue(stateName, out var state))
            {
                throw new InvalidOperationException($"Procedure state '{stateName}' is not registered.");
            }

            return state;
        }

        /// <summary>
        /// 移除流程状态
        /// </summary>
        /// <param name="stateName">流程状态名称</param>
        /// <returns>如果移除成功返回true，否则返回false</returns>
        /// <exception cref="InvalidOperationException">不能移除当前或活动的流程状态</exception>
        public bool RemoveState(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            if (CurrentState != null && string.Equals(CurrentState.Name, stateName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Can not remove the current procedure state.");
            }

            if (_activeStatePath.Exists(state => string.Equals(state.Name, stateName, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Can not remove an active procedure state.");
            }

            _stateParents.Remove(stateName);
            return _states.Remove(stateName);
        }

        /// <summary>
        /// 注册流程转换保护器
        /// </summary>
        /// <param name="guard">流程转换保护器</param>
        /// <exception cref="ArgumentNullException">保护器为空</exception>
        public void RegisterGuard(IProcedureTransitionGuard guard)
        {
            if (guard == null)
            {
                throw new ArgumentNullException(nameof(guard));
            }

            if (!_guards.Contains(guard))
            {
                _guards.Add(guard);
            }
        }

        /// <summary>
        /// 注销流程转换保护器
        /// </summary>
        /// <param name="guard">流程转换保护器</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool UnregisterGuard(IProcedureTransitionGuard guard)
        {
            return guard != null && _guards.Remove(guard);
        }

        /// <summary>
        /// 清除所有流程状态
        /// </summary>
        /// <exception cref="InvalidOperationException">当前有活动流程状态</exception>
        public void ClearStates()
        {
            if (CurrentState != null)
            {
                throw new InvalidOperationException("Can not clear procedure states while a current state is active.");
            }

            _states.Clear();
        }

        /// <summary>
        /// 异步改变流程状态
        /// </summary>
        /// <typeparam name="TState">流程状态类型</typeparam>
        /// <param name="userData">用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        /// <exception cref="InvalidOperationException">流程状态未注册</exception>
        public UniTask ChangeStateAsync<TState>(object userData = null, CancellationToken cancellationToken = default)
            where TState : class, IProcedureState
        {
            foreach (var state in _states.Values)
            {
                if (state is TState)
                {
                    return ChangeStateAsync(new ProcedureTransitionRequest
                    {
                        StateName = state.Name,
                        UserData = userData
                    }, cancellationToken);
                }
            }

            throw new InvalidOperationException($"Procedure state '{typeof(TState).FullName}' is not registered.");
        }

        /// <summary>
        /// 异步改变流程状态
        /// </summary>
        /// <param name="stateName">流程状态名称</param>
        /// <param name="userData">用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public UniTask ChangeStateAsync(string stateName, object userData = null, CancellationToken cancellationToken = default)
        {
            return ChangeStateAsync(new ProcedureTransitionRequest
            {
                StateName = stateName,
                UserData = userData
            }, cancellationToken);
        }

        /// <summary>
        /// 从启动步骤异步改变流程状态
        /// </summary>
        /// <param name="stateName">流程状态名称</param>
        /// <param name="startupStep">启动步骤</param>
        /// <param name="userData">用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public UniTask ChangeStateFromStartupAsync(string stateName, string startupStep = null, object userData = null, CancellationToken cancellationToken = default)
        {
            return ChangeStateAsync(new ProcedureTransitionRequest
            {
                StateName = stateName,
                UserData = userData,
                Source = ProcedureTransitionSource.Startup,
                Trigger = startupStep
            }, cancellationToken);
        }

        /// <summary>
        /// 从场景异步改变流程状态
        /// </summary>
        /// <param name="stateName">流程状态名称</param>
        /// <param name="sceneName">场景名称</param>
        /// <param name="userData">用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public UniTask ChangeStateFromSceneAsync(string stateName, string sceneName = null, object userData = null, CancellationToken cancellationToken = default)
        {
            return ChangeStateAsync(new ProcedureTransitionRequest
            {
                StateName = stateName,
                UserData = userData,
                Source = ProcedureTransitionSource.Scene,
                Trigger = sceneName
            }, cancellationToken);
        }

        /// <summary>
        /// 从UI异步改变流程状态
        /// </summary>
        /// <param name="stateName">流程状态名称</param>
        /// <param name="windowName">窗口名称</param>
        /// <param name="userData">用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public UniTask ChangeStateFromUIAsync(string stateName, string windowName = null, object userData = null, CancellationToken cancellationToken = default)
        {
            return ChangeStateAsync(new ProcedureTransitionRequest
            {
                StateName = stateName,
                UserData = userData,
                Source = ProcedureTransitionSource.UI,
                Trigger = windowName
            }, cancellationToken);
        }

        /// <summary>
        /// 异步改变流程状态
        /// </summary>
        /// <param name="request">流程转换请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        /// <exception cref="ArgumentNullException">请求为空</exception>
        /// <exception cref="ArgumentException">流程状态名称为空</exception>
        /// <exception cref="InvalidOperationException">流程状态正在改变或未注册</exception>
        public async UniTask ChangeStateAsync(ProcedureTransitionRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.StateName))
            {
                throw new ArgumentException("Procedure state name can not be empty.", nameof(request));
            }

            if (_isChangingState)
            {
                throw new InvalidOperationException("Procedure state is changing.");
            }

            if (!_states.TryGetValue(request.StateName, out var nextState))
            {
                throw new InvalidOperationException($"Procedure state '{request.StateName}' is not registered.");
            }

            var previousState = CurrentState;
            if (ReferenceEquals(previousState, nextState))
            {
                return;
            }

            _lastTransitionSource = request.Source.ToString();
            StateRequested?.Invoke(request);
            if (Game.TryGetModule<EventModule>(out var requestEventModule))
            {
                requestEventModule.Raise(StateRequestedEventName, this, request);
            }

            StateChanging?.Invoke(previousState, nextState);
            if (Game.TryGetModule<EventModule>(out var changingEventModule))
            {
                changingEventModule.Raise(StateChangingEventName, this, previousState, nextState, request.Source, request.Trigger);
            }

            if (!CanTransition(previousState, nextState, request.UserData, out var blockedReason))
            {
                _blockedTransitionCount++;
                _lastBlockedReason = blockedReason;
                StateBlocked?.Invoke(previousState, nextState, blockedReason);

                if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
                {
                    diagnostics.LogWarning($"Blocked transition: {previousState?.Name ?? "<None>"} -> {nextState.Name}. {blockedReason}", nameof(ProcedureModule));
                    diagnostics.CaptureSnapshot("Procedure.LastBlockedReason", blockedReason ?? string.Empty);
                }

                if (Game.TryGetModule<EventModule>(out var blockedEventModule))
                {
                    blockedEventModule.Raise(StateBlockedEventName, this, previousState, nextState, blockedReason, request.Source, request.Trigger);
                }

                return;
            }

            _isChangingState = true;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var previousPath = _activeStatePath.Count > 0 ? new List<IProcedureState>(_activeStatePath) : BuildStatePath(previousState);
                var nextPath = BuildStatePath(nextState);
                var sharedDepth = GetSharedPathDepth(previousPath, nextPath);

                for (var i = previousPath.Count - 1; i >= sharedDepth; i--)
                {
                    await previousPath[i].OnExitAsync(cancellationToken);
                }

                _activeStatePath.Clear();
                for (var i = 0; i < nextPath.Count; i++)
                {
                    _activeStatePath.Add(nextPath[i]);
                }

                CurrentState = nextState;
                for (var i = sharedDepth; i < nextPath.Count; i++)
                {
                    await nextPath[i].OnEnterAsync(i == nextPath.Count - 1 ? request.UserData : null, cancellationToken);
                }
            }
            finally
            {
                _isChangingState = false;
            }

            _transitionCount++;
            _lastStateName = nextState.Name;
            StateChanged?.Invoke(previousState, nextState);
            if (Game.TryGetModule<EventModule>(out var eventModule))
            {
                eventModule.Raise(StateChangedEventName, this, previousState, nextState, request.Source, request.Trigger);
            }

            if (Game.TryGetModule<DiagnosticsModule>(out var changedDiagnostics))
            {
                changedDiagnostics.LogInfo($"Changed state: {previousState?.Name ?? "<None>"} -> {nextState.Name} ({request.Source}:{request.Trigger ?? "Direct"})", nameof(ProcedureModule));
            }
        }

        /// <summary>
        /// 释放流程模块占用的所有资源
        /// </summary>
        public void Dispose()
        {
            RemoveDiagnosticsSnapshotProviders();
            _states.Clear();
            _stateParents.Clear();
            _activeStatePath.Clear();
            _guards.Clear();
            CurrentState = null;
            StateChanged = null;
            StateChanging = null;
            StateBlocked = null;
            StateRequested = null;
            _status = GameFrameworkModuleStatus.Disposed;

            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver.gameObject);
            }
        }

        private void Update(float deltaTime)
        {
            if (_activeStatePath.Count == 0)
            {
                CurrentState?.OnUpdate(deltaTime);
                return;
            }

            for (var i = 0; i < _activeStatePath.Count; i++)
            {
                _activeStatePath[i].OnUpdate(deltaTime);
            }
        }

        private bool CanTransition(IProcedureState currentState, IProcedureState nextState, object userData, out string reason)
        {
            for (var i = 0; i < _guards.Count; i++)
            {
                if (!_guards[i].CanTransition(currentState, nextState, userData, out reason))
                {
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private void RegisterDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Procedure.CurrentState", () => CurrentStateName ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Procedure.LastState", () => _lastStateName ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Procedure.ActiveStatePath", () => ActiveStatePathText);
            diagnostics.RegisterSnapshotProvider("Procedure.LastTransitionSource", () => _lastTransitionSource ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Procedure.TransitionCount", () => _transitionCount.ToString());
            diagnostics.RegisterSnapshotProvider("Procedure.BlockedTransitionCount", () => _blockedTransitionCount.ToString());
            diagnostics.RegisterSnapshotProvider("Procedure.LastBlockedReason", () => _lastBlockedReason ?? string.Empty);
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Procedure.CurrentState");
            diagnostics.RemoveSnapshotProvider("Procedure.LastState");
            diagnostics.RemoveSnapshotProvider("Procedure.ActiveStatePath");
            diagnostics.RemoveSnapshotProvider("Procedure.LastTransitionSource");
            diagnostics.RemoveSnapshotProvider("Procedure.TransitionCount");
            diagnostics.RemoveSnapshotProvider("Procedure.BlockedTransitionCount");
            diagnostics.RemoveSnapshotProvider("Procedure.LastBlockedReason");
            _diagnosticsRegistered = false;
        }

        private List<IProcedureState> BuildStatePath(IProcedureState state)
        {
            var path = new List<IProcedureState>();
            while (state != null)
            {
                path.Add(state);
                if (!_stateParents.TryGetValue(state.Name, out var parentStateName) || !_states.TryGetValue(parentStateName, out state))
                {
                    break;
                }
            }

            path.Reverse();
            return path;
        }

        private static int GetSharedPathDepth(List<IProcedureState> previousPath, List<IProcedureState> nextPath)
        {
            var sharedDepth = 0;
            var maxCount = Math.Min(previousPath.Count, nextPath.Count);
            while (sharedDepth < maxCount && ReferenceEquals(previousPath[sharedDepth], nextPath[sharedDepth]))
            {
                sharedDepth++;
            }

            return sharedDepth;
        }
    }
}

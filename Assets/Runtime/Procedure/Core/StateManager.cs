using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 流程管理器
    /// </summary>
    public class StateManager : IStateManager
    {
        // 静态缓存：流程类型发现结果
        private static readonly Dictionary<Type, Type[]> s_discoveredTypes = new Dictionary<Type, Type[]>();
        private static bool s_isDiscovered;

        private readonly Dictionary<Type, StateBase> _procedures = new Dictionary<Type, StateBase>();
        private StateBase _currentProcedure;
        private UniTaskCompletionSource<bool> _waitForCompletionSource;
        private Type _waitForType;
        private CancellationTokenSource _cts;

        public StateBase CurrentProcedure => _currentProcedure;

        public async UniTask StartAsync<T>(CancellationToken cancellationToken = default, params object[] args) where T : StateBase
        {
            // 创建链接的取消令牌
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            // 自动发现并注册所有流程
            DiscoverProcedures();

            var entryType = typeof(T);
            if (!_procedures.TryGetValue(entryType, out var procedure))
            {
                Game.Debug.Error($"Entry procedure not found: {entryType.Name}");
                return;
            }

            try
            {
                await ExecuteProcedureChainAsync(procedure, args, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Game.Debug.Info("Procedure chain cancelled");
            }
        }

        public UniTask WaitForAsync<T>(CancellationToken cancellationToken = default) where T : StateBase
        {
            if (_currentProcedure?.GetType() == typeof(T))
                return UniTask.CompletedTask;

            _waitForType = typeof(T);
            _waitForCompletionSource = new UniTaskCompletionSource<bool>();
            
            if (cancellationToken.CanBeCanceled)
                cancellationToken.Register(() => _waitForCompletionSource?.TrySetCanceled());
            
            return _waitForCompletionSource.Task;
        }

        public bool Contains<T>() where T : StateBase
        {
            return _procedures.ContainsKey(typeof(T));
        }

        public void Shutdown()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            
            _currentProcedure = null;
            _waitForCompletionSource?.TrySetCanceled();
            _waitForCompletionSource = null;

            foreach (var procedure in _procedures.Values)
                procedure.OnDestroy(this);

            _procedures.Clear();
        }

        private void DiscoverProcedures()
        {
            if (_procedures.Count > 0) return;

            // 使用静态缓存
            if (!s_isDiscovered)
            {
                var procedureBaseType = typeof(StateBase);
                var types = new List<Type>();
                
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        types.AddRange(assembly.GetTypes()
                            .Where(t => t.IsClass && !t.IsAbstract && procedureBaseType.IsAssignableFrom(t))
                            .Where(t => t.GetCustomAttribute<ProcedureAttribute>() != null));
                    }
                    catch { }
                }
                
                s_discoveredTypes[procedureBaseType] = types.ToArray();
                s_isDiscovered = true;
            }

            // 从缓存创建实例
            foreach (var type in s_discoveredTypes[typeof(StateBase)])
            {
                if (!_procedures.ContainsKey(type))
                    _procedures[type] = Activator.CreateInstance(type) as StateBase;
            }
        }

        private async UniTask ExecuteProcedureChainAsync(StateBase procedure, object[] args, CancellationToken ct)
        {
            while (procedure != null)
            {
                ct.ThrowIfCancellationRequested();
                
                var previousProcedure = _currentProcedure;
                _currentProcedure = procedure;

                FireProcedureChangedEvent(previousProcedure?.GetType(), procedure.GetType());

                if (_waitForType != null && procedure.GetType() == _waitForType)
                {
                    _waitForCompletionSource?.TrySetResult(true);
                    _waitForCompletionSource = null;
                    _waitForType = null;
                }

                var result = await procedure.OnExecuteAsync(this, ct, args);
                if (result.NextType == null)
                    break;

                if (!_procedures.TryGetValue(result.NextType, out procedure))
                {
                    procedure = Activator.CreateInstance(result.NextType) as StateBase;
                    if (procedure == null)
                    {
                        Game.Debug.Error($"Next procedure not found: {result.NextType.Name}");
                        break;
                    }
                    _procedures[result.NextType] = procedure;
                }

                args = result.Args ?? Array.Empty<object>();
            }

            _currentProcedure = null;
        }

        private void FireProcedureChangedEvent(Type previousType, Type currentType)
        {
            var eventArgs = ReferencePool.Acquire<ProcedureChangedEventArgs>();
            eventArgs.PreviousProcedure = previousType;
            eventArgs.CurrentProcedure = currentType;
            Game.Event.FireNow(this, eventArgs);
        }
    }
}
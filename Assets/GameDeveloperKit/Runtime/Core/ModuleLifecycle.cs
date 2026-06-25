using System;
using System.Runtime.ExceptionServices;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit
{
    /// <summary>
    /// 框架生命周期管理器，负责启动/关闭状态机。
    /// 模块在首次访问时按需创建，Initialize 只初始化框架状态。
    /// </summary>
    public sealed class ModuleLifecycle
    {
        private enum LifecycleState
        {
            Stopped,
            Started,
            ShuttingDown
        }

        private readonly ModuleRegistry _registry;
        private LifecycleState _state = LifecycleState.Stopped;
        private UniTaskCompletionSource _startupCompletion;
        private UniTaskCompletionSource _shutdownCompletion;

        internal ModuleLifecycle(ModuleRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// 框架是否已启动。
        /// </summary>
        public bool IsStarted => _state == LifecycleState.Started;

        /// <summary>
        /// 框架是否正在关闭。
        /// </summary>
        public bool IsShuttingDown => _state == LifecycleState.ShuttingDown;

        /// <summary>
        /// 初始化框架状态。模块在首次访问时按需创建。
        /// UniTask 回调全部调度到 Unity 主线程 PlayerLoop，因此在两次 await 之间不存在
        /// 并发重入窗口，状态检查和赋值不需要额外的锁或原子操作。
        /// </summary>
        public async UniTask Initialize()
        {
            if (_state == LifecycleState.Started)
            {
                return;
            }

            if (_startupCompletion != null)
            {
                await _startupCompletion.Task;
                return;
            }

            if (_state == LifecycleState.ShuttingDown && _shutdownCompletion != null)
            {
                await _shutdownCompletion.Task;
            }

            // Re-check after waiting for shutdown to complete.
            if (_state == LifecycleState.Started)
            {
                return;
            }

            if (_startupCompletion != null)
            {
                await _startupCompletion.Task;
                return;
            }

            _startupCompletion = new UniTaskCompletionSource();
            _state = LifecycleState.Started;
            _startupCompletion.TrySetResult();
            _startupCompletion = null;
        }

        /// <summary>
        /// 关闭所有已启动模块。
        /// </summary>
        public async UniTask Shutdown()
        {
            if (_state == LifecycleState.Stopped && _registry.ModuleOrder.Count == 0)
            {
                return;
            }

            if (_startupCompletion != null)
            {
                try
                {
                    await _startupCompletion.Task;
                }
                catch
                {
                    return;
                }
            }

            if (_state == LifecycleState.Stopped && _registry.ModuleOrder.Count == 0)
            {
                return;
            }

            if (_state == LifecycleState.ShuttingDown && _shutdownCompletion != null)
            {
                await _shutdownCompletion.Task;
                return;
            }

            _state = LifecycleState.ShuttingDown;
            _shutdownCompletion = new UniTaskCompletionSource();
            try
            {
                var exception = _registry.ShutdownModules();
                _state = LifecycleState.Stopped;
                if (exception != null)
                {
                    _shutdownCompletion.TrySetException(exception);
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                _shutdownCompletion.TrySetResult();
            }
            finally
            {
                _shutdownCompletion = null;
            }
        }
    }
}

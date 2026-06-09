using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Combat;
using GameDeveloperKit.Command;
using GameDeveloperKit.Config;
using GameDeveloperKit.Data;
using GameDeveloperKit.Download;
using GameDeveloperKit.Event;
using GameDeveloperKit.File;
using GameDeveloperKit.Logger;
using GameDeveloperKit.Network;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Sound;
using GameDeveloperKit.Timer;
using GameDeveloperKit.UI;

namespace GameDeveloperKit
{
    /// <summary>
    /// 框架入口
    /// </summary>
    public static class App
    {
        private static readonly Dictionary<Type, IGameModule> _modules = new Dictionary<Type, IGameModule>();
        private static readonly List<Type> _moduleOrder = new List<Type>();
        private static LifecycleState _lifecycleState = LifecycleState.Stopped;
        private static UniTaskCompletionSource _startupCompletion;
        private static UniTaskCompletionSource _shutdownCompletion;

        /// <summary>
        /// 框架事件模块。
        /// </summary>
        public static EventModule Event => Get<EventModule>();
        /// <summary>
        /// 框架资源模块。
        /// </summary>
        public static ResourceModule Resource => Get<ResourceModule>();
        /// <summary>
        /// 框架文件模块。
        /// </summary>
        public static FileModule File => Get<FileModule>();
        /// <summary>
        /// 框架下载模块。
        /// </summary>
        public static DownloadModule Download => Get<DownloadModule>();
        /// <summary>
        /// 框架网络模块。
        /// </summary>
        public static NetworkModule Network => Get<NetworkModule>();
        /// <summary>
        /// 框架配置模块。
        /// </summary>
        public static ConfigModule Config => Get<ConfigModule>();
        /// <summary>
        /// 框架数据模块。
        /// </summary>
        public static DataModule Data => Get<DataModule>();
        /// <summary>
        /// 框架调试模块。
        /// </summary>
        public static DebugModule Debug => Get<DebugModule>();

        /// <summary>
        /// 框架声音模块。
        /// </summary>
        public static SoundModule Sound => Get<SoundModule>();

        /// <summary>
        /// 框架命令模块。
        /// </summary>
        public static CommandModule Command => Get<CommandModule>();

        /// <summary>
        /// 框架UI模块。
        /// </summary>
        public static UIModule UI => Get<UIModule>();

        /// <summary>
        /// 框架操作模块。
        /// </summary>
        public static OperationModule Operation => Get<OperationModule>();

        /// <summary>
        /// 框架流程模块。
        /// </summary>
        public static ProcedureModule Procedure => Get<ProcedureModule>();

        /// <summary>
        /// 框架计时器模块。
        /// </summary>
        public static TimerModule Timer => Get<TimerModule>();

        /// <summary>
        /// 框架战斗模块。
        /// </summary>
        public static CombatModule Combat => Get<CombatModule>();

        static T Get<T>() where T : class, IGameModule
        {
            if (_modules.TryGetValue(typeof(T), out var module))
            {
                return (T)module;
            }

            foreach (var value in _modules.Values)
            {
                if (value is T typedModule)
                {
                    return typedModule;
                }
            }

            throw new GameException($"Module '{typeof(T).Name}' is not registered.");
        }

        internal static bool TryGetRegistered<T>(out T module) where T : class, IGameModule
        {
            if (_modules.TryGetValue(typeof(T), out var value))
            {
                module = (T)value;
                return true;
            }

            module = null;
            return false;
        }

        /// <summary>
        /// 启动框架默认模块。
        /// </summary>
        /// <returns>框架启动任务。</returns>
        public static async UniTask Startup()
        {
            if (_lifecycleState == LifecycleState.Started)
            {
                return;
            }

            if (_lifecycleState == LifecycleState.Starting && _startupCompletion != null)
            {
                await _startupCompletion.Task;
                return;
            }

            if (_lifecycleState == LifecycleState.ShuttingDown && _shutdownCompletion != null)
            {
                await _shutdownCompletion.Task;
            }

            if (_lifecycleState == LifecycleState.Started)
            {
                return;
            }

            if (_lifecycleState == LifecycleState.Starting && _startupCompletion != null)
            {
                await _startupCompletion.Task;
                return;
            }

            await StartupInternal();
        }

        /// <summary>
        /// 关闭所有已启动模块。
        /// </summary>
        /// <returns>框架关闭任务。</returns>
        public static async UniTask Shutdown()
        {
            if (_lifecycleState == LifecycleState.Stopped && _moduleOrder.Count == 0)
            {
                return;
            }

            if (_lifecycleState == LifecycleState.Starting && _startupCompletion != null)
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

            if (_lifecycleState == LifecycleState.Stopped && _moduleOrder.Count == 0)
            {
                return;
            }

            if (_lifecycleState == LifecycleState.ShuttingDown && _shutdownCompletion != null)
            {
                await _shutdownCompletion.Task;
                return;
            }

            await ShutdownInternal();
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns></returns>
        /// <exception cref="GameException">模块已经注册过了</exception>
        public static async UniTask Register<T>() where T : IGameModule, new()
        {
            var type = typeof(T);
            if (_modules.ContainsKey(type))
            {
                throw new GameException($"Module '{type.Name}' has already been registered.");
            }

            var module = new T();
            _modules.Add(type, module);
            try
            {
                await module.Startup();
                TrackModuleOrder(type);
            }
            catch
            {
                _modules.Remove(type);
                RemoveModuleOrder(type);
                throw;
            }
        }

        /// <summary>
        /// 卸载模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns>返回状态</returns>
        /// <exception cref="GameException">模块没有注册</exception>
        public static async UniTask Unregister<T>() where T : IGameModule
        {
            var type = typeof(T);
            if (!_modules.TryGetValue(type, out var module))
            {
                throw new GameException($"Module '{type.Name}' is not registered.");
            }

            _modules.Remove(type);
            RemoveModuleOrder(type);
            await module.Shutdown();
        }

        /// <summary>
        /// 获取或创建模块
        /// </summary>
        /// <param name="module">模块</param>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns>返回状态</returns>
        public static bool TryGetValue<T>(out T module) where T : class, IGameModule, new()
        {
            module = null;
            if (!_modules.TryGetValue(typeof(T), out var value))
            {
                value = new T();
            }

            module = (T)value;
            return true;
        }

        private static async UniTask StartupInternal()
        {
            _lifecycleState = LifecycleState.Starting;
            _startupCompletion = new UniTaskCompletionSource();
            try
            {
                await RegisterDefault<OperationModule>();
                await RegisterDefault<TimerModule>();
                await RegisterDefault<EventModule>();
                await RegisterDefault<FileModule>();
                await RegisterDefault<DownloadModule>();
                await RegisterDefault<CommandModule>();
                await RegisterDefault<NetworkModule>();
                await RegisterDefault<DebugModule>();
                await RegisterDefault<ResourceModule>();
                await RegisterDefault<ConfigModule>();
                await RegisterDefault<DataModule>();
                await RegisterDefault<SoundModule>();
                await RegisterDefault<UIModule>();
                await RegisterDefault<ProcedureModule>();

                _lifecycleState = LifecycleState.Started;
                _startupCompletion.TrySetResult();
            }
            catch (Exception exception)
            {
                var cleanupException = await ShutdownRegisteredModules();
                _lifecycleState = LifecycleState.Stopped;
                if (cleanupException != null)
                {
                    var aggregateException = new AggregateException("Framework startup failed and cleanup failed.", exception, cleanupException);
                    _startupCompletion.TrySetException(aggregateException);
                    throw aggregateException;
                }

                _startupCompletion.TrySetException(exception);
                throw;
            }
            finally
            {
                _startupCompletion = null;
            }
        }

        private static async UniTask ShutdownInternal()
        {
            _lifecycleState = LifecycleState.ShuttingDown;
            _shutdownCompletion = new UniTaskCompletionSource();
            try
            {
                var exception = await ShutdownRegisteredModules();
                _lifecycleState = LifecycleState.Stopped;
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

        private static UniTask RegisterDefault<T>() where T : IGameModule, new()
        {
            var type = typeof(T);
            if (_modules.ContainsKey(type))
            {
                TrackModuleOrder(type, true);
                return UniTask.CompletedTask;
            }

            return Register<T>();
        }

        private static async UniTask<Exception> ShutdownRegisteredModules()
        {
            Exception firstException = null;
            for (var i = _moduleOrder.Count - 1; i >= 0; i--)
            {
                var type = _moduleOrder[i];
                if (!_modules.TryGetValue(type, out var module))
                {
                    continue;
                }

                try
                {
                    await module.Shutdown();
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }

            _modules.Clear();
            _moduleOrder.Clear();
            return firstException;
        }

        private static void TrackModuleOrder(Type type, bool moveToEnd = false)
        {
            if (moveToEnd)
            {
                _moduleOrder.Remove(type);
            }

            if (!_moduleOrder.Contains(type))
            {
                _moduleOrder.Add(type);
            }
        }

        private static void RemoveModuleOrder(Type type)
        {
            _moduleOrder.Remove(type);
        }

        private enum LifecycleState
        {
            Stopped,
            Starting,
            Started,
            ShuttingDown
        }
    }
}

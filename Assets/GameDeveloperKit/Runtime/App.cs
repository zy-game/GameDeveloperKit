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
using GameDeveloperKit.Localization;
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
        /// <summary>
        /// 存储 modules。
        /// </summary>
        private static readonly Dictionary<Type, IGameModule> _modules = new Dictionary<Type, IGameModule>();
        /// <summary>
        /// 存储 module Order。
        /// </summary>
        private static readonly List<Type> _moduleOrder = new List<Type>();
        /// <summary>
        /// 存储 lifecycle State。
        /// </summary>
        private static LifecycleState _lifecycleState = LifecycleState.Stopped;
        /// <summary>
        /// 存储 startup Completion。
        /// </summary>
        private static UniTaskCompletionSource _startupCompletion;
        /// <summary>
        /// 存储 shutdown Completion。
        /// </summary>
        private static UniTaskCompletionSource _shutdownCompletion;

        /// <summary>
        /// 框架事件模块。
        /// </summary>
        public static EventModule Event => GetModule<EventModule>();
        /// <summary>
        /// 框架资源模块。
        /// </summary>
        public static ResourceModule Resource => GetModule<ResourceModule>();
        /// <summary>
        /// 框架文件模块。
        /// </summary>
        public static FileModule File => GetModule<FileModule>();
        /// <summary>
        /// 框架下载模块。
        /// </summary>
        public static DownloadModule Download => GetModule<DownloadModule>();
        /// <summary>
        /// 框架网络模块。
        /// </summary>
        public static NetworkModule Network => GetModule<NetworkModule>();
        /// <summary>
        /// 框架配置模块。
        /// </summary>
        public static ConfigModule Config => GetModule<ConfigModule>();
        /// <summary>
        /// 框架数据模块。
        /// </summary>
        public static DataModule Data => GetModule<DataModule>();
        /// <summary>
        /// 框架调试模块。
        /// </summary>
        public static DebugModule Debug => GetModule<DebugModule>();

        /// <summary>
        /// 框架本地化模块。
        /// </summary>
        public static LocalizationModule Localization => GetModule<LocalizationModule>();

        /// <summary>
        /// 框架声音模块。
        /// </summary>
        public static SoundModule Sound => GetModule<SoundModule>();

        /// <summary>
        /// 框架命令模块。
        /// </summary>
        public static CommandModule Command => GetModule<CommandModule>();

        /// <summary>
        /// 框架UI模块。
        /// </summary>
        public static UIModule UI => GetModule<UIModule>();

        /// <summary>
        /// 框架操作模块。
        /// </summary>
        public static OperationModule Operation => GetModule<OperationModule>();

        /// <summary>
        /// 框架流程模块。
        /// </summary>
        public static ProcedureModule Procedure => GetModule<ProcedureModule>();

        /// <summary>
        /// 框架计时器模块。
        /// </summary>
        public static TimerModule Timer => GetModule<TimerModule>();

        /// <summary>
        /// 框架战斗模块。
        /// </summary>
        public static CombatModule Combat => GetModule<CombatModule>();

        /// <summary>
        /// 获取或创建模块，并递归启动声明的模块依赖。
        /// </summary>
        /// <typeparam name="TModule">模块类型。</typeparam>
        /// <returns>模块实例。</returns>
        public static TModule GetModule<TModule>() where TModule : class, IGameModule, new()
        {
            return (TModule)ResolveModuleWithRollback(typeof(TModule));
        }

        /// <summary>
        /// 获取已注册模块。
        /// </summary>
        /// <param name="type">模块类型。</param>
        /// <param name="module">模块实例。</param>
        /// <returns>条件满足时返回 true。</returns>
        private static bool TryGetRegistered(Type type, out IGameModule module)
        {
            if (_modules.TryGetValue(type, out module))
            {
                return true;
            }

            foreach (var value in _modules.Values)
            {
                if (type.IsInstanceOfType(value))
                {
                    module = value;
                    return true;
                }
            }

            module = null;
            return false;
        }

        /// <summary>
        /// 尝试获取 Registered。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="module">module 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public static bool TryGetRegistered<T>(out T module) where T : class, IGameModule
        {
            if (TryGetRegistered(typeof(T), out var value))
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
        /// <returns>执行结果。</returns>
        /// <exception cref="GameException">模块已经注册过了</exception>
        public static UniTask Register<T>() where T : class, IGameModule, new()
        {
            var type = typeof(T);
            if (TryGetRegistered(type, out _))
            {
                throw new GameException($"Module '{type.Name}' has already been registered.");
            }

            GetModule<T>();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 卸载模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns>返回状态</returns>
        /// <exception cref="GameException">模块没有注册</exception>
        public static UniTask Unregister<T>() where T : IGameModule
        {
            var type = typeof(T);
            if (!TryGetRegistered(type, out var module))
            {
                throw new GameException($"Module '{type.Name}' is not registered.");
            }

            var registeredType = module.GetType();
            _modules.Remove(registeredType);
            RemoveModuleOrder(registeredType);
            module.Shutdown();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 获取或创建模块
        /// </summary>
        /// <param name="module">模块</param>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns>返回状态</returns>
        public static bool TryGetValue<T>(out T module) where T : class, IGameModule, new()
        {
            return TryGetRegistered(out module);
        }

        /// <summary>
        /// 启动 Internal。
        /// </summary>
        /// <returns>操作完成任务。</returns>
        private static UniTask StartupInternal()
        {
            _lifecycleState = LifecycleState.Starting;
            _startupCompletion = new UniTaskCompletionSource();

            _lifecycleState = LifecycleState.Started;
            _startupCompletion.TrySetResult();
            _startupCompletion = null;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 关闭 Internal。
        /// </summary>
        /// <returns>操作完成任务。</returns>
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

        /// <summary>
        /// 关闭 Registered Modules。
        /// </summary>
        /// <returns>操作完成任务。</returns>
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
                    module.Shutdown();
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

        /// <summary>
        /// 执行 Track Module Order。
        /// </summary>
        /// <param name="type">type 参数。</param>
        /// <param name="moveToEnd">move To End 参数。</param>
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

        /// <summary>
        /// 移除 Module Order。
        /// </summary>
        /// <param name="type">type 参数。</param>
        private static void RemoveModuleOrder(Type type)
        {
            _moduleOrder.Remove(type);
        }

        /// <summary>
        /// 带回滚地解析模块。
        /// </summary>
        /// <param name="moduleType">模块类型。</param>
        /// <returns>模块实例。</returns>
        private static IGameModule ResolveModuleWithRollback(Type moduleType)
        {
            if (_lifecycleState == LifecycleState.ShuttingDown)
            {
                throw new GameException($"Cannot resolve module '{moduleType.Name}' while framework is shutting down.");
            }

            var createdTypes = new List<Type>();
            try
            {
                return ResolveModule(moduleType, new List<Type>(), createdTypes);
            }
            catch (Exception exception)
            {
                var cleanupException = RollbackCreatedModules(createdTypes);
                if (cleanupException != null)
                {
                    throw new AggregateException($"Module '{moduleType.Name}' startup failed and rollback failed.", exception, cleanupException);
                }

                ExceptionDispatchInfo.Capture(exception).Throw();
                throw;
            }
        }

        /// <summary>
        /// 递归解析模块。
        /// </summary>
        /// <param name="moduleType">模块类型。</param>
        /// <param name="resolvingTypes">正在解析的模块类型。</param>
        /// <param name="createdTypes">本次解析新创建的模块类型。</param>
        /// <returns>模块实例。</returns>
        private static IGameModule ResolveModule(Type moduleType, List<Type> resolvingTypes, List<Type> createdTypes)
        {
            ValidateModuleType(moduleType);
            if (TryGetRegistered(moduleType, out var existingModule))
            {
                return existingModule;
            }

            if (resolvingTypes.Contains(moduleType))
            {
                throw new GameException($"Circular module dependency detected: {FormatDependencyChain(resolvingTypes, moduleType)}.");
            }

            resolvingTypes.Add(moduleType);
            try
            {
                var dependencyTypes = GetModuleDependencyTypes(moduleType);
                for (var i = 0; i < dependencyTypes.Count; i++)
                {
                    ResolveModule(dependencyTypes[i], resolvingTypes, createdTypes);
                }

                if (TryGetRegistered(moduleType, out existingModule))
                {
                    return existingModule;
                }

                var module = (IGameModule)Activator.CreateInstance(moduleType);
                _modules.Add(moduleType, module);
                try
                {
                    module.Startup();
                    TrackModuleOrder(moduleType);
                    createdTypes.Add(moduleType);
                    return module;
                }
                catch (Exception exception)
                {
                    _modules.Remove(moduleType);
                    RemoveModuleOrder(moduleType);
                    throw new GameException($"Failed to start module '{moduleType.Name}'.", exception);
                }
            }
            finally
            {
                resolvingTypes.RemoveAt(resolvingTypes.Count - 1);
            }
        }

        /// <summary>
        /// 获取模块依赖类型。
        /// </summary>
        /// <param name="moduleType">模块类型。</param>
        /// <returns>模块依赖类型列表。</returns>
        private static List<Type> GetModuleDependencyTypes(Type moduleType)
        {
            var attributes = (ModuleDependencyAttribute[])Attribute.GetCustomAttributes(
                moduleType,
                typeof(ModuleDependencyAttribute),
                false);
            var dependencyTypes = new List<Type>(attributes.Length);
            for (var i = 0; i < attributes.Length; i++)
            {
                var dependencyType = attributes[i].DependencyType;
                ValidateModuleType(dependencyType);
                if (!dependencyTypes.Contains(dependencyType))
                {
                    dependencyTypes.Add(dependencyType);
                }
            }

            return dependencyTypes;
        }

        /// <summary>
        /// 校验模块类型。
        /// </summary>
        /// <param name="moduleType">模块类型。</param>
        private static void ValidateModuleType(Type moduleType)
        {
            if (moduleType == null)
            {
                throw new ArgumentNullException(nameof(moduleType));
            }

            if (!typeof(IGameModule).IsAssignableFrom(moduleType))
            {
                throw new GameException($"Module type '{moduleType.FullName}' must implement IGameModule.");
            }

            if (moduleType.IsInterface || moduleType.IsAbstract)
            {
                throw new GameException($"Module type '{moduleType.FullName}' must be a concrete type.");
            }

            if (moduleType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new GameException($"Module type '{moduleType.FullName}' must have a public parameterless constructor.");
            }
        }

        /// <summary>
        /// 回滚本次创建的模块。
        /// </summary>
        /// <param name="createdTypes">本次创建的模块类型。</param>
        /// <returns>首个回滚异常。</returns>
        private static Exception RollbackCreatedModules(List<Type> createdTypes)
        {
            Exception firstException = null;
            for (var i = createdTypes.Count - 1; i >= 0; i--)
            {
                var type = createdTypes[i];
                if (!_modules.TryGetValue(type, out var module))
                {
                    continue;
                }

                try
                {
                    module.Shutdown();
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
                finally
                {
                    _modules.Remove(type);
                    RemoveModuleOrder(type);
                }
            }

            return firstException;
        }

        /// <summary>
        /// 格式化依赖链。
        /// </summary>
        /// <param name="resolvingTypes">正在解析的模块类型。</param>
        /// <param name="repeatedType">重复模块类型。</param>
        /// <returns>格式化后的依赖链。</returns>
        private static string FormatDependencyChain(List<Type> resolvingTypes, Type repeatedType)
        {
            var cycleIndex = resolvingTypes.IndexOf(repeatedType);
            if (cycleIndex < 0)
            {
                cycleIndex = 0;
            }

            var names = new List<string>();
            for (var i = cycleIndex; i < resolvingTypes.Count; i++)
            {
                names.Add(resolvingTypes[i].Name);
            }

            names.Add(repeatedType.Name);
            return string.Join(" -> ", names);
        }

        /// <summary>
        /// 定义 Lifecycle State 枚举。
        /// </summary>
        private enum LifecycleState
        {
            Stopped,
            Starting,
            Started,
            ShuttingDown
        }
    }
}

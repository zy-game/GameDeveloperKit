using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 游戏框架核心入口类，提供模块管理和访问功能
    /// </summary>
    public static class Game
    {
        private static readonly Dictionary<Type, IGameFrameworkModule> Modules = new();
        private static readonly List<Type> ModuleOrder = new();
        private static readonly Type[] PreferredShutdownOrder =
        {
            typeof(ProcedureModule),
            typeof(SceneModule),
            typeof(UIModule),
            typeof(InputModule),
            typeof(AudioModule),
            typeof(LocalizationModule),
            typeof(ResourceModule),
            typeof(DownloadModule),
            typeof(NetworkModule),
            typeof(EventModule),
            typeof(CommandModule),
            typeof(SchedulerModule),
            typeof(PoolModule),
            typeof(PlatformModule),
            typeof(DataModule),
            typeof(DiagnosticsModule)
        };

        /// <summary>
        /// 获取已注册模块的数量
        /// </summary>
        public static int ModuleCount => Modules.Count;

        /// <summary>
        /// 获取命令模块
        /// </summary>
        /// <summary>
        /// 获取命令模块
        /// </summary>
        public static CommandModule Command => GetOrCreateModule(static () => new CommandModule());

        /// <summary>
        /// 获取数据模块
        /// </summary>
        public static DataModule Data => GetOrCreateModule(static () => new DataModule());

        /// <summary>
        /// 获取诊断模块
        /// </summary>
        public static DiagnosticsModule Diagnostics => GetOrCreateModule(static () => new DiagnosticsModule());

        /// <summary>
        /// 获取下载模块
        /// </summary>
        public static DownloadModule Download => GetOrCreateModule(static () => new DownloadModule());

        /// <summary>
        /// 获取事件模块
        /// </summary>
        public static EventModule Event => GetOrCreateModule(static () => new EventModule());

        /// <summary>
        /// 获取输入模块
        /// </summary>
        public static InputModule Input => GetOrCreateModule(static () => new InputModule());

        /// <summary>
        /// 获取本地化模块
        /// </summary>
        public static LocalizationModule Localization => GetOrCreateModule(static () => new LocalizationModule());

        /// <summary>
        /// 获取网络模块
        /// </summary>
        public static NetworkModule Network => GetOrCreateModule(static () => new NetworkModule());

        /// <summary>
        /// 获取音频模块
        /// </summary>
        public static AudioModule Audio => GetOrCreateModule(static () => new AudioModule());

        /// <summary>
        /// 获取平台模块
        /// </summary>
        public static PlatformModule Platform => GetOrCreateModule(static () => new PlatformModule());

        /// <summary>
        /// 获取对象池模块
        /// </summary>
        public static PoolModule Pool => GetOrCreateModule(static () => new PoolModule());

        /// <summary>
        /// 获取流程模块
        /// </summary>
        public static ProcedureModule Procedure => GetOrCreateModule(static () => new ProcedureModule());

        /// <summary>
        /// 获取调度器模块
        /// </summary>
        public static SchedulerModule Scheduler => GetOrCreateModule(static () => new SchedulerModule());

        /// <summary>
        /// 获取UI模块
        /// </summary>
        public static UIModule UI => GetOrCreateModule(static () => new UIModule());

        /// <summary>
        /// 获取场景模块
        /// </summary>
        public static SceneModule Scene => GetOrCreateModule(static () => new SceneModule());

        /// <summary>
        /// 获取资源模块
        /// </summary>
        public static ResourceModule Resource => GetOrCreateModule(static () => new ResourceModule());

        /// <summary>
        /// 获取所有已注册模块的只读集合
        /// </summary>
        /// <summary>
        /// 获取所有已注册模块的只读集合
        /// </summary>
        public static IReadOnlyCollection<IGameFrameworkModule> AllModules => Modules.Values;

        /// <summary>
        /// 注册模块到框架中
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <param name="module">模块实例</param>
        /// <exception cref="ArgumentNullException">模块实例为空</exception>
        /// <exception cref="InvalidOperationException">模块已注册或已释放</exception>
        public static void RegisterModule<TModule>(TModule module)
            where TModule : class, IGameFrameworkModule
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            var moduleType = typeof(TModule);
            if (Modules.ContainsKey(moduleType))
            {
                throw new InvalidOperationException($"Module '{moduleType.FullName}' is already registered.");
            }

            if (module is IGameFrameworkLifecycleModule lifecycleModule
                && lifecycleModule.Status == GameFrameworkModuleStatus.Disposed)
            {
                throw new InvalidOperationException($"Module '{moduleType.FullName}' can not be registered because it is already disposed.");
            }

            Modules.Add(moduleType, module);
            ModuleOrder.Add(moduleType);
        }

        /// <summary>
        /// 检查指定类型的模块是否已注册
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <returns>如果已注册返回true，否则返回false</returns>
        public static bool HasModule<TModule>()
            where TModule : class, IGameFrameworkModule
        {
            return Modules.ContainsKey(typeof(TModule));
        }

        /// <summary>
        /// 获取已注册的模块实例
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <returns>模块实例</returns>
        /// <exception cref="InvalidOperationException">模块未注册</exception>
        public static TModule GetModule<TModule>()
            where TModule : class, IGameFrameworkModule
        {
            if (!TryGetModule<TModule>(out var module))
            {
                throw new InvalidOperationException($"Module '{typeof(TModule).FullName}' is not registered.");
            }

            return module;
        }

        /// <summary>
        /// 获取已注册的模块实例，如果不存在则使用工厂方法创建并注册
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <param name="factory">模块工厂方法</param>
        /// <returns>模块实例</returns>
        /// <exception cref="ArgumentNullException">工厂方法为空</exception>
        public static TModule GetOrCreateModule<TModule>(Func<TModule> factory)
            where TModule : class, IGameFrameworkModule
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (TryGetModule<TModule>(out var module))
            {
                return module;
            }

            module = factory();
            RegisterModule(module);
            return module;
        }

        /// <summary>
        /// 异步初始化模块，如果不存在则使用工厂方法创建并注册
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <param name="factory">模块工厂方法</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已初始化的模块实例</returns>
        /// <exception cref="InvalidOperationException">模块已释放或关闭中</exception>
        public static async UniTask<TModule> InitializeModuleAsync<TModule>(Func<TModule> factory, CancellationToken cancellationToken = default)
            where TModule : class, IGameFrameworkModule
        {
            var module = GetOrCreateModule(factory);
            if (module is IGameFrameworkLifecycleModule lifecycleModule)
            {
                var status = lifecycleModule.Status;
                if (status == GameFrameworkModuleStatus.Disposed
                    || status == GameFrameworkModuleStatus.ShuttingDown)
                {
                    throw new InvalidOperationException($"Module '{typeof(TModule).FullName}' can not be initialized from status '{status}'.");
                }

                if (status != GameFrameworkModuleStatus.Ready)
                {
                    await lifecycleModule.InitializeAsync(cancellationToken);
                }

                if (lifecycleModule.Status != GameFrameworkModuleStatus.Ready)
                {
                    throw new InvalidOperationException($"Module '{typeof(TModule).FullName}' failed to reach ready status. Current status: {lifecycleModule.Status}.");
                }
            }

            return module;
        }

        /// <summary>
        /// 获取模块的状态
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <returns>模块状态</returns>
        /// <exception cref="InvalidOperationException">模块未注册</exception>
        public static GameFrameworkModuleStatus GetModuleStatus<TModule>()
            where TModule : class, IGameFrameworkModule
        {
            if (!TryGetModule<TModule>(out var module))
            {
                throw new InvalidOperationException($"Module '{typeof(TModule).FullName}' is not registered.");
            }

            return module is IGameFrameworkLifecycleModule lifecycleModule
                ? lifecycleModule.Status
                : GameFrameworkModuleStatus.Ready;
        }

        /// <summary>
        /// 尝试获取模块的状态
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <param name="status">模块状态输出参数</param>
        /// <returns>如果成功获取状态返回true，否则返回false</returns>
        public static bool TryGetModuleStatus<TModule>(out GameFrameworkModuleStatus status)
            where TModule : class, IGameFrameworkModule
        {
            if (!TryGetModule<TModule>(out var module))
            {
                status = default;
                return false;
            }

            status = module is IGameFrameworkLifecycleModule lifecycleModule
                ? lifecycleModule.Status
                : GameFrameworkModuleStatus.Ready;
            return true;
        }

        /// <summary>
        /// 检查模块是否处于就绪状态
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <returns>如果模块已就绪返回true，否则返回false</returns>
        public static bool IsModuleReady<TModule>()
            where TModule : class, IGameFrameworkModule
        {
            return TryGetModuleStatus<TModule>(out var status) && status == GameFrameworkModuleStatus.Ready;
        }

        /// <summary>
        /// 确保模块处于就绪状态
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <exception cref="InvalidOperationException">模块未注册或未就绪</exception>
        public static void EnsureModuleReady<TModule>()
            where TModule : class, IGameFrameworkModule
        {
            if (!TryGetModuleStatus<TModule>(out var status))
            {
                throw new InvalidOperationException($"Module '{typeof(TModule).FullName}' is not registered.");
            }

            if (status != GameFrameworkModuleStatus.Ready)
            {
                throw new InvalidOperationException($"Module '{typeof(TModule).FullName}' is not ready. Current status: {status}.");
            }
        }

        /// <summary>
        /// 尝试获取已注册的模块实例
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <param name="module">模块实例输出参数</param>
        /// <returns>如果成功获取模块返回true，否则返回false</returns>
        public static bool TryGetModule<TModule>(out TModule module)
            where TModule : class, IGameFrameworkModule
        {
            if (Modules.TryGetValue(typeof(TModule), out var registeredModule) && registeredModule is TModule typedModule)
            {
                module = typedModule;
                return true;
            }

            module = null!;
            return false;
        }

        /// <summary>
        /// 移除已注册的模块
        /// </summary>
        /// <typeparam name="TModule">模块类型</typeparam>
        /// <param name="dispose">是否释放模块资源</param>
        /// <returns>如果成功移除返回true，否则返回false</returns>
        public static bool RemoveModule<TModule>(bool dispose = true)
            where TModule : class, IGameFrameworkModule
        {
            var moduleType = typeof(TModule);
            if (!Modules.Remove(moduleType, out var module))
            {
                return false;
            }

            ModuleOrder.Remove(moduleType);

            if (dispose)
            {
                module.Dispose();
            }

            return true;
        }

        /// <summary>
        /// 清除所有已注册的模块
        /// </summary>
        /// <param name="dispose">是否释放模块资源</param>
        public static void Clear(bool dispose = true)
        {
            var shutdownOrder = GetShutdownOrderSnapshot();
            for (var i = 0; i < shutdownOrder.Count; i++)
            {
                var moduleType = shutdownOrder[i];
                if (!Modules.TryGetValue(moduleType, out var module))
                {
                    continue;
                }

                if (dispose)
                {
                    module.Dispose();
                }
            }

            Modules.Clear();
            ModuleOrder.Clear();
        }

        /// <summary>
        /// 异步关闭所有模块
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask ShutdownAllAsync(CancellationToken cancellationToken = default)
        {
            var shutdownOrder = GetShutdownOrderSnapshot();
            for (var i = 0; i < shutdownOrder.Count; i++)
            {
                var moduleType = shutdownOrder[i];
                if (!Modules.TryGetValue(moduleType, out var module))
                {
                    continue;
                }

                if (module is IGameFrameworkLifecycleModule lifecycleModule
                    && lifecycleModule.Status != GameFrameworkModuleStatus.Disposed)
                {
                    await lifecycleModule.ShutdownAsync(cancellationToken);
                }
                else
                {
                    module.Dispose();
                }
            }

            Modules.Clear();
            ModuleOrder.Clear();
        }

        /// <summary>
        /// 获取模块关闭顺序的快照
        /// </summary>
        /// <returns>按关闭顺序排列的模块类型列表</returns>
        private static List<Type> GetShutdownOrderSnapshot()
        {
            var shutdownOrder = new List<Type>(ModuleOrder.Count);
            var preferredTypes = new HashSet<Type>(PreferredShutdownOrder);

            for (var i = ModuleOrder.Count - 1; i >= 0; i--)
            {
                var moduleType = ModuleOrder[i];
                if (!preferredTypes.Contains(moduleType) && Modules.ContainsKey(moduleType))
                {
                    shutdownOrder.Add(moduleType);
                }
            }

            for (var i = 0; i < PreferredShutdownOrder.Length; i++)
            {
                var moduleType = PreferredShutdownOrder[i];
                if (Modules.ContainsKey(moduleType))
                {
                    shutdownOrder.Add(moduleType);
                }
            }

            return shutdownOrder;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 游戏框架核心入口类，提供模块管理和访问功能。
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

        public static int ModuleCount => Modules.Count;

        public static CommandModule Command => GetOrCreateModule(static () => new CommandModule());

        public static DataModule Data => GetOrCreateModule(static () => new DataModule());

        public static DiagnosticsModule Diagnostics => GetOrCreateModule(static () => new DiagnosticsModule());

        public static DownloadModule Download => GetOrCreateModule(static () => new DownloadModule());

        public static EventModule Event => GetOrCreateModule(static () => new EventModule());

        public static InputModule Input => GetOrCreateModule(static () => new InputModule());

        public static LocalizationModule Localization => GetOrCreateModule(static () => new LocalizationModule());

        public static NetworkModule Network => GetOrCreateModule(static () => new NetworkModule());

        public static AudioModule Audio => GetOrCreateModule(static () => new AudioModule());

        public static PlatformModule Platform => GetOrCreateModule(static () => new PlatformModule());

        public static PoolModule Pool => GetOrCreateModule(static () => new PoolModule());

        public static ProcedureModule Procedure => GetOrCreateModule(static () => new ProcedureModule());

        public static SchedulerModule Scheduler => GetOrCreateModule(static () => new SchedulerModule());

        public static UIModule UI => GetOrCreateModule(static () => new UIModule());

        public static SceneModule Scene => GetOrCreateModule(static () => new SceneModule());

        public static ResourceModule Resource => GetOrCreateModule(static () => new ResourceModule());

        public static IReadOnlyCollection<IGameFrameworkModule> AllModules => Modules.Values;

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

            Modules.Add(moduleType, module);
            ModuleOrder.Add(moduleType);
        }

        public static bool HasModule<TModule>()
            where TModule : class, IGameFrameworkModule
        {
            return Modules.ContainsKey(typeof(TModule));
        }

        public static TModule GetModule<TModule>()
            where TModule : class, IGameFrameworkModule
        {
            if (!TryGetModule<TModule>(out var module))
            {
                throw new InvalidOperationException($"Module '{typeof(TModule).FullName}' is not registered.");
            }

            return module;
        }

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

        public static async UniTask<TModule> InitializeModuleAsync<TModule>(Func<TModule> factory, CancellationToken cancellationToken = default)
            where TModule : class, IGameFrameworkModule
        {
            var module = GetOrCreateModule(factory);
            if (module is IGameFrameworkLifecycleModule lifecycleModule)
            {
                if (!lifecycleModule.IsInitialized)
                {
                    await lifecycleModule.InitializeAsync(cancellationToken);
                }

                if (!lifecycleModule.IsInitialized)
                {
                    throw new InvalidOperationException($"Module '{typeof(TModule).FullName}' failed to initialize.");
                }
            }

            return module;
        }

        public static bool IsModuleReady<TModule>()
            where TModule : class, IGameFrameworkModule
        {
            if (!TryGetModule<TModule>(out var module))
            {
                return false;
            }

            return module is not IGameFrameworkLifecycleModule lifecycleModule || lifecycleModule.IsInitialized;
        }

        public static void EnsureModuleReady<TModule>()
            where TModule : class, IGameFrameworkModule
        {
            if (!TryGetModule<TModule>(out var module))
            {
                throw new InvalidOperationException($"Module '{typeof(TModule).FullName}' is not registered.");
            }

            if (module is IGameFrameworkLifecycleModule lifecycleModule && !lifecycleModule.IsInitialized)
            {
                throw new InvalidOperationException($"Module '{typeof(TModule).FullName}' is not ready.");
            }
        }

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

                if (module is IGameFrameworkLifecycleModule lifecycleModule)
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

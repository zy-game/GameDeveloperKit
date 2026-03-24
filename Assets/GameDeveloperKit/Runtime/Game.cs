using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    public interface IGameFrameworkModule : IDisposable
    {
    }

    public static class Game
    {
        private static readonly Dictionary<Type, IGameFrameworkModule> Modules = new();
        private static readonly List<Type> ModuleOrder = new();

        public static int ModuleCount => Modules.Count;

        public static CommandModule Command => GetOrCreateModule(static () => new CommandModule());

        public static DownloadModule Download => GetOrCreateModule(static () => new DownloadModule());

        public static EventModule Event => GetOrCreateModule(static () => new EventModule());

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
            for (var i = ModuleOrder.Count - 1; i >= 0; i--)
            {
                var moduleType = ModuleOrder[i];
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
    }
}

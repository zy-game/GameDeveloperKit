using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit
{
    public static class Super
    {
        private static readonly Dictionary<Type, IGameFrameworkModule> s_Modules = new Dictionary<Type, IGameFrameworkModule>();

        public static int ModuleCount => s_Modules.Count;

        internal static void Register<T>(T module) where T : IGameFrameworkModule
        {
            var type = typeof(T);
            if (s_Modules.ContainsKey(type))
            {
                throw new GameFrameworkException($"Module '{type.Name}' has already been registered.");
            }

            s_Modules[type] = module;
        }

        internal static void Unregister<T>() where T : IGameFrameworkModule
        {
            s_Modules.Remove(typeof(T));
        }

        internal static T Get<T>() where T : class, IGameFrameworkModule
        {
            if (s_Modules.TryGetValue(typeof(T), out var module))
            {
                return (T)module;
            }

            return null;
        }

        public static async UniTask StartupAllAsync()
        {
            foreach (var kvp in s_Modules)
            {
                await kvp.Value.Startup();
            }
        }

        public static async UniTask ShutdownAllAsync()
        {
            foreach (var kvp in s_Modules.Reverse())
            {
                await kvp.Value.Shutdown();
            }
        }

        public static EventManager Event => Get<EventManager>();
        public static ResourceManager Resource => Get<ResourceManager>();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit
{
    public static class Super
    {
        private static readonly Dictionary<Type, IGameModule> _modules = new Dictionary<Type, IGameModule>();
        // public static EventManager Event => Get<EventManager>();
        // public static ResourceManager Resource => Get<ResourceManager>();
        public static FileModule File => Get<FileModule>();
        // public static DownloadManager Download => Get<DownloadManager>();

        static T Get<T>() where T : class, IGameModule
        {
            if (_modules.TryGetValue(typeof(T), out var module))
            {
                return (T)module;
            }
            throw new GameException($"Module '{typeof(T).Name}' is not registered.");
        }

        public static UniTask Register<T>() where T : IGameModule, new()
        {
            var type = typeof(T);
            if (_modules.ContainsKey(type))
            {
                throw new GameException($"Module '{type.Name}' has already been registered.");
            }

            var module = new T();
            _modules.Add(type, module);
            return _modules[type].Startup();
        }

        public static UniTask Unregister<T>() where T : IGameModule
        {
            if (!_modules.ContainsKey(typeof(T)))
            {
                throw new GameException($"Module '{typeof(T).Name}' is not registered.");
            }
            var module = _modules[typeof(T)];
            _modules.Remove(typeof(T));
            return module.Shutdown();
        }

        public static async UniTask<T> GetOrCreate<T>() where T : class, IGameModule, new()
        {
            if (!_modules.TryGetValue(typeof(T), out var module))
            {
                module = new T();
                await module.Startup();
            }
            return (T)module;
        }

        public static bool TryGetValue<T>(out T module) where T : class, IGameModule
        {
            if (_modules.TryGetValue(typeof(T), out var value))
            {
                module = (T)value;
                return true;
            }

            module = null;
            return false;
        }
    }
}

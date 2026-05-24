using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.Event;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit
{
    /// <summary>
    /// 框架入口
    /// </summary>
    public static class Super
    {
        private static readonly Dictionary<Type, IGameModule> _modules = new Dictionary<Type, IGameModule>();
        public static EventModule Event => Get<EventModule>();
        public static ResourceModule Resource => Get<ResourceModule>();
        public static FileModule File => Get<FileModule>();
        public static DownloadModule Download => Get<DownloadModule>();
        
        public static OperationModule Operation => Get<OperationModule>();

        static T Get<T>() where T : class, IGameModule
        {
            if (_modules.TryGetValue(typeof(T), out var module))
            {
                return (T)module;
            }

            throw new GameException($"Module '{typeof(T).Name}' is not registered.");
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns></returns>
        /// <exception cref="GameException">模块已经注册过了</exception>
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

        /// <summary>
        /// 卸载模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns>返回状态</returns>
        /// <exception cref="GameException">模块没有注册</exception>
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
    }
}
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Command;
using GameDeveloperKit.Config;
using GameDeveloperKit.Download;
using GameDeveloperKit.Event;
using GameDeveloperKit.File;
using GameDeveloperKit.Logger;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Sound;
using GameDeveloperKit.UI;

namespace GameDeveloperKit
{
    /// <summary>
    /// 框架入口
    /// </summary>
    public static class Super
    {
        private static readonly Dictionary<Type, IGameModule> _modules = new Dictionary<Type, IGameModule>();
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
        /// 框架配置模块。
        /// </summary>
        public static ConfigModule Config => Get<ConfigModule>();
        /// <summary>
        /// 框架日志模块。
        /// </summary>
        public static LoggerModule Logger => Get<LoggerModule>();

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

        static T Get<T>() where T : class, IGameModule
        {
            if (_modules.TryGetValue(typeof(T), out var module))
            {
                return (T)module;
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

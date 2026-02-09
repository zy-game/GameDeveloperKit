using System.Collections.Generic;
using GameDeveloperKit.Log;
using ZLinq;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// 世界模块，管理多个GameWorld实例
    /// </summary>
    public sealed class WorldModule : IWorldManager
    {
        private readonly Dictionary<string, GameWorld> _worlds = new Dictionary<string, GameWorld>();

        public GameWorld DefaultWorld { get; private set; }

        /// <summary>
        /// 模块启动，创建默认World
        /// </summary>
        public void OnStartup()
        {
            DefaultWorld = CreateWorld("Default");
            
            // 注册调试面板
            if (Game.Debug is LoggerModule loggerModule)
            {
                loggerModule.RegisterPanel(new WorldDebugPanel());
            }
        }

        /// <summary>
        /// 模块更新，更新所有World
        /// </summary>
        public void OnUpdate(float elapseSeconds)
        {
            foreach (var world in _worlds.Values.AsValueEnumerable())
            {
                world.OnUpdate(elapseSeconds);
            }
        }

        /// <summary>
        /// 模块清理，销毁所有World
        /// </summary>
        public void OnClearup()
        {
            foreach (var world in _worlds.Values)
            {
                world.OnClearup();
            }

            _worlds.Clear();
            DefaultWorld = null;
        }

        /// <summary>
        /// 创建一个新的World
        /// </summary>
        public GameWorld CreateWorld(string name)
        {
            return CreateWorld(name, null);
        }

        /// <summary>
        /// 创建一个新的World，带时间配置
        /// </summary>
        public GameWorld CreateWorld(string name, WorldTimeConfig timeConfig)
        {
            Throw.IfArgumentInvalid(string.IsNullOrEmpty(name), nameof(name), "World name cannot be null or empty");

            if (_worlds.ContainsKey(name))
            {
                Throw.InvalidOperation($"World with name '{name}' already exists");
            }

            var world = new GameWorld(name, timeConfig);
            _worlds[name] = world;

            Game.Debug.Info($"World created: {name}");
            return world;
        }

        /// <summary>
        /// 获取指定名称的World
        /// </summary>
        public GameWorld GetWorld(string name)
        {
            return _worlds.TryGetValue(name, out var world) ? world : null;
        }

        /// <summary>
        /// 销毁指定名称的World
        /// </summary>
        public bool DestroyWorld(string name)
        {
            if (string.IsNullOrEmpty(name) || !_worlds.TryGetValue(name, out var world))
            {
                return false;
            }

            // 不允许销毁默认World
            if (world == DefaultWorld)
            {
                Game.Debug.Warning($"Cannot destroy default World");
                return false;
            }

            world.OnClearup();
            _worlds.Remove(name);

            Game.Debug.Info($"World destroyed: {name}");
            return true;
        }

        /// <summary>
        /// 检查是否存在指定名称的World
        /// </summary>
        public bool HasWorld(string name)
        {
            return _worlds.ContainsKey(name);
        }

        /// <summary>
        /// 获取所有World
        /// </summary>
        public IReadOnlyCollection<GameWorld> GetAllWorlds()
        {
            return _worlds.Values.AsValueEnumerable().ToArray();
        }
    }
}

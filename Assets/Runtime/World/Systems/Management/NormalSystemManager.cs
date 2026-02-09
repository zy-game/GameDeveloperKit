using System.Collections.Generic;
using Massive;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// Normal系统管理器
    /// 管理INormalSystem的生命周期（OnStartup/OnShutdown）
    /// </summary>
    public sealed class NormalSystemManager
    {
        private readonly WorldContext _context;
        private readonly List<INormalSystem> _systems;
        private readonly Dictionary<IGroupSystem, Query> _queryCache;

        public NormalSystemManager(WorldContext context)
        {
            _context = context;
            _systems = new List<INormalSystem>();
            _queryCache = new Dictionary<IGroupSystem, Query>();
        }

        public void RegisterSystem(ISystem system, GameWorld world)
        {
            if (system is not INormalSystem normalSystem)
                return;

            _systems.Add(normalSystem);

            // 如果是GroupSystem，构建Query缓存
            if (system is IGroupSystem groupSystem)
            {
                var config = groupSystem.GetGroupConfig();
                var query = _context.Query(config?.IncludeSelector, config?.ExcludeSelector);
                _queryCache[groupSystem] = query;
            }

            // 检查是否应该执行OnStartup
            if (ShouldExecute(system))
            {
                normalSystem.OnStartup(world);
            }
        }

        public void UnregisterSystem(ISystem system, GameWorld world)
        {
            if (system is not INormalSystem normalSystem)
                return;

            // 检查是否应该执行OnShutdown
            if (ShouldExecute(system))
            {
                normalSystem.OnShutdown(world);
            }

            _systems.Remove(normalSystem);

            if (system is IGroupSystem groupSystem)
                _queryCache.Remove(groupSystem);
        }

        public void ClearAll(GameWorld world)
        {
            foreach (var system in _systems)
            {
                if (ShouldExecute(system))
                {
                    system.OnShutdown(world);
                }
            }
            _systems.Clear();
            _queryCache.Clear();
        }

        private bool ShouldExecute(ISystem system)
        {
            if (system is not IGroupSystem groupSystem)
                return true;

            if (!_queryCache.TryGetValue(groupSystem, out var query))
                return true;

            var enumerator = query.GetEnumerator();
            bool hasEntities = enumerator.MoveNext();
            enumerator.Dispose();
            return hasEntities;
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using Massive;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// Update系统管理器
    /// 管理IUpdateSystem的注册和更新调度
    /// </summary>
    public sealed class UpdateSystemManager
    {
        private readonly WorldContext _context;
        private readonly List<IUpdateSystem> _systems;
        private readonly Dictionary<IUpdateSystem, Query> _queryCache;

        public UpdateSystemManager(WorldContext context)
        {
            _context = context;
            _systems = new List<IUpdateSystem>();
            _queryCache = new Dictionary<IUpdateSystem, Query>();
        }

        public void RegisterSystem(ISystem system)
        {
            if (system is not IUpdateSystem updateSystem)
                return;

            _systems.Add(updateSystem);

            // 构建Query缓存
            if (system is IGroupSystem groupSystem)
            {
                var config = groupSystem.GetGroupConfig();
                var query = _context.Query(config?.IncludeSelector, config?.ExcludeSelector);
                _queryCache[updateSystem] = query;
            }
            else
            {
                // 非GroupSystem，使用空Filter匹配所有实体
                _queryCache[updateSystem] = _context.Query(Filter.Empty);
            }

            SortSystems();
        }

        public void UnregisterSystem(ISystem system)
        {
            if (system is not IUpdateSystem updateSystem)
                return;

            _systems.Remove(updateSystem);
            _queryCache.Remove(updateSystem);
        }

        public void UpdateAll(GameWorld world, float deltaTime)
        {
            foreach (var system in _systems)
            {
                if (_queryCache.TryGetValue(system, out var query))
                {
                    query.ForEach((int entityId) => { system.OnUpdate(world, entityId, deltaTime); });
                }
            }
        }

        private void SortSystems()
        {
            _systems.Sort((a, b) => GetPriority(a).CompareTo(GetPriority(b)));
        }

        private int GetPriority(ISystem system)
        {
            var attr = system.GetType().GetCustomAttribute<SystemPriorityAttribute>();
            return attr?.Priority ?? 0;
        }

        public void Clear()
        {
            _systems.Clear();
            _queryCache.Clear();
        }
    }
}
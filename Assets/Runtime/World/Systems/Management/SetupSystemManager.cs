using System;
using System.Collections.Generic;
using Massive;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// Setup系统管理器
    /// 管理ISetupSystem的注册和事件分发
    /// </summary>
    public sealed class SetupSystemManager
    {
        private readonly WorldContext _context;
        private readonly List<ISetupSystem> _systems;
        private readonly Dictionary<IGroupSystem, Query> _queryCache;
        private readonly Dictionary<IGroupSystem, Type[]> _includeTypesCache;

        public SetupSystemManager(WorldContext context)
        {
            _context = context;
            _systems = new List<ISetupSystem>();
            _queryCache = new Dictionary<IGroupSystem, Query>();
            _includeTypesCache = new Dictionary<IGroupSystem, Type[]>();
        }

        public void RegisterSystem(ISystem system)
        {
            if (system is not ISetupSystem setupSystem)
                return;

            if (system is not IGroupSystem groupSystem)
                return;

            _systems.Add(setupSystem);

            var config = groupSystem.GetGroupConfig();
            var query = _context.Query(config?.IncludeSelector, config?.ExcludeSelector);
            _queryCache[groupSystem] = query;
            
            if (config?.IncludeSelector != null)
            {
                _includeTypesCache[groupSystem] = _context.GetSelectorTypes(config.IncludeSelector);
            }
        }

        public void UnregisterSystem(ISystem system)
        {
            if (system is not ISetupSystem setupSystem)
                return;

            _systems.Remove(setupSystem);

            if (system is IGroupSystem groupSystem)
            {
                _queryCache.Remove(groupSystem);
                _includeTypesCache.Remove(groupSystem);
            }
        }

        public void DispatchSetup(GameWorld world, int entityId, Type componentType)
        {
            foreach (var system in _systems)
            {
                if (system is not IGroupSystem groupSystem)
                    continue;

                // 快速检查：componentType是否在Include中
                if (!_includeTypesCache.TryGetValue(groupSystem, out var includeTypes))
                    continue;

                if (Array.IndexOf(includeTypes, componentType) < 0)
                    continue;

                // 使用Query的Filter检查entity是否符合条件
                if (_queryCache.TryGetValue(groupSystem, out var query))
                {
                    if (MatchesFilter(entityId, query.Filter))
                    {
                        system.OnSetup(world, entityId);
                    }
                }
            }
        }

        private bool MatchesFilter(int entityId, Filter filter)
        {
            for (int i = 0; i < filter.IncludedCount; i++)
            {
                if (!filter.Included[i].Has(entityId))
                    return false;
            }

            for (int i = 0; i < filter.ExcludedCount; i++)
            {
                if (filter.Excluded[i].Has(entityId))
                    return false;
            }

            return true;
        }

        public void Clear()
        {
            _systems.Clear();
            _queryCache.Clear();
            _includeTypesCache.Clear();
        }
    }
}

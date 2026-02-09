using System;
using System.Collections.Generic;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// 系统管理器
    /// 作为门面统一管理所有子管理器
    /// </summary>
    public sealed class SystemManager
    {
        private readonly List<ISystem> _systems;
        private readonly Dictionary<Type, ISystem> _systemByType;

        private readonly NormalSystemManager _normalSystemManager;
        private readonly UpdateSystemManager _updateSystemManager;
        private readonly SetupSystemManager _setupSystemManager;
        private readonly TeardownSystemManager _teardownSystemManager;

        public IReadOnlyList<ISystem> Systems => _systems;

        public SystemManager(WorldContext context)
        {
            _systems = new List<ISystem>();
            _systemByType = new Dictionary<Type, ISystem>();

            _normalSystemManager = new NormalSystemManager(context);
            _updateSystemManager = new UpdateSystemManager(context);
            _setupSystemManager = new SetupSystemManager(context);
            _teardownSystemManager = new TeardownSystemManager(context);
        }

        #region System Registration

        public void AddSystem(ISystem system, GameWorld world)
        {
            if (system == null || _systems.Contains(system))
                return;

            _systems.Add(system);
            _systemByType[system.GetType()] = system;

            // 分发到子管理器
            _normalSystemManager.RegisterSystem(system, world);
            _updateSystemManager.RegisterSystem(system);
            _setupSystemManager.RegisterSystem(system);
            _teardownSystemManager.RegisterSystem(system);
        }

        public void RemoveSystem(ISystem system, GameWorld world)
        {
            if (system == null || !_systems.Contains(system))
                return;

            // 从子管理器注销
            _normalSystemManager.UnregisterSystem(system, world);
            _updateSystemManager.UnregisterSystem(system);
            _setupSystemManager.UnregisterSystem(system);
            _teardownSystemManager.UnregisterSystem(system);

            _systems.Remove(system);
            _systemByType.Remove(system.GetType());
        }

        public void RemoveSystem<T>(GameWorld world) where T : ISystem
        {
            for (int i = _systems.Count - 1; i >= 0; i--)
            {
                if (_systems[i] is T)
                {
                    RemoveSystem(_systems[i], world);
                }
            }
        }

        public T GetSystem<T>() where T : class, ISystem
        {
            return _systemByType.TryGetValue(typeof(T), out var system) ? system as T : null;
        }

        #endregion

        #region Update

        public void UpdateAll(GameWorld world, float deltaTime)
        {
            _updateSystemManager.UpdateAll(world, deltaTime);
        }

        #endregion

        #region Setup/Teardown Events

        public void DispatchSetup(GameWorld world, int entityId, Type componentType)
        {
            _setupSystemManager.DispatchSetup(world, entityId, componentType);
        }

        public void DispatchTeardown(GameWorld world, int entityId, Type componentType)
        {
            _teardownSystemManager.DispatchTeardown(world, entityId, componentType);
        }

        #endregion

        #region Cleanup

        public void Clear(GameWorld world)
        {
            _normalSystemManager.ClearAll(world);
            _updateSystemManager.Clear();
            _setupSystemManager.Clear();
            _teardownSystemManager.Clear();
            _systems.Clear();
            _systemByType.Clear();
        }

        #endregion
    }
}

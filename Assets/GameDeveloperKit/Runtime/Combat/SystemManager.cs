using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗系统管理器。
    /// </summary>
    public sealed class SystemManager
    {
        private readonly World m_World;
        private readonly List<SystemRegistration> m_Registrations = new List<SystemRegistration>();
        private readonly List<Entity> m_Buffer = new List<Entity>();

        internal SystemManager(World world)
        {
            m_World = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>
        /// 注册系统。
        /// </summary>
        /// <param name="system">系统实例。</param>
        public void Add(SystemBase system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            if (Contains(system))
            {
                throw new GameException($"System '{system.GetType().Name}' has already been registered.");
            }

            var registration = new SystemRegistration(
                system,
                Normalize(system.Include, nameof(system.Include)),
                Normalize(system.Exclude, nameof(system.Exclude)));

            ValidateNoConflict(registration.Include, registration.Exclude, system);
            system.Initialize(m_World);
            m_Registrations.Add(registration);

            foreach (var entity in m_World.EntityManager.AliveEntities)
            {
                if (!registration.Matches(entity, m_World.EntityManager))
                {
                    continue;
                }

                registration.Active.Add(entity);
                system.InvokeOnCreate(entity);
            }
        }

        /// <summary>
        /// 创建并注册系统。
        /// </summary>
        /// <typeparam name="TSystem">系统类型。</typeparam>
        /// <returns>系统实例。</returns>
        public TSystem Add<TSystem>() where TSystem : SystemBase, new()
        {
            var system = new TSystem();
            Add(system);
            return system;
        }

        /// <summary>
        /// 移除系统。
        /// </summary>
        /// <param name="system">系统实例。</param>
        /// <returns>系统是否被移除。</returns>
        public bool Remove(SystemBase system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            var index = IndexOf(system);
            if (index < 0)
            {
                return false;
            }

            var registration = m_Registrations[index];
            DestroyActive(registration);
            m_Registrations.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// 更新系统。
        /// </summary>
        public void Update()
        {
            foreach (var registration in m_Registrations)
            {
                m_Buffer.Clear();
                m_Buffer.AddRange(registration.Active);
                m_Buffer.Sort(CompareEntity);

                foreach (var entity in m_Buffer)
                {
                    if (!registration.Active.Contains(entity))
                    {
                        continue;
                    }

                    if (!entity.IsAlive || !registration.Matches(entity, m_World.EntityManager))
                    {
                        if (registration.Active.Remove(entity))
                        {
                            registration.System.InvokeOnDestroy(entity);
                        }

                        continue;
                    }

                    registration.System.InvokeOnUpdate(entity);
                }
            }

            m_Buffer.Clear();
        }

        /// <summary>
        /// 刷新实体匹配状态。
        /// </summary>
        /// <param name="entity">实体。</param>
        public void Refresh(Entity entity)
        {
            m_World.EntityManager.ValidateEntityWorld(entity);
            if (!entity.IsAlive)
            {
                RemoveEntity(entity);
                return;
            }

            foreach (var registration in m_Registrations)
            {
                Refresh(registration, entity);
            }
        }

        /// <summary>
        /// 重建所有系统匹配集合。
        /// </summary>
        public void Rebuild()
        {
            foreach (var registration in m_Registrations)
            {
                m_Buffer.Clear();
                m_Buffer.AddRange(registration.Active);
                m_Buffer.Sort(CompareEntity);

                foreach (var entity in m_Buffer)
                {
                    if (entity.IsAlive && registration.Matches(entity, m_World.EntityManager))
                    {
                        continue;
                    }

                    registration.Active.Remove(entity);
                    registration.System.InvokeOnDestroy(entity);
                }

                foreach (var entity in m_World.EntityManager.AliveEntities)
                {
                    if (registration.Active.Contains(entity) ||
                        !registration.Matches(entity, m_World.EntityManager))
                    {
                        continue;
                    }

                    registration.Active.Add(entity);
                    registration.System.InvokeOnCreate(entity);
                }
            }

            m_Buffer.Clear();
        }

        /// <summary>
        /// 清理所有系统。
        /// </summary>
        public void Clear()
        {
            foreach (var registration in m_Registrations)
            {
                DestroyActive(registration);
            }

            m_Registrations.Clear();
            m_Buffer.Clear();
        }

        internal void RemoveEntity(Entity entity)
        {
            foreach (var registration in m_Registrations)
            {
                if (registration.Active.Remove(entity))
                {
                    registration.System.InvokeOnDestroy(entity);
                }
            }
        }

        private void Refresh(SystemRegistration registration, Entity entity)
        {
            var matches = registration.Matches(entity, m_World.EntityManager);
            var active = registration.Active.Contains(entity);

            if (matches && !active)
            {
                registration.Active.Add(entity);
                registration.System.InvokeOnCreate(entity);
                return;
            }

            if (!matches && active)
            {
                registration.Active.Remove(entity);
                registration.System.InvokeOnDestroy(entity);
            }
        }

        private void DestroyActive(SystemRegistration registration)
        {
            m_Buffer.Clear();
            m_Buffer.AddRange(registration.Active);
            m_Buffer.Sort(CompareEntity);

            foreach (var entity in m_Buffer)
            {
                registration.System.InvokeOnDestroy(entity);
            }

            registration.Active.Clear();
            m_Buffer.Clear();
        }

        private bool Contains(SystemBase system)
        {
            return IndexOf(system) >= 0;
        }

        private int IndexOf(SystemBase system)
        {
            for (var i = 0; i < m_Registrations.Count; i++)
            {
                if (ReferenceEquals(m_Registrations[i].System, system))
                {
                    return i;
                }
            }

            return -1;
        }

        private static ComponentType[] Normalize(ComponentType[] componentTypes, string name)
        {
            if (componentTypes == null || componentTypes.Length == 0)
            {
                return Array.Empty<ComponentType>();
            }

            var result = new List<ComponentType>(componentTypes.Length);
            var seen = new HashSet<ComponentType>();
            foreach (var componentType in componentTypes)
            {
                if (componentType.Type == null)
                {
                    throw new ArgumentNullException(name, "System component type cannot be default.");
                }

                var normalized = ComponentType.From(componentType.Type);
                if (seen.Add(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result.Count == 0 ? Array.Empty<ComponentType>() : result.ToArray();
        }

        private static void ValidateNoConflict(ComponentType[] include, ComponentType[] exclude, SystemBase system)
        {
            if (include.Length == 0 || exclude.Length == 0)
            {
                return;
            }

            var includeSet = new HashSet<ComponentType>(include);
            foreach (var componentType in exclude)
            {
                if (includeSet.Contains(componentType))
                {
                    throw new GameException($"System '{system.GetType().Name}' cannot include and exclude component '{componentType}'.");
                }
            }
        }

        private static int CompareEntity(Entity left, Entity right)
        {
            var idCompare = left.Id.CompareTo(right.Id);
            return idCompare != 0 ? idCompare : left.Version.CompareTo(right.Version);
        }

        private sealed class SystemRegistration
        {
            public SystemRegistration(SystemBase system, ComponentType[] include, ComponentType[] exclude)
            {
                System = system;
                Include = include;
                Exclude = exclude;
            }

            public SystemBase System { get; }

            public ComponentType[] Include { get; }

            public ComponentType[] Exclude { get; }

            public HashSet<Entity> Active { get; } = new HashSet<Entity>();

            public bool Matches(Entity entity, EntityManager entityManager)
            {
                if (!entity.IsAlive)
                {
                    return false;
                }

                foreach (var componentType in Include)
                {
                    if (!entityManager.Has(entity, componentType))
                    {
                        return false;
                    }
                }

                foreach (var componentType in Exclude)
                {
                    if (entityManager.Has(entity, componentType))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}

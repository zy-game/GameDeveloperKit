using System;
using System.Collections.Generic;
using System.Linq;
using Massive;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗系统管理器。
    /// </summary>
    public sealed partial class SystemManager
    {
        private readonly World m_World;
        private readonly MassiveWorld m_MassiveWorld;
        private readonly List<Registration> m_Registrations = new List<Registration>();

        internal IEnumerable<Registration> Registrations => m_Registrations;

        internal SystemManager(World world, MassiveWorld massiveWorld)
        {
            m_World = world ?? throw new ArgumentNullException(nameof(world));
            m_MassiveWorld = massiveWorld ?? throw new ArgumentNullException(nameof(massiveWorld));
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

            var registration = new Registration(system, m_MassiveWorld);
            system.Initialize(m_World);
            m_Registrations.Add(registration);
            InvokeOnCreateForMatches(registration);
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

            var registration = m_Registrations.FirstOrDefault(x => ReferenceEquals(x.System, system));
            if (registration == null)
            {
                return false;
            }

            InvokeOnDestroyForMatches(registration);
            m_Registrations.Remove(registration);
            return true;
        }

        /// <summary>
        /// 移除系统。
        /// </summary>
        /// <typeparam name="TSystem">系统类型。</typeparam>
        /// <returns>系统是否被移除。</returns>
        public bool Remove<TSystem>() where TSystem : SystemBase
        {
            var systemType = typeof(TSystem);
            var registration = m_Registrations.FirstOrDefault(x => x.SystemType == systemType);
            if (registration == null)
            {
                return false;
            }

            InvokeOnDestroyForMatches(registration);
            m_Registrations.Remove(registration);
            return true;
        }

        /// <summary>
        /// 清理所有系统。
        /// </summary>
        public void Clear()
        {
            foreach (var registration in m_Registrations)
            {
                InvokeOnDestroyForMatches(registration);
            }

            m_Registrations.Clear();
        }

        public bool Contains(SystemBase system)
        {
            return m_Registrations.Any(x => ReferenceEquals(x.System, system));
        }

        internal Dictionary<Registration, bool> Capture(Entity entity)
        {
            var snapshot = new Dictionary<Registration, bool>(m_Registrations.Count);
            foreach (var registration in m_Registrations)
            {
                snapshot.Add(registration, registration.Matches(entity));
            }

            return snapshot;
        }

        internal Dictionary<Registration, HashSet<Entity>> Capture()
        {
            var snapshot = new Dictionary<Registration, HashSet<Entity>>(m_Registrations.Count);
            foreach (var registration in m_Registrations)
            {
                snapshot.Add(registration, CollectMatches(registration));
            }

            return snapshot;
        }

        internal void NotifyChanged(Entity entity, Dictionary<Registration, bool> before)
        {
            before ??= new Dictionary<Registration, bool>();
            foreach (var registration in m_Registrations)
            {
                var wasMatching = before.TryGetValue(registration, out var matched) && matched;
                var isMatching = registration.Matches(entity);

                if (!wasMatching && isMatching)
                {
                    registration.System.OnCreate(entity);
                    continue;
                }

                if (wasMatching && !isMatching)
                {
                    registration.System.OnDestroy(entity);
                }
            }
        }

        internal void NotifyDestroyed(Entity entity, Dictionary<Registration, bool> before)
        {
            before ??= new Dictionary<Registration, bool>();
            foreach (var registration in m_Registrations)
            {
                if (before.TryGetValue(registration, out var wasMatching) && wasMatching)
                {
                    registration.System.OnDestroy(entity);
                }
            }
        }

        internal void NotifyChanged(Dictionary<Registration, HashSet<Entity>> before)
        {
            before ??= new Dictionary<Registration, HashSet<Entity>>();
            foreach (var registration in m_Registrations)
            {
                before.TryGetValue(registration, out var previous);
                previous ??= new HashSet<Entity>();

                var current = CollectMatches(registration);
                foreach (var entity in previous)
                {
                    if (!current.Contains(entity))
                    {
                        registration.System.OnDestroy(entity);
                    }
                }

                foreach (var entity in current)
                {
                    if (!previous.Contains(entity))
                    {
                        registration.System.OnCreate(entity);
                    }
                }
            }
        }

        private HashSet<Entity> CollectMatches(Registration registration)
        {
            var entities = new HashSet<Entity>();
            foreach (var entity in m_World.ForEach(registration.Filter))
            {
                entities.Add(entity);
            }

            return entities;
        }

        private void InvokeOnCreateForMatches(Registration registration)
        {
            foreach (var entity in m_World.ForEach(registration.Filter))
            {
                registration.System.OnCreate(entity);
            }
        }

        private void InvokeOnDestroyForMatches(Registration registration)
        {
            foreach (var entity in m_World.ForEach(registration.Filter))
            {
                registration.System.OnDestroy(entity);
            }
        }
    }
}

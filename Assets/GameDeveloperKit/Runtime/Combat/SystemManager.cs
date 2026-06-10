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
        /// <summary>
        /// 系统所属的战斗世界。
        /// </summary>
        private readonly World m_World;

        /// <summary>
        /// 底层 Massive 世界实例。
        /// </summary>
        private readonly MassiveWorld m_MassiveWorld;

        /// <summary>
        /// 当前已注册的系统记录。
        /// </summary>
        private readonly List<Registration> m_Registrations = new List<Registration>();

        /// <summary>
        /// 按组件类型索引的系统注册记录。
        /// </summary>
        private readonly Dictionary<Type, List<Registration>> m_RegistrationsByComponent = new Dictionary<Type, List<Registration>>();

        /// <summary>
        /// 当前活跃系统注册记录快照。
        /// </summary>
        internal Registration[] Registrations => GetRegistrationsSnapshot();

        /// <summary>
        /// 初始化战斗系统管理器。
        /// </summary>
        /// <param name="world">系统所属的战斗世界。</param>
        /// <param name="massiveWorld">底层 Massive 世界实例。</param>
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
            AddToComponentIndex(registration);
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

            var registration = m_Registrations.FirstOrDefault(x => x.IsActive && ReferenceEquals(x.System, system));
            if (registration == null)
            {
                return false;
            }

            return RemoveRegistration(registration);
        }

        /// <summary>
        /// 移除系统。
        /// </summary>
        /// <typeparam name="TSystem">系统类型。</typeparam>
        /// <returns>系统是否被移除。</returns>
        public bool Remove<TSystem>() where TSystem : SystemBase
        {
            var systemType = typeof(TSystem);
            var registration = m_Registrations.FirstOrDefault(x => x.IsActive && x.SystemType == systemType);
            if (registration == null)
            {
                return false;
            }

            return RemoveRegistration(registration);
        }

        /// <summary>
        /// 清理所有系统。
        /// </summary>
        public void Clear()
        {
            foreach (var registration in GetRegistrationsSnapshot())
            {
                RemoveRegistration(registration);
            }

            m_Registrations.Clear();
            m_RegistrationsByComponent.Clear();
        }

        /// <summary>
        /// 查询系统实例是否已注册。
        /// </summary>
        /// <param name="system">系统实例。</param>
        /// <returns>系统已注册且仍然活跃时返回 true。</returns>
        public bool Contains(SystemBase system)
        {
            return m_Registrations.Any(x => x.IsActive && ReferenceEquals(x.System, system));
        }

        /// <summary>
        /// 捕获指定实体变更前对相关系统的匹配状态。
        /// </summary>
        /// <param name="entity">即将变化的实体。</param>
        /// <param name="changedComponentType">发生变化的组件类型。</param>
        /// <returns>系统注册记录到变更前匹配状态的快照。</returns>
        internal Dictionary<Registration, bool> Capture(Entity entity, Type changedComponentType = null)
        {
            var registrations = changedComponentType == null ? GetRegistrationsSnapshot() : GetRegistrationsSnapshot(changedComponentType);
            var snapshot = new Dictionary<Registration, bool>(registrations.Length);
            foreach (var registration in registrations)
            {
                snapshot.Add(registration, registration.Matches(entity));
            }

            return snapshot;
        }

        /// <summary>
        /// 捕获所有系统当前匹配的实体集合。
        /// </summary>
        /// <returns>系统注册记录到当前匹配实体集合的快照。</returns>
        internal Dictionary<Registration, HashSet<Entity>> Capture()
        {
            var registrations = GetRegistrationsSnapshot();
            var snapshot = new Dictionary<Registration, HashSet<Entity>>(registrations.Length);
            foreach (var registration in registrations)
            {
                snapshot.Add(registration, CollectMatches(registration));
            }

            return snapshot;
        }

        /// <summary>
        /// 根据实体变更前后的匹配状态派发系统进入或离开回调。
        /// </summary>
        /// <param name="entity">已经变化的实体。</param>
        /// <param name="before">变更前的系统匹配状态。</param>
        internal void NotifyChanged(Entity entity, Dictionary<Registration, bool> before)
        {
            var registrations = before == null ? GetRegistrationsSnapshot() : GetRegistrationsSnapshot(before.Keys);
            before ??= new Dictionary<Registration, bool>();
            foreach (var registration in registrations)
            {
                if (!registration.IsActive)
                {
                    continue;
                }

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

        /// <summary>
        /// 根据销毁前的匹配状态派发系统离开回调。
        /// </summary>
        /// <param name="entity">被销毁的实体。</param>
        /// <param name="before">销毁前的系统匹配状态。</param>
        internal void NotifyDestroyed(Entity entity, Dictionary<Registration, bool> before)
        {
            before ??= new Dictionary<Registration, bool>();
            foreach (var registration in GetRegistrationsSnapshot(before.Keys))
            {
                if (registration.IsActive && before.TryGetValue(registration, out var wasMatching) && wasMatching)
                {
                    registration.System.OnDestroy(entity);
                }
            }
        }

        /// <summary>
        /// 根据系统匹配实体集合快照派发批量进入或离开回调。
        /// </summary>
        /// <param name="before">变更前的系统匹配实体集合。</param>
        internal void NotifyChanged(Dictionary<Registration, HashSet<Entity>> before)
        {
            before ??= new Dictionary<Registration, HashSet<Entity>>();
            foreach (var registration in GetRegistrationsSnapshot(before.Keys))
            {
                if (!registration.IsActive)
                {
                    continue;
                }

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

        /// <summary>
        /// 收集指定系统当前匹配的实体集合。
        /// </summary>
        /// <param name="registration">系统注册记录。</param>
        /// <returns>匹配实体集合。</returns>
        private HashSet<Entity> CollectMatches(Registration registration)
        {
            var entities = new HashSet<Entity>();
            if (!registration.IsActive)
            {
                return entities;
            }

            foreach (var entity in m_World.ForEach(registration.Filter))
            {
                entities.Add(entity);
            }

            return entities;
        }

        /// <summary>
        /// 对系统当前匹配的实体触发进入回调。
        /// </summary>
        /// <param name="registration">系统注册记录。</param>
        private void InvokeOnCreateForMatches(Registration registration)
        {
            if (!registration.IsActive)
            {
                return;
            }

            foreach (var entity in m_World.ForEach(registration.Filter))
            {
                if (!registration.IsActive)
                {
                    break;
                }

                registration.System.OnCreate(entity);
            }
        }

        /// <summary>
        /// 对系统当前匹配的实体触发离开回调。
        /// </summary>
        /// <param name="registration">系统注册记录。</param>
        private void InvokeOnDestroyForMatches(Registration registration)
        {
            foreach (var entity in m_World.ForEach(registration.Filter))
            {
                registration.System.OnDestroy(entity);
            }
        }

        /// <summary>
        /// 移除系统注册记录并触发离开回调。
        /// </summary>
        /// <param name="registration">系统注册记录。</param>
        /// <returns>注册记录被移除时返回 true。</returns>
        private bool RemoveRegistration(Registration registration)
        {
            if (registration == null || !registration.IsActive)
            {
                return false;
            }

            registration.IsActive = false;
            RemoveFromComponentIndex(registration);
            m_Registrations.Remove(registration);
            InvokeOnDestroyForMatches(registration);
            return true;
        }

        /// <summary>
        /// 获取所有活跃系统注册记录的快照。
        /// </summary>
        /// <returns>活跃系统注册记录数组。</returns>
        private Registration[] GetRegistrationsSnapshot()
        {
            if (m_Registrations.Count == 0)
            {
                return Array.Empty<Registration>();
            }

            var snapshot = new List<Registration>(m_Registrations.Count);
            foreach (var registration in m_Registrations)
            {
                if (registration.IsActive)
                {
                    snapshot.Add(registration);
                }
            }

            return snapshot.Count == 0 ? Array.Empty<Registration>() : snapshot.ToArray();
        }

        /// <summary>
        /// 获取关注指定组件类型的活跃系统注册记录快照。
        /// </summary>
        /// <param name="componentType">组件类型。</param>
        /// <returns>活跃系统注册记录数组。</returns>
        private Registration[] GetRegistrationsSnapshot(Type componentType)
        {
            if (componentType == null || !m_RegistrationsByComponent.TryGetValue(componentType, out var registrations))
            {
                return Array.Empty<Registration>();
            }

            var snapshot = new List<Registration>(registrations.Count);
            foreach (var registration in registrations)
            {
                if (registration.IsActive)
                {
                    snapshot.Add(registration);
                }
            }

            return snapshot.Count == 0 ? Array.Empty<Registration>() : snapshot.ToArray();
        }

        /// <summary>
        /// 从候选注册记录中获取仍然活跃的快照。
        /// </summary>
        /// <param name="registrations">候选系统注册记录。</param>
        /// <returns>活跃系统注册记录数组。</returns>
        private static Registration[] GetRegistrationsSnapshot(IEnumerable<Registration> registrations)
        {
            var snapshot = new List<Registration>();
            foreach (var registration in registrations)
            {
                if (registration != null && registration.IsActive)
                {
                    snapshot.Add(registration);
                }
            }

            return snapshot.Count == 0 ? Array.Empty<Registration>() : snapshot.ToArray();
        }

        /// <summary>
        /// 把系统注册记录加入组件类型索引。
        /// </summary>
        /// <param name="registration">系统注册记录。</param>
        private void AddToComponentIndex(Registration registration)
        {
            foreach (var componentType in registration.ComponentTypes)
            {
                if (!m_RegistrationsByComponent.TryGetValue(componentType, out var registrations))
                {
                    registrations = new List<Registration>();
                    m_RegistrationsByComponent[componentType] = registrations;
                }

                registrations.Add(registration);
            }
        }

        /// <summary>
        /// 从组件类型索引中移除系统注册记录。
        /// </summary>
        /// <param name="registration">系统注册记录。</param>
        private void RemoveFromComponentIndex(Registration registration)
        {
            foreach (var componentType in registration.ComponentTypes)
            {
                if (!m_RegistrationsByComponent.TryGetValue(componentType, out var registrations))
                {
                    continue;
                }

                registrations.Remove(registration);
                if (registrations.Count == 0)
                {
                    m_RegistrationsByComponent.Remove(componentType);
                }
            }
        }
    }
}

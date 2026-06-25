using System;
using System.Collections.Generic;
using Massive;
using MassiveWorld = Massive.MassiveWorld;

namespace GameDeveloperKit.Combat
{
    public sealed class World : IDisposable
    {
        public const int DefaultFrameRate = 50;

        private bool m_Disposed;
        private int m_FrameRate;
        private float m_Accumulator;
        private readonly MassiveWorld m_World;
        private readonly EntityManager m_Entities;
        private readonly SystemManager m_Systems;

        public int FrameRate
        {
            get => m_FrameRate;
            set
            {
                ThrowIfDisposed();
                ValidateFrameRate(value);
                m_FrameRate = value;
                FixedDeltaTime = 1f / value;
            }
        }

        public float FixedDeltaTime { get; private set; }

        public long Tick { get; private set; }

        public float Time { get; private set; }

        /// <summary>
        /// 可回滚帧数。
        /// </summary>
        public int CanRollbackFrames
        {
            get
            {
                ThrowIfDisposed();
                return m_World.CanRollbackFrames;
            }
        }

        /// <summary>
        /// 初始化战斗世界。
        /// </summary>
        /// <param name="frameRate">固定帧率。</param>
        public World(int frameRate = DefaultFrameRate)
        {
            ValidateFrameRate(frameRate);

            m_World = new MassiveWorld();
            m_Entities = new EntityManager(this, m_World);
            m_Systems = new SystemManager(this, m_World);
            m_FrameRate = frameRate;
            FixedDeltaTime = 1f / frameRate;
        }

        /// <summary>
        /// 查询实体是否属于当前世界且仍然存活。
        /// </summary>
        /// <param name="entity">实体句柄。</param>
        /// <returns>实体存活时返回 true。</returns>
        public bool IsAlive(Entity entity)
        {
            if (m_Disposed)
            {
                return false;
            }

            if (entity == null || !ReferenceEquals(entity.World, this))
            {
                return false;
            }

            return m_World.IsAlive(entity.Id) &&
                   m_World.IsAlive(new Entifier(entity.Id, entity.Version));
        }

        /// <summary>
        /// 创建实体。
        /// </summary>
        /// <returns>新创建的实体句柄。</returns>
        public Entity Create()
        {
            ThrowIfDisposed();
            return m_Entities.Create();
        }

        /// <summary>
        /// 按实体编号查找当前存活实体。
        /// </summary>
        /// <param name="id">实体编号。</param>
        /// <returns>实体存在且存活时返回实体句柄；否则返回 null。</returns>
        public Entity GetEntity(int id)
        {
            ThrowIfDisposed();
            return m_Entities.Find(id);
        }

        /// <summary>
        /// 销毁实体。
        /// </summary>
        /// <param name="entity">实体句柄。</param>
        /// <returns>实体成功销毁时返回 true。</returns>
        public bool Destroy(Entity entity)
        {
            ThrowIfDisposed();
            return m_Entities.Destroy(entity);
        }

        /// <summary>
        /// 创建并加载战斗系统。
        /// </summary>
        /// <typeparam name="TSystem">系统类型。</typeparam>
        /// <returns>加载后的系统实例。</returns>
        public TSystem LoadSystem<TSystem>() where TSystem : SystemBase, new()
        {
            ThrowIfDisposed();
            return m_Systems.Add<TSystem>();
        }

        /// <summary>
        /// 加载战斗系统实例。
        /// </summary>
        /// <param name="system">系统实例。</param>
        public void LoadSystem(SystemBase system)
        {
            ThrowIfDisposed();
            m_Systems.Add(system);
        }

        /// <summary>
        /// 卸载战斗系统实例。
        /// </summary>
        /// <param name="system">系统实例。</param>
        /// <returns>系统被卸载时返回 true。</returns>
        public bool UnloadSystem(SystemBase system)
        {
            ThrowIfDisposed();
            return m_Systems.Remove(system);
        }

        /// <summary>
        /// 按类型卸载战斗系统。
        /// </summary>
        /// <typeparam name="T">系统类型。</typeparam>
        /// <returns>系统被卸载时返回 true。</returns>
        public bool UnloadSystem<T>() where T : SystemBase
        {
            ThrowIfDisposed();
            return m_Systems.Remove<T>();
        }

        /// <summary>
        /// 遍历符合查询条件的实体。
        /// </summary>
        /// <param name="queryable">实体查询条件。</param>
        /// <returns>符合条件的实体集合。</returns>
        public IEnumerable<Entity> ForEach(Queryable queryable)
        {
            ThrowIfDisposed();
            if (queryable == null)
            {
                throw new ArgumentNullException(nameof(queryable));
            }

            return ForEach(SystemManager.CreateFilter(m_World, queryable));
        }

        /// <summary>
        /// 遍历符合 Massive 过滤器的实体。
        /// </summary>
        /// <param name="filter">Massive 查询过滤器。</param>
        /// <returns>符合条件的实体集合。</returns>
        internal IEnumerable<Entity> ForEach(Filter filter)
        {
            ThrowIfDisposed();
            foreach (var id in new Query(m_World, filter))
            {
                if (m_Entities.TryGetEntity(id, out var entity))
                {
                    yield return entity;
                }
            }
        }

        /// <summary>
        /// 添加默认组件。
        /// </summary>
        /// <param name="entity">实体句柄。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被添加。</returns>
        public bool AddComponent<TComponent>(Entity entity) where TComponent : ComponentBase, new()
        {
            ThrowIfDisposed();
            return m_Entities.AddComponent<TComponent>(entity);
        }

        /// <summary>
        /// 添加组件。
        /// </summary>
        /// <param name="entity">实体句柄。</param>
        /// <param name="component">组件实例。</param>
        /// <returns>组件是否被添加。</returns>
        public bool AddComponent(Entity entity, ComponentBase component)
        {
            ThrowIfDisposed();
            return m_Entities.AddComponent(entity, component);
        }

        /// <summary>
        /// 移除组件。
        /// </summary>
        /// <param name="entity">实体句柄。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被移除。</returns>
        public bool RemoveComponent<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            ThrowIfDisposed();
            return m_Entities.RemoveComponent<TComponent>(entity);
        }

        /// <summary>
        /// 查询组件是否存在。
        /// </summary>
        /// <param name="entity">实体句柄。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否存在。</returns>
        public bool HasComponent<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            ThrowIfDisposed();
            return m_Entities.HasComponent<TComponent>(entity);
        }

        /// <summary>
        /// 获取组件。
        /// </summary>
        /// <param name="entity">实体句柄。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件实例。</returns>
        public TComponent GetComponent<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            ThrowIfDisposed();
            return m_Entities.GetComponent<TComponent>(entity);
        }

        /// <summary>
        /// 按组件运行时类型查询实体是否持有组件。
        /// </summary>
        /// <param name="entity">实体句柄。</param>
        /// <param name="componentType">组件类型。</param>
        /// <returns>组件存在时返回 true。</returns>
        internal bool HasComponent(Entity entity, Type componentType)
        {
            ThrowIfDisposed();
            return m_Entities.HasComponent(entity, componentType);
        }

        /// <summary>
        /// 按真实时间推进战斗世界。
        /// </summary>
        /// <param name="deltaTime">真实帧时间。</param>
        public void Update(float deltaTime)
        {
            ThrowIfDisposed();
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Delta time cannot be negative.");
            }

            m_Accumulator += deltaTime;
            while (m_Accumulator >= FixedDeltaTime)
            {
                m_Accumulator -= FixedDeltaTime;
                Step();
            }
        }

        /// <summary>
        /// 推进一个固定帧。
        /// </summary>
        public void Step()
        {
            ThrowIfDisposed();
            Tick++;
            Time += FixedDeltaTime;
            List<Exception> stepExceptions = null;
            foreach (var registration in m_Systems.Registrations)
            {
                if (!registration.IsActive)
                {
                    continue;
                }

                try
                {
                    foreach (var id in new Query(m_World, registration.Filter))
                    {
                        if (!registration.IsActive)
                        {
                            break;
                        }

                        if (m_Entities.TryGetEntity(id, out var entity))
                        {
                            registration.System.OnUpdate(entity);
                        }
                    }
                }
                catch (Exception exception)
                {
                    stepExceptions ??= new List<Exception>();
                    stepExceptions.Add(exception);
                }
            }

            if (stepExceptions != null)
            {
                throw new AggregateException(
                    $"One or more combat systems threw exceptions during world step at tick {Tick}.",
                    stepExceptions);
            }
        }

        /// <summary>
        /// 保存当前帧。
        /// </summary>
        public void SaveFrame()
        {
            ThrowIfDisposed();
            m_World.SaveFrame();
        }

        /// <summary>
        /// 回滚指定帧数。
        /// </summary>
        /// <param name="frames">回滚帧数。</param>
        public void Rollback(int frames)
        {
            ThrowIfDisposed();
            var snapshot = m_Systems.Capture();
            m_World.Rollback(frames);
            m_Entities.Rebuild();
            m_Systems.NotifyChanged(snapshot);
        }

        /// <summary>
        /// 清理世界。
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            ClearCore();
        }

        /// <summary>
        /// 清理世界内部状态，不检查释放状态。
        /// </summary>
        private void ClearCore()
        {
            m_Systems.Clear();
            m_World.Clear();
            m_Entities.Clear();
            m_Accumulator = 0f;
            Tick = 0;
            Time = 0f;
        }

        /// <summary>
        /// 释放世界。
        /// </summary>
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            ClearCore();
            m_Disposed = true;
        }

        /// <summary>
        /// 世界已经释放时抛出异常。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new GameException("Combat world has been disposed.");
            }
        }

        /// <summary>
        /// 校验固定帧率必须大于零。
        /// </summary>
        /// <param name="frameRate">固定帧率。</param>
        private static void ValidateFrameRate(int frameRate)
        {
            if (frameRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameRate), frameRate, "World frame rate must be greater than zero.");
            }
        }

        /// <summary>
        /// 捕获实体变更前匹配的系统快照。
        /// </summary>
        /// <param name="entity">即将变更的实体。</param>
        /// <param name="changedComponentType">发生变更的组件类型。</param>
        /// <returns>系统注册项到变更前匹配状态的快照。</returns>
        internal Dictionary<SystemManager.Registration, bool> CaptureEntity(Entity entity, Type changedComponentType = null)
        {
            return m_Systems.Capture(entity, changedComponentType);
        }

        /// <summary>
        /// 通知系统实体组件集合已经变化。
        /// </summary>
        /// <param name="entity">已变化的实体。</param>
        /// <param name="snapshot">变更前的系统匹配快照。</param>
        internal void NotifyEntityChanged(Entity entity, Dictionary<SystemManager.Registration, bool> snapshot)
        {
            m_Systems.NotifyChanged(entity, snapshot);
        }

        /// <summary>
        /// 通知系统实体即将或已经销毁。
        /// </summary>
        /// <param name="entity">被销毁的实体。</param>
        /// <param name="snapshot">销毁前的系统匹配快照。</param>
        internal void NotifyEntityDestroyed(Entity entity, Dictionary<SystemManager.Registration, bool> snapshot)
        {
            m_Systems.NotifyDestroyed(entity, snapshot);
        }
    }
}

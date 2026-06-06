using System;
using System.Collections.Generic;
using Massive;
using MassiveWorld = Massive.MassiveWorld;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗世界。
    /// </summary>
    public sealed class World : IDisposable
    {
        /// <summary>
        /// 默认战斗世界帧率。
        /// </summary>
        public const int DefaultFrameRate = 50;

        private bool m_Disposed;
        private int m_FrameRate;
        private float m_Accumulator;
        private readonly MassiveWorld m_World;
        private readonly EntityManager m_Entities;
        private readonly SystemManager m_Systems;

        /// <summary>
        /// 固定帧率。
        /// </summary>
        public int FrameRate
        {
            get => m_FrameRate;
            set
            {
                ValidateFrameRate(value);
                m_FrameRate = value;
                FixedDeltaTime = 1f / value;
            }
        }

        /// <summary>
        /// 固定帧间隔。
        /// </summary>
        public float FixedDeltaTime { get; private set; }

        /// <summary>
        /// 当前固定帧计数。
        /// </summary>
        public long Tick { get; private set; }

        /// <summary>
        /// 当前固定时间。
        /// </summary>
        public float Time { get; private set; }

        /// <summary>
        /// 可回滚帧数。
        /// </summary>
        public int CanRollbackFrames => m_World.CanRollbackFrames;

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

        public bool IsAlive(Entity entity)
        {
            if (entity == null || !ReferenceEquals(entity.World, this))
            {
                return false;
            }

            return m_World.IsAlive(entity.Id) &&
                   m_World.IsAlive(new Entifier(entity.Id, entity.Version));
        }

        public Entity Create()
        {
            return m_Entities.Create();
        }

        public Entity GetEntity(int id)
        {
            return m_Entities.Find(id);
        }

        public bool Destroy(Entity entity)
        {
            return m_Entities.Destroy(entity);
        }

        public TSystem LoadSystem<TSystem>() where TSystem : SystemBase, new()
        {
            return m_Systems.Add<TSystem>();
        }

        public void LoadSystem(SystemBase system)
        {
            m_Systems.Add(system);
        }

        public bool UnloadSystem(SystemBase system)
        {
            return m_Systems.Remove(system);
        }

        public bool UnloadSystem<T>() where T : SystemBase
        {
            return m_Systems.Remove<T>();
        }

        public IEnumerable<Entity> ForEach(Queryable queryable)
        {
            if (queryable == null)
            {
                throw new ArgumentNullException(nameof(queryable));
            }

            return ForEach(SystemManager.CreateFilter(m_World, queryable));
        }

        internal IEnumerable<Entity> ForEach(Filter filter)
        {
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
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被添加。</returns>
        public bool AddComponent<TComponent>(Entity entity) where TComponent : ComponentBase, new()
        {
            return m_Entities.AddComponent<TComponent>(entity);
        }

        /// <summary>
        /// 添加组件
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="component">组件实例</param>
        /// <returns></returns>
        public bool AddComponent(Entity entity, ComponentBase component)
        {
            return m_Entities.AddComponent(entity, component);
        }

        /// <summary>
        /// 移除组件。
        /// </summary>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被移除。</returns>
        public bool RemoveComponent<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            return m_Entities.RemoveComponent<TComponent>(entity);
        }

        /// <summary>
        /// 查询组件是否存在。
        /// </summary>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否存在。</returns>
        public bool HasComponent<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            return m_Entities.HasComponent<TComponent>(entity);
        }

        /// <summary>
        /// 获取组件。
        /// </summary>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件实例。</returns>
        public TComponent GetComponent<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            return m_Entities.GetComponent<TComponent>(entity);
        }

        internal bool HasComponent(Entity entity, Type componentType)
        {
            return m_Entities.HasComponent(entity, componentType);
        }

        /// <summary>
        /// 按真实时间推进战斗世界。
        /// </summary>
        /// <param name="deltaTime">真实帧时间。</param>
        public void Update(float deltaTime)
        {
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
            Tick++;
            Time += FixedDeltaTime;
            foreach (var registration in m_Systems.Registrations)
            {
                foreach (var entity in ForEach(registration.Filter))
                {
                    registration.System.OnUpdate(entity);
                }
            }
        }

        /// <summary>
        /// 保存当前帧。
        /// </summary>
        public void SaveFrame()
        {
            m_World.SaveFrame();
        }

        /// <summary>
        /// 回滚指定帧数。
        /// </summary>
        /// <param name="frames">回滚帧数。</param>
        public void Rollback(int frames)
        {
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

            Clear();
            m_Disposed = true;
        }

        private static void ValidateFrameRate(int frameRate)
        {
            if (frameRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameRate), frameRate, "World frame rate must be greater than zero.");
            }
        }

        internal Dictionary<SystemManager.Registration, bool> CaptureEntity(Entity entity)
        {
            return m_Systems.Capture(entity);
        }

        internal void NotifyEntityChanged(Entity entity, Dictionary<SystemManager.Registration, bool> snapshot)
        {
            m_Systems.NotifyChanged(entity, snapshot);
        }

        internal void NotifyEntityDestroyed(Entity entity, Dictionary<SystemManager.Registration, bool> snapshot)
        {
            m_Systems.NotifyDestroyed(entity, snapshot);
        }
    }
}

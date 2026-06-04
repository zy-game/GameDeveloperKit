using System;
using System.Collections.Generic;
using Massive;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗实体管理器。
    /// </summary>
    public sealed class EntityManager
    {
        private readonly World m_World;
        private readonly Dictionary<long, Entity> m_Entities = new Dictionary<long, Entity>();

        internal EntityManager(World world)
        {
            m_World = world ?? throw new ArgumentNullException(nameof(world));
        }

        internal IEnumerable<Entity> AliveEntities
        {
            get
            {
                var enumerator = m_World.MassiveWorld.Entities.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                    {
                        var entity = enumerator.Current;
                        yield return GetOrCreate(entity.Id, entity.Version);
                    }
                }
                finally
                {
                    enumerator.Dispose();
                }
            }
        }

        /// <summary>
        /// 创建实体。
        /// </summary>
        /// <returns>实体句柄。</returns>
        public Entity Create()
        {
            var entifier = m_World.MassiveWorld.Entities.Create();
            var entity = GetOrCreate(entifier.Id, entifier.Version);
            m_World.SystemManager.Refresh(entity);
            return entity;
        }

        /// <summary>
        /// 销毁实体。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <returns>实体是否被销毁。</returns>
        public bool Destroy(Entity entity)
        {
            ValidateEntityWorld(entity);
            if (!IsAlive(entity))
            {
                return false;
            }

            m_World.SystemManager.RemoveEntity(entity);
            return m_World.MassiveWorld.Destroy(entity.Id);
        }

        /// <summary>
        /// 查询实体是否存活。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <returns>实体是否存活。</returns>
        public bool IsAlive(Entity entity)
        {
            return entity != null && ReferenceEquals(entity.World, m_World) &&
                   m_World.MassiveWorld.Entities.IsAlive(entity.Id) &&
                   m_World.MassiveWorld.Entities.IsAlive(new Entifier(entity.Id, entity.Version));
        }

        /// <summary>
        /// 设置组件。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <param name="component">组件实例。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        public void Set<TComponent>(Entity entity, TComponent component) where TComponent : ComponentBase
        {
            ValidateEntityAlive(entity);
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            m_World.MassiveWorld.Set(entity.Id, component);
            m_World.SystemManager.Refresh(entity);
        }

        /// <summary>
        /// 添加默认组件。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被添加。</returns>
        public bool Add<TComponent>(Entity entity) where TComponent : ComponentBase, new()
        {
            ValidateEntityAlive(entity);
            if (m_World.MassiveWorld.Has<TComponent>(entity.Id))
            {
                return false;
            }

            m_World.MassiveWorld.Set(entity.Id, new TComponent());
            m_World.SystemManager.Refresh(entity);
            return true;
        }

        /// <summary>
        /// 移除组件。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被移除。</returns>
        public bool Remove<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            ValidateEntityAlive(entity);
            var removed = m_World.MassiveWorld.Remove<TComponent>(entity.Id);
            if (removed)
            {
                m_World.SystemManager.Refresh(entity);
            }

            return removed;
        }

        /// <summary>
        /// 查询组件是否存在。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否存在。</returns>
        public bool Has<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            ValidateEntityAlive(entity);
            return m_World.MassiveWorld.Has<TComponent>(entity.Id);
        }

        /// <summary>
        /// 获取组件。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件实例。</returns>
        public TComponent Get<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            ValidateEntityAlive(entity);
            return m_World.MassiveWorld.Get<TComponent>(entity.Id);
        }

        internal bool Has(Entity entity, ComponentType componentType)
        {
            ValidateEntityAlive(entity);
            ValidateComponentType(componentType);
            return m_World.MassiveWorld.Sets.GetReflected(componentType.Type).Has(entity.Id);
        }

        internal void Rebuild()
        {
            foreach (var _ in AliveEntities)
            {
            }
        }

        internal void Clear()
        {
            m_Entities.Clear();
        }

        internal void ValidateEntityWorld(Entity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!ReferenceEquals(entity.World, m_World))
            {
                throw new GameException("Entity does not belong to this world.");
            }
        }

        private Entity GetOrCreate(int id, uint version)
        {
            var key = GetKey(id, version);
            if (m_Entities.TryGetValue(key, out var entity))
            {
                return entity;
            }

            entity = new Entity(m_World, id, version);
            m_Entities.Add(key, entity);
            return entity;
        }

        private void ValidateEntityAlive(Entity entity)
        {
            ValidateEntityWorld(entity);
            if (!IsAlive(entity))
            {
                throw new GameException($"Entity '{entity}' is not alive.");
            }
        }

        private static void ValidateComponentType(ComponentType componentType)
        {
            if (componentType.Type == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }
        }

        private static long GetKey(int id, uint version)
        {
            return (long)id | ((long)version << 32);
        }
    }
}

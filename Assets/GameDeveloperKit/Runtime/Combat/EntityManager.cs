using System;
using System.Collections.Generic;
using System.Linq;
using Massive;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗实体管理器。
    /// </summary>
    public sealed class EntityManager
    {
        private readonly World m_World;
        private readonly MassiveWorld m_MassiveWorld;
        private readonly Dictionary<long, Entity> m_Entities = new Dictionary<long, Entity>();

        internal EntityManager(World world, MassiveWorld massiveWorld)
        {
            m_World = world ?? throw new ArgumentNullException(nameof(world));
            m_MassiveWorld = massiveWorld ?? throw new ArgumentNullException(nameof(massiveWorld));
        }

        /// <summary>
        /// 获取还存活的实体。
        /// </summary>
        public IEnumerable<Entity> AliveEntities => m_Entities.Values.Where(x => x.IsAlive);

        /// <summary>
        /// 创建实体。
        /// </summary>
        /// <returns>实体句柄。</returns>
        public Entity Create()
        {
            var entity = GetOrCreate(m_MassiveWorld.Entities.Create());
            m_World.NotifyEntityChanged(entity, null);
            return entity;
        }

        public Entity Find(int id)
        {
            if (!m_MassiveWorld.IsAlive(id))
            {
                return null;
            }

            return GetOrCreate(m_MassiveWorld.Entities.GetEntifier(id));
        }

        public bool TryGetEntity(long id, out Entity entity)
        {
            if (!m_MassiveWorld.IsAlive((int)id))
            {
                entity = null;
                return false;
            }

            entity = GetOrCreate(m_MassiveWorld.Entities.GetEntifier((int)id));
            return true;
        }

        /// <summary>
        /// 销毁实体。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <returns>实体是否被销毁。</returns>
        public bool Destroy(Entity entity)
        {
            ValidateEntityWorld(entity);
            if (!entity.IsAlive)
            {
                return false;
            }

            var snapshot = m_World.CaptureEntity(entity);
            m_World.NotifyEntityDestroyed(entity, snapshot);
            if (!m_MassiveWorld.Destroy(entity.Id))
            {
                return false;
            }

            m_Entities.Remove(GetKey(entity.Id, entity.Version));
            return true;
        }

        /// <summary>
        /// 添加默认组件。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被添加。</returns>
        public bool AddComponent<TComponent>(Entity entity) where TComponent : ComponentBase, new()
        {
            ValidateEntityAlive(entity);
            if (m_MassiveWorld.Has<TComponent>(entity.Id))
            {
                return false;
            }

            var snapshot = m_World.CaptureEntity(entity, typeof(TComponent));
            m_MassiveWorld.Set(entity.Id, new TComponent());
            m_World.NotifyEntityChanged(entity, snapshot);
            return true;
        }

        /// <summary>
        /// 添加组件。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <param name="instance">组件实例。</param>
        /// <returns>组件是否被添加。</returns>
        public bool AddComponent(Entity entity, ComponentBase instance)
        {
            ValidateEntityAlive(entity);
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var componentType = instance.GetType();
            if (HasComponent(entity, componentType))
            {
                return false;
            }

            var snapshot = m_World.CaptureEntity(entity, componentType);
            var dataSet = GetComponentDataSet(componentType);
            dataSet.SetRaw(entity.Id, instance);
            m_World.NotifyEntityChanged(entity, snapshot);
            return true;
        }

        /// <summary>
        /// 移除组件。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被移除。</returns>
        public bool RemoveComponent<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            ValidateEntityAlive(entity);
            if (!m_MassiveWorld.Has<TComponent>(entity.Id))
            {
                return false;
            }

            var snapshot = m_World.CaptureEntity(entity, typeof(TComponent));
            var removed = m_MassiveWorld.Remove<TComponent>(entity.Id);
            if (removed)
            {
                m_World.NotifyEntityChanged(entity, snapshot);
            }

            return removed;
        }

        /// <summary>
        /// 查询组件是否存在。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否存在。</returns>
        public bool HasComponent<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            ValidateEntityAlive(entity);
            return m_MassiveWorld.Has<TComponent>(entity.Id);
        }

        /// <summary>
        /// 获取组件。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件实例。</returns>
        public TComponent GetComponent<TComponent>(Entity entity) where TComponent : ComponentBase
        {
            ValidateEntityAlive(entity);
            if (!m_MassiveWorld.Has<TComponent>(entity.Id))
            {
                throw new GameException($"Entity '{entity}' does not have component '{typeof(TComponent).Name}'.");
            }

            return m_MassiveWorld.Get<TComponent>(entity.Id);
        }

        /// <summary>
        /// 是否存在组件。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <param name="componentType">组件类型。</param>
        /// <returns>组件是否存在。</returns>
        public bool HasComponent(Entity entity, Type componentType)
        {
            ValidateEntityAlive(entity);
            if (componentType == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }

            var dataSet = GetComponentDataSet(componentType);
            return dataSet.BitSet.Has(entity.Id);
        }

        /// <summary>
        /// 清理所有实体。
        /// </summary>
        public void Clear()
        {
            m_Entities.Clear();
        }

        internal void Rebuild()
        {
            var stale = new List<long>();
            foreach (var item in m_Entities)
            {
                var entity = item.Value;
                if (!m_MassiveWorld.IsAlive(entity.Id) ||
                    m_MassiveWorld.Entities.Versions[entity.Id] != entity.Version)
                {
                    stale.Add(item.Key);
                }
            }

            foreach (var id in stale)
            {
                m_Entities.Remove(id);
            }

            foreach (var massiveEntity in m_MassiveWorld.Entities)
            {
                GetOrCreate(m_MassiveWorld.Entities.GetEntifier(massiveEntity.Id));
            }
        }

        internal void ValidateEntityWorld(Entity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!ReferenceEquals(entity.World, m_World))
            {
                throw new GameException("Entity belongs to another combat world.");
            }
        }

        private Entity GetOrCreate(Entifier entifier)
        {
            var key = GetKey(entifier.Id, entifier.Version);
            if (m_Entities.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var entity = new Entity(m_World, entifier);
            m_Entities[key] = entity;
            return entity;
        }

        private void ValidateEntityAlive(Entity entity)
        {
            ValidateEntityWorld(entity);
            if (!entity.IsAlive)
            {
                throw new GameException("Entity is not alive.");
            }
        }

        private IDataSet GetComponentDataSet(Type componentType)
        {
            ValidateComponentType(componentType);
            var dataSet = m_MassiveWorld.Sets.GetReflected(componentType) as IDataSet;
            if (dataSet == null)
            {
                throw new GameException($"Component type '{componentType.Name}' has no data set.");
            }

            return dataSet;
        }

        private static void ValidateComponentType(Type componentType)
        {
            if (componentType == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }

            if (!typeof(ComponentBase).IsAssignableFrom(componentType))
            {
                throw new ArgumentException($"Component type '{componentType.Name}' must inherit ComponentBase.", nameof(componentType));
            }
        }

        private static long GetKey(int id, uint version)
        {
            return (long)id | ((long)version << 32);
        }
    }
}

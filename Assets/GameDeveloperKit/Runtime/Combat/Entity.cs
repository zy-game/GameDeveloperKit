using System;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗实体句柄。
    /// </summary>
    public sealed class Entity : IEquatable<Entity>
    {
        private readonly World m_World;
        private readonly Massive.Entifier m_Entity;

        internal Entity(World world, Massive.Entifier entifier)
        {
            m_World = world;
            m_Entity = entifier;
        }

        /// <summary>
        /// 实体编号。
        /// </summary>
        public int Id => m_Entity.Id;

        /// <summary>
        /// 实体版本。
        /// </summary>
        public uint Version => m_Entity.Version;

        /// <summary>
        /// 实体是否存活。
        /// </summary>
        public bool IsAlive => m_World != null && m_World.IsAlive(this);

        internal World World => m_World;

        /// <summary>
        /// 添加默认组件。
        /// </summary>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被添加。</returns>
        public bool AddComponent<TComponent>() where TComponent : ComponentBase, new()
        {
            return m_World.AddComponent<TComponent>(this);
        }

        /// <summary>
        /// 添加默认组件。
        /// </summary>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被添加。</returns>
        public bool AddComponent<TComponent>(TComponent component) where TComponent : ComponentBase
        {
            return m_World.AddComponent(this, component);
        }

        /// <summary>
        /// 移除组件。
        /// </summary>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否被移除。</returns>
        public bool RemoveComponent<TComponent>() where TComponent : ComponentBase
        {
            return m_World.RemoveComponent<TComponent>(this);
        }

        /// <summary>
        /// 查询组件是否存在。
        /// </summary>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件是否存在。</returns>
        public bool HasComponent<TComponent>() where TComponent : ComponentBase
        {
            return m_World.HasComponent<TComponent>(this);
        }

        /// <summary>
        /// 获取组件。
        /// </summary>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件实例。</returns>
        public TComponent GetComponent<TComponent>() where TComponent : ComponentBase
        {
            return m_World.GetComponent<TComponent>(this);
        }

        /// <inheritdoc />
        public bool Equals(Entity other)
        {
            return other != null && Id == other.Id && Version == other.Version && ReferenceEquals(m_World, other.m_World);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Entity other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + Id;
                hash = hash * 31 + (int)Version;
                hash = hash * 31 + (m_World != null ? m_World.GetHashCode() : 0);
                return hash;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"(id:{Id} v:{Version})";
        }
    }
}

using System;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗组件类型描述。
    /// </summary>
    public readonly struct ComponentType : IEquatable<ComponentType>
    {
        private ComponentType(Type type)
        {
            Type = type;
        }

        /// <summary>
        /// 组件类型。
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// 创建组件类型描述。
        /// </summary>
        /// <typeparam name="TComponent">组件类型。</typeparam>
        /// <returns>组件类型描述。</returns>
        public static ComponentType Of<TComponent>() where TComponent : ComponentBase
        {
            return From(typeof(TComponent));
        }

        /// <summary>
        /// 创建组件类型描述。
        /// </summary>
        /// <param name="type">组件类型。</param>
        /// <returns>组件类型描述。</returns>
        public static ComponentType From(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!typeof(ComponentBase).IsAssignableFrom(type))
            {
                throw new GameException($"Component type '{type.FullName}' must inherit ComponentBase.");
            }

            if (type.IsAbstract || type.ContainsGenericParameters)
            {
                throw new GameException($"Component type '{type.FullName}' must be a concrete ComponentBase type.");
            }

            return new ComponentType(type);
        }

        /// <inheritdoc />
        public bool Equals(ComponentType other)
        {
            return Type == other.Type;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ComponentType other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Type != null ? Type.GetHashCode() : 0;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Type != null ? Type.Name : "<null>";
        }

        public static bool operator ==(ComponentType left, ComponentType right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ComponentType left, ComponentType right)
        {
            return !left.Equals(right);
        }
    }
}

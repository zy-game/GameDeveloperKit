using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗实体查询条件。
    /// </summary>
    public sealed class Queryable
    {
        /// <summary>
        /// 匹配所有存活实体。
        /// </summary>
        public static Queryable All { get; } = new Queryable(Array.Empty<Type>(), Array.Empty<Type>());

        /// <summary>
        /// 必须包含的组件类型。
        /// </summary>
        public Type[] Included { get; }

        /// <summary>
        /// 必须不包含的组件类型。
        /// </summary>
        public Type[] Excluded { get; }

        /// <summary>
        /// 初始化查询条件。
        /// </summary>
        /// <param name="included">必须包含的组件类型。</param>
        public Queryable(params Type[] included)
            : this(included, Array.Empty<Type>())
        {
        }

        /// <summary>
        /// 初始化查询条件。
        /// </summary>
        /// <param name="included">必须包含的组件类型。</param>
        /// <param name="excluded">必须不包含的组件类型。</param>
        public Queryable(Type[] included, Type[] excluded)
        {
            Included = Normalize(included, nameof(included));
            Excluded = Normalize(excluded, nameof(excluded));
            ValidateNoConflict(Included, Excluded);
        }

        /// <summary>
        /// 校验、去重并复制组件类型数组。
        /// </summary>
        /// <param name="componentTypes">组件类型数组。</param>
        /// <param name="name">参数名。</param>
        /// <returns>规范化后的组件类型数组。</returns>
        private static Type[] Normalize(Type[] componentTypes, string name)
        {
            if (componentTypes == null)
            {
                throw new ArgumentNullException(name);
            }

            if (componentTypes.Length == 0)
            {
                return Array.Empty<Type>();
            }

            var result = new List<Type>(componentTypes.Length);
            var seen = new HashSet<Type>();
            foreach (var componentType in componentTypes)
            {
                if (componentType == null)
                {
                    throw new ArgumentNullException(name, "Component type cannot be null.");
                }

                if (!typeof(ComponentBase).IsAssignableFrom(componentType))
                {
                    throw new ArgumentException($"Component type '{componentType.Name}' must inherit ComponentBase.", name);
                }

                if (seen.Add(componentType))
                {
                    result.Add(componentType);
                }
            }

            return result.Count == 0 ? Array.Empty<Type>() : result.ToArray();
        }

        /// <summary>
        /// 校验包含组件集合与排除组件集合不能有交集。
        /// </summary>
        /// <param name="included">必须包含的组件类型。</param>
        /// <param name="excluded">必须不包含的组件类型。</param>
        private static void ValidateNoConflict(Type[] included, Type[] excluded)
        {
            if (included.Length == 0 || excluded.Length == 0)
            {
                return;
            }

            var includeSet = new HashSet<Type>(included);
            foreach (var componentType in excluded)
            {
                if (includeSet.Contains(componentType))
                {
                    throw new GameException($"Query cannot include and exclude component '{componentType.Name}'.");
                }
            }
        }
    }
}

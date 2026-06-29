using System;
using System.Collections.Generic;
using Massive;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗系统管理器的注册与查询过滤分部。
    /// </summary>
    public sealed partial class SystemManager
    {
        /// <summary>
        /// 把组件类型数组解析为 Massive 位集合数组。
        /// </summary>
        /// <param name="world">底层 Massive 世界实例。</param>
        /// <param name="componentTypes">组件类型数组。</param>
        /// <returns>Massive 位集合数组。</returns>
        public static BitSet[] ResolveSets(MassiveWorld world, Type[] componentTypes)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (componentTypes == null || componentTypes.Length == 0)
            {
                return Array.Empty<BitSet>();
            }

            var bitSets = new BitSet[componentTypes.Length];
            for (var i = 0; i < componentTypes.Length; i++)
            {
                if (componentTypes[i] == null)
                {
                    throw new ArgumentNullException(nameof(componentTypes), "Component type cannot be null.");
                }

                ValidateComponentType(componentTypes[i]);
                bitSets[i] = world.Sets.GetReflected(componentTypes[i]);
            }

            return bitSets;
        }

        /// <summary>
        /// 根据实体查询条件创建 Massive 查询过滤器。
        /// </summary>
        /// <param name="world">底层 Massive 世界实例。</param>
        /// <param name="queryable">实体查询条件。</param>
        /// <returns>Massive 查询过滤器。</returns>
        internal static Filter CreateFilter(MassiveWorld world, Queryable queryable)
        {
            queryable ??= Queryable.All;
            return new Filter(
                ResolveSets(world, queryable.Included),
                ResolveSets(world, queryable.Excluded));
        }

        /// <summary>
        /// 系统注册记录，缓存系统实例、查询条件和组件索引信息。
        /// </summary>
        internal sealed class Registration
        {
            /// <summary>
            /// 初始化系统注册记录。
            /// </summary>
            /// <param name="system">系统实例。</param>
            /// <param name="world">底层 Massive 世界实例。</param>
            public Registration(SystemBase system, MassiveWorld world)
            {
                System = system ?? throw new ArgumentNullException(nameof(system));
                SystemType = system.GetType();
                Query = system.Query ?? Queryable.All;
                Filter = CreateFilter(world, Query);
                ComponentTypes = ResolveComponentTypes(Query);
            }

            /// <summary>
            /// 系统运行时类型。
            /// </summary>
            public Type SystemType { get; }

            /// <summary>
            /// 系统实例。
            /// </summary>
            public SystemBase System { get; }

            /// <summary>
            /// 系统实体查询条件。
            /// </summary>
            public Queryable Query { get; }

            /// <summary>
            /// 系统对应的 Massive 查询过滤器。
            /// </summary>
            public Filter Filter { get; }

            /// <summary>
            /// 系统查询涉及的全部组件类型。
            /// </summary>
            public Type[] ComponentTypes { get; }

            /// <summary>
            /// 注册记录是否仍然活跃。
            /// </summary>
            public bool IsActive { get; set; } = true;

            /// <summary>
            /// 查询实体是否匹配当前系统。
            /// </summary>
            /// <param name="entity">实体句柄。</param>
            /// <returns>实体匹配系统查询条件时返回 true。</returns>
            public bool Matches(Entity entity)
            {
                if (entity == null || !entity.IsAlive)
                {
                    return false;
                }

                var world = entity.World;
                for (var i = 0; i < Query.Included.Length; i++)
                {
                    if (!world.HasComponent(entity, Query.Included[i]))
                    {
                        return false;
                    }
                }

                for (var i = 0; i < Query.Excluded.Length; i++)
                {
                    if (world.HasComponent(entity, Query.Excluded[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// 解析查询条件中涉及的全部组件类型。
        /// </summary>
        /// <param name="query">实体查询条件。</param>
        /// <returns>去重后的组件类型数组。</returns>
        private static Type[] ResolveComponentTypes(Queryable query)
        {
            var result = new List<Type>();
            var seen = new HashSet<Type>();
            AddComponentTypes(query.Included, result, seen);
            AddComponentTypes(query.Excluded, result, seen);
            return result.Count == 0 ? Array.Empty<Type>() : result.ToArray();
        }

        /// <summary>
        /// 把组件类型追加到结果集合并去重。
        /// </summary>
        /// <param name="source">源组件类型数组。</param>
        /// <param name="result">结果组件类型列表。</param>
        /// <param name="seen">已经加入的组件类型集合。</param>
        private static void AddComponentTypes(Type[] source, List<Type> result, HashSet<Type> seen)
        {
            for (var i = 0; i < source.Length; i++)
            {
                if (seen.Add(source[i]))
                {
                    result.Add(source[i]);
                }
            }
        }

        /// <summary>
        /// 校验组件类型必须继承 <see cref="ComponentBase"/>。
        /// </summary>
        /// <param name="componentType">组件类型。</param>
        private static void ValidateComponentType(Type componentType)
        {
            if (!typeof(ComponentBase).IsAssignableFrom(componentType))
            {
                throw new ArgumentException($"Component type '{componentType.Name}' must inherit ComponentBase.", nameof(componentType));
            }
        }
    }
}

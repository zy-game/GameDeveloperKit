using System;
using System.Collections.Generic;
using Massive;

namespace GameDeveloperKit.Combat
{
    public sealed partial class SystemManager
    {
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

        internal static Filter CreateFilter(MassiveWorld world, Queryable queryable)
        {
            queryable ??= Queryable.All;
            return new Filter(
                ResolveSets(world, queryable.Included),
                ResolveSets(world, queryable.Excluded));
        }

        internal sealed class Registration
        {
            public Registration(SystemBase system, MassiveWorld world)
            {
                System = system ?? throw new ArgumentNullException(nameof(system));
                SystemType = system.GetType();
                Query = system.Query ?? Queryable.All;
                Filter = CreateFilter(world, Query);
                ComponentTypes = ResolveComponentTypes(Query);
            }

            public Type SystemType { get; }

            public SystemBase System { get; }

            public Queryable Query { get; }

            public Filter Filter { get; }

            public Type[] ComponentTypes { get; }

            public bool IsActive { get; set; } = true;

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

        private static Type[] ResolveComponentTypes(Queryable query)
        {
            var result = new List<Type>();
            var seen = new HashSet<Type>();
            AddComponentTypes(query.Included, result, seen);
            AddComponentTypes(query.Excluded, result, seen);
            return result.Count == 0 ? Array.Empty<Type>() : result.ToArray();
        }

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

        private static void ValidateComponentType(Type componentType)
        {
            if (!typeof(ComponentBase).IsAssignableFrom(componentType))
            {
                throw new ArgumentException($"Component type '{componentType.Name}' must inherit ComponentBase.", nameof(componentType));
            }
        }
    }
}

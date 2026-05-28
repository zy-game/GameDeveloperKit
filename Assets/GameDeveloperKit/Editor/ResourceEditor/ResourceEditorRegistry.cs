using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor
{
    public sealed class ResourceCollectorDescriptor
    {
        public ResourceCollectorDescriptor(string id, string displayName, string description, int order, Type type, ResourceCollector instance)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Order = order;
            Type = type;
            Instance = instance;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public int Order { get; }

        public Type Type { get; }

        public ResourceCollector Instance { get; }
    }

    public sealed class ResourceBuildStrategyDescriptor
    {
        public ResourceBuildStrategyDescriptor(string id, string displayName, string description, int order, Type type, ResourceBuildStrategy instance)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Order = order;
            Type = type;
            Instance = instance;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public int Order { get; }

        public Type Type { get; }

        public ResourceBuildStrategy Instance { get; }
    }

    public sealed class ResourceCheckerDescriptor
    {
        public ResourceCheckerDescriptor(string id, string displayName, int order, Type type, ResourceChecker instance)
        {
            Id = id;
            DisplayName = displayName;
            Order = order;
            Type = type;
            Instance = instance;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public int Order { get; }

        public Type Type { get; }

        public ResourceChecker Instance { get; }
    }

    public sealed class ResourceEditorRegistry
    {
        private readonly List<ResourceCollectorDescriptor> m_Collectors = new List<ResourceCollectorDescriptor>();
        private readonly List<ResourceBuildStrategyDescriptor> m_BuildStrategies = new List<ResourceBuildStrategyDescriptor>();
        private readonly List<ResourceCheckerDescriptor> m_Checkers = new List<ResourceCheckerDescriptor>();
        private readonly List<string> m_Errors = new List<string>();

        public IReadOnlyList<ResourceCollectorDescriptor> Collectors => m_Collectors;

        public IReadOnlyList<ResourceBuildStrategyDescriptor> BuildStrategies => m_BuildStrategies;

        public IReadOnlyList<ResourceCheckerDescriptor> Checkers => m_Checkers;

        public IReadOnlyList<string> Errors => m_Errors;

        public static ResourceEditorRegistry Scan()
        {
            var registry = new ResourceEditorRegistry();
            registry.ScanTypes();
            return registry;
        }

        public ResourceCollectorDescriptor GetCollector(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return m_Collectors.FirstOrDefault();
            }

            return m_Collectors.FirstOrDefault(x => x.Id == id);
        }

        public ResourceBuildStrategyDescriptor GetBuildStrategy(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return m_BuildStrategies.FirstOrDefault();
            }

            return m_BuildStrategies.FirstOrDefault(x => x.Id == id);
        }

        private void ScanTypes()
        {
            m_Collectors.Clear();
            m_BuildStrategies.Clear();
            m_Checkers.Clear();
            m_Errors.Clear();

            foreach (var type in GetEditorTypes())
            {
                TryRegisterCollector(type);
                TryRegisterBuildStrategy(type);
                TryRegisterChecker(type);
            }

            m_Collectors.Sort((a, b) => CompareDescriptor(a.Order, a.DisplayName, b.Order, b.DisplayName));
            m_BuildStrategies.Sort((a, b) => CompareDescriptor(a.Order, a.DisplayName, b.Order, b.DisplayName));
            m_Checkers.Sort((a, b) => CompareDescriptor(a.Order, a.DisplayName, b.Order, b.DisplayName));
        }

        private static int CompareDescriptor(int leftOrder, string leftName, int rightOrder, string rightName)
        {
            var order = leftOrder.CompareTo(rightOrder);
            return order != 0 ? order : string.Compare(leftName, rightName, StringComparison.Ordinal);
        }

        private static IEnumerable<Type> GetEditorTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(x => x != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.ContainsGenericParameters)
                    {
                        continue;
                    }

                    yield return type;
                }
            }
        }

        private void TryRegisterCollector(Type type)
        {
            var attribute = type.GetCustomAttribute<ColletionAttribute>();
            if (attribute == null)
            {
                return;
            }

            if (typeof(ResourceCollector).IsAssignableFrom(type) is false)
            {
                m_Errors.Add($"{type.FullName} has ColletionAttribute but does not inherit ResourceCollector.");
                return;
            }

            if (TryCreate(type, out ResourceCollector instance) is false)
            {
                return;
            }

            if (m_Collectors.Any(x => x.Id == attribute.Id))
            {
                m_Errors.Add($"Duplicate collector id: {attribute.Id}");
                return;
            }

            m_Collectors.Add(new ResourceCollectorDescriptor(attribute.Id, attribute.DisplayName, attribute.Description, attribute.Order, type, instance));
        }

        private void TryRegisterBuildStrategy(Type type)
        {
            var attribute = type.GetCustomAttribute<BuildedAttribute>();
            if (attribute == null)
            {
                return;
            }

            if (typeof(ResourceBuildStrategy).IsAssignableFrom(type) is false)
            {
                m_Errors.Add($"{type.FullName} has BuildedAttribute but does not inherit ResourceBuildStrategy.");
                return;
            }

            if (TryCreate(type, out ResourceBuildStrategy instance) is false)
            {
                return;
            }

            if (m_BuildStrategies.Any(x => x.Id == attribute.Id))
            {
                m_Errors.Add($"Duplicate build strategy id: {attribute.Id}");
                return;
            }

            m_BuildStrategies.Add(new ResourceBuildStrategyDescriptor(attribute.Id, attribute.DisplayName, attribute.Description, attribute.Order, type, instance));
        }

        private void TryRegisterChecker(Type type)
        {
            if (typeof(ResourceChecker).IsAssignableFrom(type) is false)
            {
                return;
            }

            if (TryCreate(type, out ResourceChecker instance) is false)
            {
                return;
            }

            var id = type.FullName;
            var displayName = ObjectNames.NicifyVariableName(type.Name.Replace("Checker", string.Empty));
            m_Checkers.Add(new ResourceCheckerDescriptor(id, displayName, 0, type, instance));
        }

        private bool TryCreate<T>(Type type, out T instance) where T : class
        {
            try
            {
                instance = Activator.CreateInstance(type) as T;
                if (instance == null)
                {
                    m_Errors.Add($"Unable to create instance of {type.FullName}.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                instance = null;
                m_Errors.Add($"{type.FullName} failed to initialize: {exception.Message}");
                return false;
            }
        }
    }

    [InitializeOnLoad]
    public static class ResourceEditorRegistryCache
    {
        static ResourceEditorRegistryCache()
        {
            Refresh();
        }

        public static ResourceEditorRegistry Current { get; private set; }

        public static ResourceEditorRegistry Refresh()
        {
            Current = ResourceEditorRegistry.Scan();
            return Current;
        }
    }
}

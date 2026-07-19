using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor.Registry
{
    /// <summary>
    /// 定义 Resource Collector Descriptor 类型。
    /// </summary>
    public sealed class CollectorDescriptor
    {
        /// <summary>
        /// 初始化 Resource Collector Descriptor。
        /// </summary>
        /// <param name="id">id 参数。</param>
        /// <param name="displayName">display Name 参数。</param>
        /// <param name="description">description 参数。</param>
        /// <param name="order">order 参数。</param>
        /// <param name="type">type 参数。</param>
        /// <param name="instance">instance 参数。</param>
        public CollectorDescriptor(string id, string displayName, string description, int order, Type type, Collector instance)
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

        public Collector Instance { get; }
    }

    public sealed class FilterRuleDescriptor
    {
        public FilterRuleDescriptor(string id, string displayName, string description, int order, Type type, FilterRule instance)
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

        public FilterRule Instance { get; }
    }

    public sealed class PackRuleDescriptor
    {
        public PackRuleDescriptor(string id, string displayName, string description, int order, Type type, PackRule instance)
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

        public PackRule Instance { get; }
    }

    /// <summary>
    /// 定义 Resource Checker Descriptor 类型。
    /// </summary>
    public sealed class CheckerDescriptor
    {
        /// <summary>
        /// 初始化 Resource Checker Descriptor。
        /// </summary>
        /// <param name="id">id 参数。</param>
        /// <param name="displayName">display Name 参数。</param>
        /// <param name="order">order 参数。</param>
        /// <param name="type">type 参数。</param>
        /// <param name="instance">instance 参数。</param>
        public CheckerDescriptor(string id, string displayName, int order, Type type, GameDeveloperKit.ResourceEditor.Validation.Checker instance)
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

        public GameDeveloperKit.ResourceEditor.Validation.Checker Instance { get; }
    }

    /// <summary>
    /// 定义 Resource Editor Registry 类型。
    /// </summary>
    public sealed class ExtensionRegistry
    {
        /// <summary>         /// 存储 Collectors。         /// </summary>
        private readonly List<CollectorDescriptor> m_Collectors = new List<CollectorDescriptor>();
        private readonly List<FilterRuleDescriptor> m_FilterRules = new List<FilterRuleDescriptor>();
        private readonly List<PackRuleDescriptor> m_PackRules = new List<PackRuleDescriptor>();
        /// <summary>         /// 存储 Checkers。         /// </summary>
        private readonly List<CheckerDescriptor> m_Checkers = new List<CheckerDescriptor>();
        /// <summary>         /// 存储 Errors。         /// </summary>
        private readonly List<string> m_Errors = new List<string>();

        /// <summary>
        /// 存储 Collectors。
        /// </summary>
        public IReadOnlyList<CollectorDescriptor> Collectors => m_Collectors;

        public IReadOnlyList<FilterRuleDescriptor> FilterRules => m_FilterRules;

        public IReadOnlyList<PackRuleDescriptor> PackRules => m_PackRules;

        /// <summary>
        /// 存储 Checkers。
        /// </summary>
        public IReadOnlyList<CheckerDescriptor> Checkers => m_Checkers;

        /// <summary>
        /// 存储 Errors。
        /// </summary>
        public IReadOnlyList<string> Errors => m_Errors;

        /// <summary>
        /// 执行 Scan。
        /// </summary>
        /// <returns>执行结果。</returns>
        public static ExtensionRegistry Scan()
        {
            var registry = new ExtensionRegistry();
            registry.ScanTypes();
            return registry;
        }

        /// <summary>
        /// 获取 Collector。
        /// </summary>
        /// <param name="id">id 参数。</param>
        /// <returns>执行结果。</returns>
        public CollectorDescriptor GetCollector(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return m_Collectors.FirstOrDefault(x => x.Id == id);
        }

        public FilterRuleDescriptor GetFilterRule(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return m_FilterRules.FirstOrDefault(x => x.Id == id);
        }

        public PackRuleDescriptor GetPackRule(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return m_PackRules.FirstOrDefault(x => x.Id == id);
        }

        /// <summary>
        /// 执行 Scan Types。
        /// </summary>
        private void ScanTypes()
        {
            m_Collectors.Clear();
            m_FilterRules.Clear();
            m_PackRules.Clear();
            m_Checkers.Clear();
            m_Errors.Clear();

            foreach (var type in GetConcreteTypes(TypeCache.GetTypesWithAttribute<CollectorAttribute>()))
            {
                TryRegisterCollector(type);
            }

            foreach (var type in GetConcreteTypes(TypeCache.GetTypesWithAttribute<FilterRuleAttribute>()))
            {
                TryRegisterFilterRule(type);
            }

            foreach (var type in GetConcreteTypes(TypeCache.GetTypesWithAttribute<PackRuleAttribute>()))
            {
                TryRegisterPackRule(type);
            }

            foreach (var type in GetConcreteTypes(TypeCache.GetTypesDerivedFrom<GameDeveloperKit.ResourceEditor.Validation.Checker>()))
            {
                TryRegisterChecker(type);
            }

            m_Collectors.Sort((a, b) => CompareDescriptor((a.Order, a.DisplayName, a.Id), (b.Order, b.DisplayName, b.Id)));
            m_FilterRules.Sort((a, b) => CompareDescriptor((a.Order, a.DisplayName, a.Id), (b.Order, b.DisplayName, b.Id)));
            m_PackRules.Sort((a, b) => CompareDescriptor((a.Order, a.DisplayName, a.Id), (b.Order, b.DisplayName, b.Id)));
            m_Checkers.Sort((a, b) => CompareDescriptor((a.Order, a.DisplayName, a.Id), (b.Order, b.DisplayName, b.Id)));
            m_Errors.Sort(StringComparer.Ordinal);
        }

        /// <summary>
        /// 执行 Compare Descriptor。
        /// </summary>
        /// <param name="leftOrder">left Order 参数。</param>
        /// <param name="leftName">left Name 参数。</param>
        /// <param name="rightOrder">right Order 参数。</param>
        /// <param name="rightName">right Name 参数。</param>
        /// <returns>执行结果。</returns>
        private static int CompareDescriptor(
            (int Order, string Name, string Id) left,
            (int Order, string Name, string Id) right)
        {
            var order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }

            var name = string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            return name != 0 ? name : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        }

        /// <summary>
        /// 获取 Concrete Types。
        /// </summary>
        /// <param name="types">types 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<Type> GetConcreteTypes(IEnumerable<Type> types)
        {
            foreach (var type in types.OrderBy(type => type?.AssemblyQualifiedName, StringComparer.Ordinal))
            {
                if (type == null || type.IsAbstract || type.ContainsGenericParameters)
                {
                    continue;
                }

                yield return type;
            }
        }

        /// <summary>
        /// 尝试执行 Try Register Collector。
        /// </summary>
        /// <param name="type">type 参数。</param>
        private void TryRegisterCollector(Type type)
        {
            var attribute = type.GetCustomAttribute<CollectorAttribute>();
            if (attribute == null)
            {
                return;
            }

            if (typeof(Collector).IsAssignableFrom(type) is false)
            {
                m_Errors.Add($"{type.FullName} has CollectorAttribute but does not inherit Collector.");
                return;
            }

            if (TryCreate(type, out Collector instance) is false)
            {
                return;
            }

            if (m_Collectors.Any(x => x.Id == attribute.Id))
            {
                m_Errors.Add($"Duplicate collector id: {attribute.Id}");
                return;
            }

            m_Collectors.Add(new CollectorDescriptor(attribute.Id, attribute.DisplayName, attribute.Description, attribute.Order, type, instance));
        }

        private void TryRegisterFilterRule(Type type)
        {
            var attribute = type.GetCustomAttribute<FilterRuleAttribute>();
            if (attribute == null)
            {
                return;
            }

            if (typeof(FilterRule).IsAssignableFrom(type) is false)
            {
                m_Errors.Add($"{type.FullName} has FilterRuleAttribute but does not inherit FilterRule.");
                return;
            }

            if (m_FilterRules.Any(x => x.Id == attribute.Id))
            {
                m_Errors.Add($"Duplicate filter rule id: {attribute.Id}");
                return;
            }

            if (TryCreate(type, out FilterRule instance) is false)
            {
                return;
            }

            m_FilterRules.Add(new FilterRuleDescriptor(attribute.Id, attribute.DisplayName, attribute.Description, attribute.Order, type, instance));
        }

        private void TryRegisterPackRule(Type type)
        {
            var attribute = type.GetCustomAttribute<PackRuleAttribute>();
            if (attribute == null)
            {
                return;
            }

            if (typeof(PackRule).IsAssignableFrom(type) is false)
            {
                m_Errors.Add($"{type.FullName} has PackRuleAttribute but does not inherit PackRule.");
                return;
            }

            if (m_PackRules.Any(x => x.Id == attribute.Id))
            {
                m_Errors.Add($"Duplicate pack rule id: {attribute.Id}");
                return;
            }

            if (TryCreate(type, out PackRule instance) is false)
            {
                return;
            }

            m_PackRules.Add(new PackRuleDescriptor(attribute.Id, attribute.DisplayName, attribute.Description, attribute.Order, type, instance));
        }

        /// <summary>
        /// 尝试执行 Try Register Checker。
        /// </summary>
        /// <param name="type">type 参数。</param>
        private void TryRegisterChecker(Type type)
        {
            if (typeof(GameDeveloperKit.ResourceEditor.Validation.Checker).IsAssignableFrom(type) is false)
            {
                return;
            }

            if (TryCreate(type, out GameDeveloperKit.ResourceEditor.Validation.Checker instance) is false)
            {
                return;
            }

            var id = type.FullName;
            var displayName = ObjectNames.NicifyVariableName(type.Name.Replace("Checker", string.Empty));
            m_Checkers.Add(new CheckerDescriptor(id, displayName, 0, type, instance));
        }

        /// <summary>
        /// 定义 Try Create 类型。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="type">type 参数。</param>
        /// <param name="instance">instance 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
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

    /// <summary>
    /// 定义 Resource Editor Registry Cache 类型。
    /// </summary>
    [InitializeOnLoad]
    public static class ExtensionRegistryCache
    {
        static ExtensionRegistryCache()
        {
            Refresh();
        }

        public static ExtensionRegistry Current { get; private set; }

        /// <summary>
        /// 刷新 member。
        /// </summary>
        /// <returns>执行结果。</returns>
        public static ExtensionRegistry Refresh()
        {
            Current = ExtensionRegistry.Scan();
            return Current;
        }
    }
}

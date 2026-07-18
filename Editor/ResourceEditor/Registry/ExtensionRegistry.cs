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

    /// <summary>
    /// 定义 Resource Build Strategy Descriptor 类型。
    /// </summary>
    public sealed class BuildStrategyDescriptor
    {
        /// <summary>
        /// 初始化 Resource Build Strategy Descriptor。
        /// </summary>
        /// <param name="id">id 参数。</param>
        /// <param name="displayName">display Name 参数。</param>
        /// <param name="description">description 参数。</param>
        /// <param name="order">order 参数。</param>
        /// <param name="type">type 参数。</param>
        /// <param name="instance">instance 参数。</param>
        public BuildStrategyDescriptor(string id, string displayName, string description, int order, Type type, BuildStrategy instance)
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

        public BuildStrategy Instance { get; }
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
        /// <summary>         /// 存储 Build Strategies。         /// </summary>
        private readonly List<BuildStrategyDescriptor> m_BuildStrategies = new List<BuildStrategyDescriptor>();
        /// <summary>         /// 存储 Checkers。         /// </summary>
        private readonly List<CheckerDescriptor> m_Checkers = new List<CheckerDescriptor>();
        /// <summary>         /// 存储 Errors。         /// </summary>
        private readonly List<string> m_Errors = new List<string>();

        /// <summary>
        /// 存储 Collectors。
        /// </summary>
        public IReadOnlyList<CollectorDescriptor> Collectors => m_Collectors;

        /// <summary>
        /// 存储 Build Strategies。
        /// </summary>
        public IReadOnlyList<BuildStrategyDescriptor> BuildStrategies => m_BuildStrategies;

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

        /// <summary>
        /// 获取 Build Strategy。
        /// </summary>
        /// <param name="id">id 参数。</param>
        /// <returns>执行结果。</returns>
        public BuildStrategyDescriptor GetBuildStrategy(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return m_BuildStrategies.FirstOrDefault();
            }

            return m_BuildStrategies.FirstOrDefault(x => x.Id == id);
        }

        /// <summary>
        /// 执行 Scan Types。
        /// </summary>
        private void ScanTypes()
        {
            m_Collectors.Clear();
            m_BuildStrategies.Clear();
            m_Checkers.Clear();
            m_Errors.Clear();

            foreach (var type in GetConcreteTypes(TypeCache.GetTypesWithAttribute<CollectorAttribute>()))
            {
                TryRegisterCollector(type);
            }

            foreach (var type in GetConcreteTypes(TypeCache.GetTypesWithAttribute<BuildStrategyAttribute>()))
            {
                TryRegisterBuildStrategy(type);
            }

            foreach (var type in GetConcreteTypes(TypeCache.GetTypesDerivedFrom<GameDeveloperKit.ResourceEditor.Validation.Checker>()))
            {
                TryRegisterChecker(type);
            }

            m_Collectors.Sort((a, b) => CompareDescriptor(a.Order, a.DisplayName, b.Order, b.DisplayName));
            m_BuildStrategies.Sort((a, b) => CompareDescriptor(a.Order, a.DisplayName, b.Order, b.DisplayName));
            m_Checkers.Sort((a, b) => CompareDescriptor(a.Order, a.DisplayName, b.Order, b.DisplayName));
        }

        /// <summary>
        /// 执行 Compare Descriptor。
        /// </summary>
        /// <param name="leftOrder">left Order 参数。</param>
        /// <param name="leftName">left Name 参数。</param>
        /// <param name="rightOrder">right Order 参数。</param>
        /// <param name="rightName">right Name 参数。</param>
        /// <returns>执行结果。</returns>
        private static int CompareDescriptor(int leftOrder, string leftName, int rightOrder, string rightName)
        {
            var order = leftOrder.CompareTo(rightOrder);
            return order != 0 ? order : string.Compare(leftName, rightName, StringComparison.Ordinal);
        }

        /// <summary>
        /// 获取 Concrete Types。
        /// </summary>
        /// <param name="types">types 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<Type> GetConcreteTypes(IEnumerable<Type> types)
        {
            foreach (var type in types)
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

        /// <summary>
        /// 尝试执行 Try Register Build Strategy。
        /// </summary>
        /// <param name="type">type 参数。</param>
        private void TryRegisterBuildStrategy(Type type)
        {
            var attribute = type.GetCustomAttribute<BuildStrategyAttribute>();
            if (attribute == null)
            {
                return;
            }

            if (typeof(BuildStrategy).IsAssignableFrom(type) is false)
            {
                m_Errors.Add($"{type.FullName} has BuildStrategyAttribute but does not inherit BuildStrategy.");
                return;
            }

            if (TryCreate(type, out BuildStrategy instance) is false)
            {
                return;
            }

            if (m_BuildStrategies.Any(x => x.Id == attribute.Id))
            {
                m_Errors.Add($"Duplicate build strategy id: {attribute.Id}");
                return;
            }

            m_BuildStrategies.Add(new BuildStrategyDescriptor(attribute.Id, attribute.DisplayName, attribute.Description, attribute.Order, type, instance));
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

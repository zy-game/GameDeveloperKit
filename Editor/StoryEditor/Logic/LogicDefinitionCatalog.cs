using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Logic;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor.Logic
{
    internal sealed class LogicDefinition
    {
        public LogicDefinition(
            string logicId,
            string displayName,
            string category,
            string description,
            Type nodeType,
            string inputLabel,
            IReadOnlyList<PortDefinition> ports,
            IReadOnlyList<NodeParameterDefinition> parameters,
            IReadOnlyDictionary<string, string> fieldRendererKeys)
        {
            LogicId = logicId;
            DisplayName = displayName;
            Category = category;
            Description = description;
            NodeType = nodeType;
            InputLabel = inputLabel;
            Ports = ports ?? Array.Empty<PortDefinition>();
            Parameters = parameters ?? Array.Empty<NodeParameterDefinition>();
            FieldRendererKeys = fieldRendererKeys ??
                                new Dictionary<string, string>(0, StringComparer.Ordinal);
        }

        public string LogicId { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public string Description { get; }

        public Type NodeType { get; }

        public string InputLabel { get; }

        public IReadOnlyList<PortDefinition> Ports { get; }

        public IReadOnlyList<NodeParameterDefinition> Parameters { get; }

        public IReadOnlyDictionary<string, string> FieldRendererKeys { get; }
    }

    internal sealed class LogicDefinitionCatalog
    {
        private static LogicDefinitionCatalog s_Shared;

        private readonly Dictionary<string, LogicDefinition> m_ById;

        private LogicDefinitionCatalog(
            IReadOnlyList<LogicDefinition> definitions,
            IReadOnlyList<string> errors)
        {
            Definitions = definitions;
            Errors = errors;
            m_ById = definitions.ToDictionary(item => item.LogicId, StringComparer.Ordinal);
        }

        public static LogicDefinitionCatalog Shared => s_Shared ??= Discover();

        public IReadOnlyList<LogicDefinition> Definitions { get; }

        public IReadOnlyList<string> Errors { get; }

        public bool TryGet(string logicId, out LogicDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(logicId))
            {
                definition = null;
                return false;
            }

            return m_ById.TryGetValue(logicId, out definition);
        }

        internal static LogicDefinitionCatalog Create(IEnumerable<Type> types)
        {
            var definitions = new List<LogicDefinition>();
            var errors = new List<string>();
            var definitionTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            if (types == null)
            {
                return new LogicDefinitionCatalog(definitions, errors);
            }

            foreach (var type in types.Where(item => item != null)
                         .OrderBy(item => item.FullName, StringComparer.Ordinal))
            {
                if (!TryCreateDefinition(type, out var definition, out var error))
                {
                    errors.Add(error);
                    continue;
                }

                if (definitionTypes.TryGetValue(definition.LogicId, out var existingType))
                {
                    if (duplicateIds.Add(definition.LogicId))
                    {
                        definitions.RemoveAll(item => string.Equals(
                            item.LogicId,
                            definition.LogicId,
                            StringComparison.Ordinal));
                    }

                    errors.Add(
                        $"代码节点 ID 重复：{definition.LogicId}（{existingType.FullName} / {type.FullName}）。");
                    continue;
                }

                definitionTypes.Add(definition.LogicId, type);
                definitions.Add(definition);
            }

            definitions.Sort(CompareDefinitions);
            errors.Sort(StringComparer.Ordinal);
            return new LogicDefinitionCatalog(definitions, errors);
        }

        private static LogicDefinitionCatalog Discover()
        {
            return Create(TypeCache.GetTypesDerivedFrom<ILogicNode>());
        }

        private static bool TryCreateDefinition(
            Type type,
            out LogicDefinition definition,
            out string error)
        {
            definition = null;
            error = null;
            if (!typeof(ILogicNode).IsAssignableFrom(type) ||
                type.IsAbstract ||
                type.IsInterface ||
                type.ContainsGenericParameters ||
                !IsAccessible(type) ||
                type.GetConstructor(Type.EmptyTypes) == null)
            {
                error = $"代码节点类型无效：{type.FullName}。必须是可访问、非抽象、非泛型并具有 public 无参构造的 ILogicNode。";
                return false;
            }

            LogicNodeAttribute nodeAttribute;
            OutputPortAttribute[] outputAttributes;
            LogicParameterAttribute[] parameterAttributes;
            InputPortAttribute inputAttribute;
            DescriptionAttribute descriptionAttribute;
            try
            {
                nodeAttribute = type.GetCustomAttribute<LogicNodeAttribute>(false);
                outputAttributes = type.GetCustomAttributes<OutputPortAttribute>(false).ToArray();
                parameterAttributes = type.GetCustomAttributes<LogicParameterAttribute>(false).ToArray();
                inputAttribute = type.GetCustomAttribute<InputPortAttribute>(false);
                descriptionAttribute = type.GetCustomAttribute<DescriptionAttribute>(false);
            }
            catch (Exception exception)
            {
                error = $"代码节点元数据读取失败：{type.FullName}。{exception.Message}";
                return false;
            }

            if (nodeAttribute == null)
            {
                error = $"代码节点缺少 LogicNodeAttribute：{type.FullName}。";
                return false;
            }

            if (outputAttributes.Length == 0)
            {
                error = $"代码节点至少需要一个输出端口：{type.FullName}。";
                return false;
            }

            var ports = new List<PortDefinition>
            {
                new PortDefinition("in", inputAttribute?.Label ?? "进入", PortDirection.Input, true)
            };
            var portIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < outputAttributes.Length; i++)
            {
                var output = outputAttributes[i];
                if (string.Equals(output.PortId, "in", StringComparison.Ordinal) ||
                    !portIds.Add(output.PortId))
                {
                    error = $"代码节点输出端口保留或重复：{type.FullName} / {output.PortId}。";
                    return false;
                }

                ports.Add(new PortDefinition(
                    output.PortId,
                    output.Label,
                    PortDirection.Output));
            }

            var parameters = new List<NodeParameterDefinition>();
            var parameterKeys = new HashSet<string>(StringComparer.Ordinal);
            var rendererKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < parameterAttributes.Length; i++)
            {
                var parameter = parameterAttributes[i];
                if (string.Equals(parameter.Key, LogicCommandCodec.LogicIdParameter, StringComparison.Ordinal) ||
                    string.Equals(parameter.Key, LogicCommandCodec.MarkerArgument, StringComparison.Ordinal) ||
                    !parameterKeys.Add(parameter.Key))
                {
                    error = $"代码节点参数键保留或重复：{type.FullName} / {parameter.Key}。";
                    return false;
                }

                var options = parameter.Options ?? Array.Empty<string>();
                if (parameter.ValueType == ParameterValueType.Option && !ValidOptions(options))
                {
                    error = $"代码节点选项参数必须声明非空且唯一的 Options：{type.FullName} / {parameter.Key}。";
                    return false;
                }

                if (parameter.ValueType == ParameterValueType.AssetReference &&
                    string.IsNullOrWhiteSpace(parameter.ResourceType))
                {
                    error = $"代码节点资源参数必须声明 ResourceType：{type.FullName} / {parameter.Key}。";
                    return false;
                }

                parameters.Add(new NodeParameterDefinition(
                    parameter.Key,
                    parameter.Label,
                    parameter.ValueType,
                    parameter.Required,
                    parameter.Tooltip,
                    parameter.ResourceType,
                    options));
                if (!string.IsNullOrWhiteSpace(parameter.FieldRendererKey))
                {
                    rendererKeys.Add(parameter.Key, parameter.FieldRendererKey.Trim());
                }
            }

            definition = new LogicDefinition(
                nodeAttribute.LogicId,
                nodeAttribute.DisplayName,
                string.IsNullOrWhiteSpace(nodeAttribute.Category) ? "代码节点" : nodeAttribute.Category,
                descriptionAttribute?.Description ?? string.Empty,
                type,
                inputAttribute?.Label ?? "进入",
                ports,
                parameters,
                rendererKeys);
            return true;
        }

        private static bool IsAccessible(Type type)
        {
            for (var current = type; current != null; current = current.DeclaringType)
            {
                if (current.IsNestedPrivate || current.IsNestedFamily || current.IsNestedFamANDAssem)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidOptions(IReadOnlyList<string> options)
        {
            if (options == null || options.Count == 0)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < options.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(options[i]) || !seen.Add(options[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareDefinitions(LogicDefinition left, LogicDefinition right)
        {
            var category = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
            if (category != 0)
            {
                return category;
            }

            var displayName = string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            return displayName != 0
                ? displayName
                : string.Compare(left.LogicId, right.LogicId, StringComparison.Ordinal);
        }
    }
}

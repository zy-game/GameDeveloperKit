using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 变量类型。
    /// </summary>
    public enum VariableType
    {
        /// <summary>
        /// 布尔。
        /// </summary>
        Boolean = 0,

        /// <summary>
        /// 数字。
        /// </summary>
        Number = 1,

        /// <summary>
        /// 字符串。
        /// </summary>
        String = 2
    }

    /// <summary>
    /// 变量声明。
    /// </summary>
    public sealed class VariableDefinition
    {
        /// <summary>
        /// 初始化变量声明。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <param name="type">变量类型。</param>
        /// <param name="defaultValue">默认值。</param>
        public VariableDefinition(string name, VariableType type, Value defaultValue = default(Value))
        {
            ValidateText(name, nameof(name));
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
        }

        /// <summary>
        /// 变量名。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 变量类型。
        /// </summary>
        public VariableType Type { get; }

        /// <summary>
        /// 默认值。
        /// </summary>
        public Value DefaultValue { get; }

        private static void ValidateText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }

    /// <summary>
    /// 变量 schema。
    /// </summary>
    public sealed class VariableSchema
    {
        private readonly List<VariableDefinition> m_Definitions;

        /// <summary>
        /// 初始化变量 schema。
        /// </summary>
        /// <param name="definitions">变量声明。</param>
        public VariableSchema(IReadOnlyList<VariableDefinition> definitions = null)
        {
            m_Definitions = CopyList(definitions);
        }

        /// <summary>
        /// 变量声明集合。
        /// </summary>
        public IReadOnlyList<VariableDefinition> Definitions => m_Definitions;

        private static List<VariableDefinition> CopyList(IReadOnlyList<VariableDefinition> items)
        {
            if (items == null || items.Count == 0)
            {
                return new List<VariableDefinition>();
            }

            return new List<VariableDefinition>(items);
        }
    }

    /// <summary>
    /// 命令声明。
    /// </summary>
    public sealed class CommandArgumentDefinition
    {
        /// <summary>
        /// 初始化命令参数声明。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <param name="label">显示名。</param>
        /// <param name="valueType">值类型。</param>
        /// <param name="required">是否必填。</param>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="options">选项。</param>
        /// <param name="tooltip">提示。</param>
        public CommandArgumentDefinition(
            string key,
            string label,
            ParameterValueType valueType = ParameterValueType.String,
            bool required = false,
            string resourceType = null,
            IReadOnlyList<string> options = null,
            string tooltip = null)
        {
            ValidateText(key, nameof(key));
            Key = key;
            Label = string.IsNullOrWhiteSpace(label) ? key : label;
            ValueType = valueType;
            Required = required;
            ResourceType = resourceType;
            Options = CopyList(options);
            Tooltip = tooltip;
        }

        /// <summary>
        /// 参数键。
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 显示名。
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// 值类型。
        /// </summary>
        public ParameterValueType ValueType { get; }

        /// <summary>
        /// 是否必填。
        /// </summary>
        public bool Required { get; }

        /// <summary>
        /// 资源类型。
        /// </summary>
        public string ResourceType { get; }

        /// <summary>
        /// 选项。
        /// </summary>
        public IReadOnlyList<string> Options { get; }

        /// <summary>
        /// 提示。
        /// </summary>
        public string Tooltip { get; }

        private static IReadOnlyList<string> CopyList(IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<string>();
            }

            return new List<string>(items);
        }

        private static void ValidateText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }

    /// <summary>
    /// 命令声明。
    /// </summary>
    public sealed class CommandDefinition
    {
        /// <summary>
        /// 初始化命令声明。
        /// </summary>
        /// <param name="name">命令名。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="waitForCompletion">是否等待完成。</param>
        /// <param name="argumentNames">参数名。</param>
        /// <param name="outcomePorts">输出端口。</param>
        public CommandDefinition(
            string name,
            string displayName,
            bool waitForCompletion = false,
            IReadOnlyList<string> argumentNames = null,
            IReadOnlyList<string> outcomePorts = null)
            : this(
                name,
                displayName,
                waitForCompletion,
                BuildArgumentDefinitions(argumentNames),
                outcomePorts)
        {
        }

        /// <summary>
        /// 初始化命令声明。
        /// </summary>
        /// <param name="name">命令名。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="waitForCompletion">是否等待完成。</param>
        /// <param name="argumentDefinitions">参数声明。</param>
        /// <param name="outcomePorts">输出端口。</param>
        public CommandDefinition(
            string name,
            string displayName,
            bool waitForCompletion,
            IReadOnlyList<CommandArgumentDefinition> argumentDefinitions,
            IReadOnlyList<string> outcomePorts)
        {
            ValidateText(name, nameof(name));
            Name = name;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
            WaitForCompletion = waitForCompletion;
            ArgumentDefinitions = CopyArguments(argumentDefinitions);
            ArgumentNames = BuildArgumentNames(ArgumentDefinitions);
            OutcomePorts = CopyList(outcomePorts);
        }

        /// <summary>
        /// 命令名。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 显示名。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 是否等待完成。
        /// </summary>
        public bool WaitForCompletion { get; }

        /// <summary>
        /// 参数名。
        /// </summary>
        public IReadOnlyList<string> ArgumentNames { get; }

        /// <summary>
        /// 参数声明。
        /// </summary>
        public IReadOnlyList<CommandArgumentDefinition> ArgumentDefinitions { get; }

        /// <summary>
        /// 输出端口。
        /// </summary>
        public IReadOnlyList<string> OutcomePorts { get; }

        private static IReadOnlyList<string> CopyList(IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<string>();
            }

            return new List<string>(items);
        }

        private static IReadOnlyList<CommandArgumentDefinition> CopyArguments(IReadOnlyList<CommandArgumentDefinition> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<CommandArgumentDefinition>();
            }

            var result = new List<CommandArgumentDefinition>();
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    result.Add(items[i]);
                }
            }

            return result;
        }

        private static IReadOnlyList<CommandArgumentDefinition> BuildArgumentDefinitions(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0)
            {
                return Array.Empty<CommandArgumentDefinition>();
            }

            var result = new List<CommandArgumentDefinition>();
            for (var i = 0; i < names.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(names[i]))
                {
                    continue;
                }

                result.Add(new CommandArgumentDefinition(names[i], names[i]));
            }

            return result;
        }

        private static IReadOnlyList<string> BuildArgumentNames(IReadOnlyList<CommandArgumentDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
            {
                return Array.Empty<string>();
            }

            var names = new List<string>();
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Key))
                {
                    continue;
                }

                if (!names.Contains(definition.Key))
                {
                    names.Add(definition.Key);
                }
            }

            return names;
        }

        private static void ValidateText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }

    /// <summary>
    /// 命令 schema。
    /// </summary>
    public sealed class CommandSchema
    {
        private readonly List<CommandDefinition> m_Definitions;

        /// <summary>
        /// 初始化命令 schema。
        /// </summary>
        /// <param name="definitions">命令声明。</param>
        public CommandSchema(IReadOnlyList<CommandDefinition> definitions = null)
        {
            m_Definitions = CopyList(definitions);
        }

        /// <summary>
        /// 命令声明集合。
        /// </summary>
        public IReadOnlyList<CommandDefinition> Definitions => m_Definitions;

        private static List<CommandDefinition> CopyList(IReadOnlyList<CommandDefinition> items)
        {
            if (items == null || items.Count == 0)
            {
                return new List<CommandDefinition>();
            }

            return new List<CommandDefinition>(items);
        }
    }
}

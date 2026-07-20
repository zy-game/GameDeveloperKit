using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Logic
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class LogicNodeAttribute : Attribute
    {
        public LogicNodeAttribute(string logicId, string displayName, string category = null)
        {
            ValidateText(logicId, nameof(logicId));
            ValidateText(displayName, nameof(displayName));
            LogicId = logicId.Trim();
            DisplayName = displayName.Trim();
            Category = string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
        }

        public string LogicId { get; }

        public string DisplayName { get; }

        public string Category { get; }

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

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class InputPortAttribute : Attribute
    {
        public InputPortAttribute(string label = "进入")
        {
            Label = string.IsNullOrWhiteSpace(label) ? "进入" : label.Trim();
        }

        public string Label { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class OutputPortAttribute : Attribute
    {
        public OutputPortAttribute(string portId, string label = null)
        {
            if (portId == null)
            {
                throw new ArgumentNullException(nameof(portId));
            }

            if (string.IsNullOrWhiteSpace(portId))
            {
                throw new ArgumentException("Logic output port ID cannot be empty.", nameof(portId));
            }

            PortId = portId.Trim();
            Label = string.IsNullOrWhiteSpace(label) ? PortId : label.Trim();
        }

        public string PortId { get; }

        public string Label { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class LogicParameterAttribute : Attribute
    {
        public LogicParameterAttribute(
            string key,
            string label,
            ParameterValueType valueType = ParameterValueType.String)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Logic parameter key cannot be empty.", nameof(key));
            }

            if (!Enum.IsDefined(typeof(ParameterValueType), valueType))
            {
                throw new ArgumentOutOfRangeException(nameof(valueType));
            }

            Key = key.Trim();
            Label = string.IsNullOrWhiteSpace(label) ? Key : label.Trim();
            ValueType = valueType;
        }

        public string Key { get; }

        public string Label { get; }

        public ParameterValueType ValueType { get; }

        public bool Required { get; set; }

        public string Tooltip { get; set; }

        public string ResourceType { get; set; }

        public string[] Options { get; set; }

        public string FieldRendererKey { get; set; }
    }

    public interface ILogicNode
    {
        UniTask<LogicResult> ExecuteAsync(
            LogicContext context,
            CancellationToken cancellationToken);
    }

    public readonly struct LogicContext
    {
        public LogicContext(
            string logicId,
            string invocationId,
            ArgumentBag arguments,
            RuntimeContext runtime)
        {
            if (string.IsNullOrWhiteSpace(logicId))
            {
                throw new ArgumentException("Logic ID cannot be empty.", nameof(logicId));
            }

            if (string.IsNullOrWhiteSpace(invocationId))
            {
                throw new ArgumentException("Logic invocation ID cannot be empty.", nameof(invocationId));
            }

            LogicId = logicId.Trim();
            InvocationId = invocationId.Trim();
            Arguments = arguments ?? new ArgumentBag();
            Runtime = runtime;
        }

        public string LogicId { get; }

        public string InvocationId { get; }

        public ArgumentBag Arguments { get; }

        public RuntimeContext Runtime { get; }
    }

    public readonly struct LogicResult
    {
        public LogicResult(string outputPortId)
        {
            OutputPortId = outputPortId;
        }

        public string OutputPortId { get; }

        public static LogicResult To(string outputPortId)
        {
            return new LogicResult(outputPortId);
        }
    }
}

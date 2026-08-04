using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 变量类型。
    /// </summary>
    public enum VariableType
    {
        Boolean = 0,
        Number = 1,
        String = 2
    }

    /// <summary>
    /// 变量声明。
    /// </summary>
    public sealed class VariableDefinition
    {
        public VariableDefinition(
            string name,
            VariableType type,
            Value defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(name));
            }

            Name = name;
            Type = type;
            DefaultValue = defaultValue;
        }

        public string Name { get; }

        public VariableType Type { get; }

        public Value DefaultValue { get; }
    }

    /// <summary>
    /// 变量 schema。
    /// </summary>
    public sealed class VariableSchema
    {
        public VariableSchema(IReadOnlyList<VariableDefinition> definitions = null)
        {
            Definitions = definitions == null || definitions.Count == 0
                ? Array.Empty<VariableDefinition>()
                : new List<VariableDefinition>(definitions).AsReadOnly();
        }

        public IReadOnlyList<VariableDefinition> Definitions { get; }
    }
}

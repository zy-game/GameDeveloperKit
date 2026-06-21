using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 语义节点分类。
    /// </summary>
    public enum NodeCategory
    {
        /// <summary>
        /// 流程节点。
        /// </summary>
        Flow = 0,

        /// <summary>
        /// 动作节点。
        /// </summary>
        Action = 1,

        /// <summary>
        /// 交互节点。
        /// </summary>
        Interaction = 2
    }

    /// <summary>
    /// 节点端口方向。
    /// </summary>
    public enum PortDirection
    {
        /// <summary>
        /// 输入端口。
        /// </summary>
        Input = 0,

        /// <summary>
        /// 输出端口。
        /// </summary>
        Output = 1
    }

    /// <summary>
    /// 节点参数值类型。
    /// </summary>
    public enum ParameterValueType
    {
        /// <summary>
        /// 字符串。
        /// </summary>
        String = 0,

        /// <summary>
        /// 数字。
        /// </summary>
        Number = 1,

        /// <summary>
        /// 布尔。
        /// </summary>
        Boolean = 2,

        /// <summary>
        /// 枚举或选项。
        /// </summary>
        Option = 3,

        /// <summary>
        /// 资源引用。
        /// </summary>
        AssetReference = 4
    }

    /// <summary>
    /// 节点端口定义。
    /// </summary>
    public readonly struct PortDefinition
    {
        /// <summary>
        /// 初始化端口定义。
        /// </summary>
        /// <param name="portId">端口 ID。</param>
        /// <param name="label">显示名。</param>
        /// <param name="direction">端口方向。</param>
        /// <param name="multiple">是否允许多连线。</param>
        public PortDefinition(string portId, string label, PortDirection direction, bool multiple = false)
        {
            PortId = portId;
            Label = label;
            Direction = direction;
            Multiple = multiple;
        }

        /// <summary>
        /// 端口 ID。
        /// </summary>
        public string PortId { get; }

        /// <summary>
        /// 显示名。
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// 端口方向。
        /// </summary>
        public PortDirection Direction { get; }

        /// <summary>
        /// 是否允许多连线。
        /// </summary>
        public bool Multiple { get; }
    }

    /// <summary>
    /// 节点参数定义。
    /// </summary>
    public readonly struct NodeParameterDefinition
    {
        /// <summary>
        /// 初始化参数定义。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <param name="label">显示名。</param>
        /// <param name="valueType">值类型。</param>
        /// <param name="required">是否必填。</param>
        /// <param name="tooltip">提示。</param>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="options">选项。</param>
        public NodeParameterDefinition(
            string key,
            string label,
            ParameterValueType valueType,
            bool required = false,
            string tooltip = null,
            string resourceType = null,
            IReadOnlyList<string> options = null)
        {
            Key = key;
            Label = label;
            ValueType = valueType;
            Required = required;
            Tooltip = tooltip;
            ResourceType = resourceType;
            Options = CopyList(options);
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
        /// 提示。
        /// </summary>
        public string Tooltip { get; }

        /// <summary>
        /// 资源类型。
        /// </summary>
        public string ResourceType { get; }

        /// <summary>
        /// 选项。
        /// </summary>
        public IReadOnlyList<string> Options { get; }

        private static IReadOnlyList<string> CopyList(IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<string>();
            }

            return new List<string>(items);
        }
    }

    /// <summary>
    /// 节点参数与端口 schema。
    /// </summary>
    public sealed class NodeParameterSchema
    {
        /// <summary>
        /// 初始化节点 schema。
        /// </summary>
        /// <param name="kind">节点类型。</param>
        /// <param name="category">节点分类。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="runtimeNode">是否进入 runtime 图。</param>
        /// <param name="ports">默认端口。</param>
        /// <param name="parameters">参数定义。</param>
        public NodeParameterSchema(
            NodeKind kind,
            NodeCategory category,
            string displayName,
            bool runtimeNode,
            IReadOnlyList<PortDefinition> ports = null,
            IReadOnlyList<NodeParameterDefinition> parameters = null)
        {
            Kind = kind;
            Category = category;
            DisplayName = displayName;
            RuntimeNode = runtimeNode;
            Ports = CopyList(ports);
            Parameters = CopyList(parameters);
        }

        /// <summary>
        /// 节点类型。
        /// </summary>
        public NodeKind Kind { get; }

        /// <summary>
        /// 节点分类。
        /// </summary>
        public NodeCategory Category { get; }

        /// <summary>
        /// 显示名。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 是否进入 runtime 图。
        /// </summary>
        public bool RuntimeNode { get; }

        /// <summary>
        /// 默认端口。
        /// </summary>
        public IReadOnlyList<PortDefinition> Ports { get; }

        /// <summary>
        /// 参数定义。
        /// </summary>
        public IReadOnlyList<NodeParameterDefinition> Parameters { get; }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            return new List<T>(items);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.EditorNodeGraph
{
    public enum EditorGraphPortDirection
    {
        Input = 0,
        Output = 1
    }

    public enum EditorGraphPortCapacity
    {
        Single = 0,
        Multiple = 1
    }

    public enum EditorGraphFieldValueType
    {
        Text = 0,
        Number = 1,
        Boolean = 2,
        Option = 3,
        AssetReference = 4
    }

    public enum EditorGraphDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum EditorGraphDiagnosticTargetKind
    {
        Graph = 0,
        Node = 1,
        Field = 2,
        Port = 3,
        Wire = 4
    }

    public sealed class EditorGraphDiagnostic
    {
        public EditorGraphDiagnostic(
            string diagnosticId,
            EditorGraphDiagnosticSeverity severity,
            EditorGraphDiagnosticTargetKind targetKind,
            string message,
            string tooltip = null,
            string nodeId = null,
            string fieldId = null,
            string portId = null,
            string wireId = null,
            bool stale = false)
        {
            DiagnosticId = diagnosticId ?? string.Empty;
            Severity = severity;
            TargetKind = targetKind;
            NodeId = nodeId;
            FieldId = fieldId;
            PortId = portId;
            WireId = wireId;
            Message = message ?? string.Empty;
            Tooltip = tooltip;
            Stale = stale;
        }

        public string DiagnosticId { get; }

        public EditorGraphDiagnosticSeverity Severity { get; }

        public EditorGraphDiagnosticTargetKind TargetKind { get; }

        public string NodeId { get; }

        public string FieldId { get; }

        public string PortId { get; }

        public string WireId { get; }

        public string Message { get; }

        public string Tooltip { get; }

        public bool Stale { get; }
    }

    public readonly struct EditorGraphPortRef : IEquatable<EditorGraphPortRef>
    {
        public EditorGraphPortRef(string nodeId, string portId)
        {
            NodeId = nodeId;
            PortId = portId;
        }

        public string NodeId { get; }

        public string PortId { get; }

        public bool IsValid => string.IsNullOrWhiteSpace(NodeId) is false && string.IsNullOrWhiteSpace(PortId) is false;

        public bool Equals(EditorGraphPortRef other)
        {
            return string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
                   string.Equals(PortId, other.PortId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EditorGraphPortRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((NodeId != null ? StringComparer.Ordinal.GetHashCode(NodeId) : 0) * 397) ^
                       (PortId != null ? StringComparer.Ordinal.GetHashCode(PortId) : 0);
            }
        }

        public override string ToString()
        {
            return $"{NodeId}:{PortId}";
        }
    }

    public readonly struct EditorNodeGraphMove
    {
        public readonly string NodeId;
        public readonly Vector2 Position;

        public EditorNodeGraphMove(string nodeId, Vector2 position)
        {
            NodeId = nodeId;
            Position = position;
        }
    }

    public sealed class EditorGraphConnectionResult
    {
        public static readonly EditorGraphConnectionResult Success = new EditorGraphConnectionResult(true, null);

        public EditorGraphConnectionResult(bool allowed, string message)
        {
            Allowed = allowed;
            Message = message;
        }

        public bool Allowed { get; }

        public string Message { get; }

        public static EditorGraphConnectionResult Fail(string message)
        {
            return new EditorGraphConnectionResult(false, message);
        }
    }

    public sealed class EditorGraphNodeModel
    {
        public EditorGraphNodeModel(
            string nodeId,
            string title,
            string subtitle,
            string category,
            Vector2 position,
            IReadOnlyList<EditorGraphPortModel> inputPorts,
            IReadOnlyList<EditorGraphPortModel> outputPorts,
            IReadOnlyList<EditorGraphFieldModel> fields,
            bool entry = false,
            bool selected = false,
            IReadOnlyList<EditorGraphDiagnostic> diagnostics = null,
            string styleKey = null)
        {
            NodeId = nodeId;
            Title = title;
            Subtitle = subtitle;
            Category = category;
            Position = position;
            InputPorts = inputPorts ?? Array.Empty<EditorGraphPortModel>();
            OutputPorts = outputPorts ?? Array.Empty<EditorGraphPortModel>();
            Fields = fields ?? Array.Empty<EditorGraphFieldModel>();
            Entry = entry;
            Selected = selected;
            Diagnostics = diagnostics ?? Array.Empty<EditorGraphDiagnostic>();
            StyleKey = styleKey;
        }

        public string NodeId { get; }

        public string Title { get; }

        public string Subtitle { get; }

        public string Category { get; }

        public string StyleKey { get; }

        public Vector2 Position { get; }

        public IReadOnlyList<EditorGraphPortModel> InputPorts { get; }

        public IReadOnlyList<EditorGraphPortModel> OutputPorts { get; }

        public IReadOnlyList<EditorGraphFieldModel> Fields { get; }

        public bool Entry { get; }

        public bool Selected { get; }

        public IReadOnlyList<EditorGraphDiagnostic> Diagnostics { get; }
    }

    public sealed class EditorGraphPortModel
    {
        public EditorGraphPortModel(
            string portId,
            string label,
            EditorGraphPortDirection direction,
            EditorGraphPortCapacity capacity,
            Color color,
            string tooltip = null,
            IReadOnlyList<EditorGraphDiagnostic> diagnostics = null)
        {
            PortId = portId;
            Label = label;
            Direction = direction;
            Capacity = capacity;
            Color = color;
            Tooltip = tooltip;
            Diagnostics = diagnostics ?? Array.Empty<EditorGraphDiagnostic>();
        }

        public string PortId { get; }

        public string Label { get; }

        public EditorGraphPortDirection Direction { get; }

        public EditorGraphPortCapacity Capacity { get; }

        public Color Color { get; }

        public string Tooltip { get; }

        public IReadOnlyList<EditorGraphDiagnostic> Diagnostics { get; }
    }

    public sealed class EditorGraphFieldModel
    {
        public EditorGraphFieldModel(
            string fieldId,
            string label,
            string value,
            EditorGraphFieldValueType valueType,
            IReadOnlyList<string> options = null,
            string tooltip = null,
            string resourceType = null,
            IReadOnlyList<EditorGraphDiagnostic> diagnostics = null,
            IReadOnlyList<EditorGraphFieldOption> optionItems = null,
            string displayValue = null)
        {
            FieldId = fieldId;
            Label = label;
            Value = value;
            ValueType = valueType;
            Options = options ?? Array.Empty<string>();
            OptionItems = optionItems ?? BuildOptionItems(Options);
            Tooltip = tooltip;
            ResourceType = resourceType;
            Diagnostics = diagnostics ?? Array.Empty<EditorGraphDiagnostic>();
            DisplayValue = displayValue ?? value ?? string.Empty;
        }

        public string FieldId { get; }

        public string Label { get; }

        public string Value { get; }

        public EditorGraphFieldValueType ValueType { get; }

        public IReadOnlyList<string> Options { get; }

        public IReadOnlyList<EditorGraphFieldOption> OptionItems { get; }

        public string Tooltip { get; }

        public string ResourceType { get; }

        public IReadOnlyList<EditorGraphDiagnostic> Diagnostics { get; }

        public string DisplayValue { get; }

        private static IReadOnlyList<EditorGraphFieldOption> BuildOptionItems(IReadOnlyList<string> options)
        {
            if (options == null || options.Count == 0)
            {
                return Array.Empty<EditorGraphFieldOption>();
            }

            var items = new EditorGraphFieldOption[options.Count];
            for (var i = 0; i < options.Count; i++)
            {
                items[i] = new EditorGraphFieldOption(options[i], options[i]);
            }

            return items;
        }
    }

    public sealed class EditorGraphFieldOption
    {
        public EditorGraphFieldOption(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Label { get; }

        public string Value { get; }
    }

    public sealed class EditorGraphWireModel
    {
        public EditorGraphWireModel(
            string wireId,
            EditorGraphPortRef output,
            EditorGraphPortRef input,
            string label = null,
            bool selected = false,
            IReadOnlyList<EditorGraphDiagnostic> diagnostics = null)
        {
            WireId = wireId;
            Output = output;
            Input = input;
            Label = label;
            Selected = selected;
            Diagnostics = diagnostics ?? Array.Empty<EditorGraphDiagnostic>();
        }

        public string WireId { get; }

        public EditorGraphPortRef Output { get; }

        public EditorGraphPortRef Input { get; }

        public string Label { get; }

        public bool Selected { get; }

        public IReadOnlyList<EditorGraphDiagnostic> Diagnostics { get; }
    }

    public sealed class EditorGraphNodeTemplate
    {
        public EditorGraphNodeTemplate(
            string templateId,
            string displayName,
            string category,
            string defaultTitle,
            IReadOnlyList<EditorGraphPortModel> ports,
            IReadOnlyList<EditorGraphFieldModel> fields,
            string tooltip = null,
            string styleKey = null)
        {
            TemplateId = templateId;
            DisplayName = displayName;
            Category = category;
            DefaultTitle = defaultTitle;
            Ports = ports ?? Array.Empty<EditorGraphPortModel>();
            Fields = fields ?? Array.Empty<EditorGraphFieldModel>();
            Tooltip = tooltip;
            StyleKey = styleKey;
        }

        public string TemplateId { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public string DefaultTitle { get; }

        public IReadOnlyList<EditorGraphPortModel> Ports { get; }

        public IReadOnlyList<EditorGraphFieldModel> Fields { get; }

        public string Tooltip { get; }

        public string StyleKey { get; }
    }
}

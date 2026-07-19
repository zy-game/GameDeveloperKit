using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story;
using GameDeveloperKit.StoryEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Text;
using GameDeveloperKit.Story.Settlement;
using GameDeveloperKit.Story.Event;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Event;
using GameDeveloperKit.StoryEditor.Media;
using GameDeveloperKit.StoryEditor.UI;

namespace GameDeveloperKit.StoryEditor.Graph
{
    internal sealed class GraphAdapter : IEditorNodeGraphAdapter
    {
        private const string VideoReferenceCustomType = "story.video-reference";
        private const string AudioReferenceCustomType = "story.audio-reference";
        private const string TextReferenceCustomType = "story.text-reference";
        private const string SettlementPlanCustomType = "story.settlement-plan";

        private readonly MainWindow m_Window;
        private readonly EventDefinitionCatalog m_EventDefinitions;

        public GraphAdapter(MainWindow window)
        {
            m_Window = window ?? throw new ArgumentNullException(nameof(window));
            m_EventDefinitions = EventDefinitionCatalog.Shared;
        }

        public IReadOnlyList<EditorGraphNodeModel> Nodes => BuildNodes();

        public IReadOnlyList<EditorGraphWireModel> Wires => BuildWires();

        public IReadOnlyList<EditorGraphNodeTemplate> Templates => BuildTemplates();

        public VisualElement CreateBlackboard()
        {
            return m_Window.CreateGraphBlackboard();
        }

        public VisualElement CreateCustomField(string nodeId, EditorGraphFieldModel field, Action<string> valueChanged)
        {
            if (field == null)
            {
                return null;
            }

            if (string.Equals(field.CustomType, AudioReferenceCustomType, StringComparison.Ordinal))
            {
                var audioContainer = new VisualElement();
                audioContainer.Add(new Label(AudioReferenceSummary(field.Value)));
                var audioActions = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                audioActions.Add(new Button(() => AudioPickerWindow.Open(field.Value, valueChanged))
                {
                    text = string.IsNullOrWhiteSpace(field.Value) ? "选择音频" : "更换音频"
                });
                audioActions.Add(new Button(() => valueChanged?.Invoke(string.Empty)) { text = "清除" });
                audioContainer.Add(audioActions);
                return audioContainer;
            }

            if (string.Equals(field.CustomType, TextReferenceCustomType, StringComparison.Ordinal))
            {
                var textContainer = new VisualElement();
                textContainer.Add(new Label(TextReferenceSummary(field.Value)));
                var textActions = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                textActions.Add(new Button(() => TextReferencePickerWindow.Open(field.Value, valueChanged)) { text = "编辑文本" });
                textActions.Add(new Button(() => valueChanged?.Invoke(string.Empty)) { text = "清除" });
                textContainer.Add(textActions);
                return textContainer;
            }

            if (string.Equals(field.CustomType, SettlementPlanCustomType, StringComparison.Ordinal))
            {
                var settlementContainer = new VisualElement();
                settlementContainer.Add(new Label(SettlementSummary(field.Value)));
                settlementContainer.Add(new Button(() => SettlementPlanEditorWindow.Open(field.Value, valueChanged)) { text = "编辑结算计划" });
                return settlementContainer;
            }

            if (string.Equals(field.CustomType, VideoReferenceCustomType, StringComparison.Ordinal) is false)
            {
                return null;
            }

            var container = new VisualElement();
            var summary = new Label(VideoReferenceSummary(nodeId, field.Value));
            summary.AddToClassList("story-video-reference__summary");
            container.Add(summary);
            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            actions.Add(new Button(() => VideoPickerWindow.Open(field.Value, valueChanged))
            {
                text = string.IsNullOrWhiteSpace(field.Value) ? "选择视频" : "更换视频"
            });
            actions.Add(new Button(() => valueChanged?.Invoke(string.Empty)) { text = "清除" });
            container.Add(actions);
            return container;
        }

        public EditorGraphConnectionResult CanConnect(EditorGraphPortRef output, EditorGraphPortRef input)
        {
            if (!output.IsValid || !input.IsValid)
            {
                return EditorGraphConnectionResult.Fail("端口无效。");
            }

            if (string.Equals(output.NodeId, input.NodeId, StringComparison.Ordinal))
            {
                return EditorGraphConnectionResult.Fail("不能把节点连接到自己。");
            }

            var from = m_Window.FindNode(output.NodeId);
            var target = m_Window.FindNode(input.NodeId);
            if (from == null || target == null)
            {
                return EditorGraphConnectionResult.Fail("节点不存在。");
            }

            var result = PortPolicy.CanConnect(m_Window.SelectedChapter, from, output.PortId, target);
            return result.Allowed
                ? EditorGraphConnectionResult.Success
                : EditorGraphConnectionResult.Fail(result.Message);
        }

        public void CreateNode(EditorGraphNodeTemplate template, Vector2 graphPosition, EditorGraphPortRef connectFrom)
        {
            if (template == null)
            {
                return;
            }

            if (Enum.TryParse(template.TemplateId, out NodeKind kind) is false)
            {
                return;
            }

            m_Window.AddNodeFromGraph(graphPosition, kind, connectFrom);
        }

        public void MoveNode(string nodeId, Vector2 graphPosition)
        {
            m_Window.MoveNodeFromGraph(nodeId, graphPosition);
        }

        public void MoveNodes(IReadOnlyList<EditorNodeGraphMove> moves)
        {
            m_Window.MoveNodesFromGraph(moves);
        }

        public void SelectNode(string nodeId)
        {
            m_Window.SelectNodeFromGraph(nodeId);
        }

        public void SelectNodes(IReadOnlyList<string> nodeIds)
        {
            m_Window.SelectNodesFromGraph(nodeIds);
        }

        public void SelectWire(string wireId)
        {
            m_Window.SelectWireFromGraph(wireId);
        }

        public void Connect(EditorGraphPortRef output, EditorGraphPortRef input)
        {
            m_Window.ConnectFromGraph(output, input);
        }

        public void Disconnect(string wireId)
        {
            m_Window.DisconnectFromGraph(wireId);
        }

        public void DeleteSelection()
        {
            m_Window.DeleteSelectionFromGraph();
        }

        public void SetNodeField(string nodeId, string fieldId, string value)
        {
            m_Window.SetNodeFieldFromGraph(nodeId, fieldId, value);
        }

        private IReadOnlyList<EditorGraphNodeModel> BuildNodes()
        {
            var chapter = m_Window.SelectedChapter;
            if (chapter == null)
            {
                return Array.Empty<EditorGraphNodeModel>();
            }

            var nodes = new List<EditorGraphNodeModel>();
            for (var i = 0; i < chapter.Nodes.Count; i++)
            {
                var node = chapter.Nodes[i];
                if (node == null)
                {
                    continue;
                }

                var schema = EventNodeSchemaResolver.Resolve(node, m_EventDefinitions);
                nodes.Add(new EditorGraphNodeModel(
                    node.NodeId,
                    schema.DisplayName,
                    schema.DisplayName,
                    CategoryLabel(schema.Category),
                    m_Window.GetNodeGraphPosition(node, i),
                    BuildPorts(node, schema, EditorGraphPortDirection.Input, PortPolicy.AllowsRuntimeFlowInput(node.NodeKind) is false),
                    BuildOutputPorts(node, schema),
                    BuildFields(node, schema),
                    string.Equals(chapter.EntryNodeId, node.NodeId, StringComparison.Ordinal),
                    m_Window.IsNodeSelected(node),
                    m_Window.GraphDiagnostics.ForNode(node.NodeId),
                    CategoryStyleKey(schema.Category)));
            }

            return nodes;
        }

        private IReadOnlyList<EditorGraphWireModel> BuildWires()
        {
            var chapter = m_Window.SelectedChapter;
            if (chapter == null)
            {
                return Array.Empty<EditorGraphWireModel>();
            }

            var wires = new List<EditorGraphWireModel>();
            for (var i = 0; i < chapter.Edges.Count; i++)
            {
                var edge = chapter.Edges[i];
                if (edge == null || edge.TargetKind != TransitionTargetKind.Node || string.IsNullOrWhiteSpace(edge.TargetNodeId))
                {
                    continue;
                }

                wires.Add(new EditorGraphWireModel(
                    edge.EdgeId,
                    new EditorGraphPortRef(edge.FromNodeId, edge.FromPortId),
                    new EditorGraphPortRef(edge.TargetNodeId, "in"),
                    edge.FromPortLabel,
                    ReferenceEquals(m_Window.SelectedEdge, edge),
                    m_Window.GraphDiagnostics.ForWire(edge.EdgeId)));
            }

            return wires;
        }

        private static IReadOnlyList<EditorGraphNodeTemplate> BuildTemplates()
        {
            var templates = new List<EditorGraphNodeTemplate>();
            foreach (var schema in NodeSchemaRegistry.Schemas.OrderBy(x => x.Category).ThenBy(x => x.DisplayName))
            {
                if (schema.Kind == NodeKind.Start ||
                    schema.Kind == NodeKind.End ||
                    NodeSchemaRegistry.IsDefaultAuthoringNode(schema.Kind) is false)
                {
                    continue;
                }

                var ports = new List<EditorGraphPortModel>();
                ports.AddRange(BuildTemplatePorts(schema, EditorGraphPortDirection.Input, PortPolicy.AllowsRuntimeFlowInput(schema.Kind) is false));
                ports.AddRange(BuildTemplatePorts(schema, EditorGraphPortDirection.Output, false));
                templates.Add(new EditorGraphNodeTemplate(
                    schema.Kind.ToString(),
                    schema.DisplayName,
                    CategoryLabel(schema.Category),
                    schema.DisplayName,
                    ports,
                    BuildTemplateFields(schema),
                    $"{CategoryLabel(schema.Category)}节点；{(schema.RuntimeNode ? "会编译进 Program。" : "仅用于编辑组织。")}",
                    CategoryStyleKey(schema.Category)));
            }

            return templates;
        }

        private IReadOnlyList<EditorGraphPortModel> BuildOutputPorts(AuthoringNode node, NodeSchema schema)
        {
            if (node.NodeKind == NodeKind.Parallel)
            {
                return BuildParallelOutputPorts(node, schema);
            }

            return BuildPorts(node, schema, EditorGraphPortDirection.Output, false);
        }

        private IReadOnlyList<EditorGraphPortModel> BuildParallelOutputPorts(AuthoringNode node, NodeSchema schema)
        {
            if (PortPolicy.AllowsRuntimeFlowOutput(schema.Kind) is false)
            {
                return Array.Empty<EditorGraphPortModel>();
            }

            var ports = new List<EditorGraphPortModel>();
            var edges = m_Window.SelectedChapter?.Edges ?? new List<AuthoringEdge>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null ||
                    string.Equals(edge.FromNodeId, node.NodeId, StringComparison.Ordinal) is false ||
                    PortPolicy.IsParallelBranchPort(edge.FromPortId) is false ||
                    seen.Add(edge.FromPortId) is false)
                {
                    continue;
                }

                ports.Add(new EditorGraphPortModel(
                    edge.FromPortId,
                    string.IsNullOrWhiteSpace(edge.FromPortLabel) ? edge.FromPortId : edge.FromPortLabel,
                    EditorGraphPortDirection.Output,
                    EditorGraphPortCapacity.Single,
                    CategoryColor(schema.Category),
                    $"并行轨道端口：{edge.FromPortId}。",
                    m_Window.GraphDiagnostics.ForPort(node.NodeId, edge.FromPortId)));
            }

            ports.Add(new EditorGraphPortModel(
                "branch",
                "新增轨道",
                EditorGraphPortDirection.Output,
                EditorGraphPortCapacity.Multiple,
                CategoryColor(schema.Category),
                "拖出连线创建一个新的并行轨道。",
                m_Window.GraphDiagnostics.ForPort(node.NodeId, "branch")));

            return ports;
        }

        private IReadOnlyList<EditorGraphPortModel> BuildPorts(
            AuthoringNode node,
            NodeSchema schema,
            EditorGraphPortDirection direction,
            bool skipInput)
        {
            if (direction == EditorGraphPortDirection.Input)
            {
                return skipInput
                    ? Array.Empty<EditorGraphPortModel>()
                    : new[]
                    {
                        new EditorGraphPortModel("in", "进入", EditorGraphPortDirection.Input, EditorGraphPortCapacity.Multiple, CategoryColor(schema.Category), "输入端口。", m_Window.GraphDiagnostics.ForPort(node.NodeId, "in"))
                    };
            }

            if (PortPolicy.AllowsRuntimeFlowOutput(schema.Kind) is false)
            {
                return Array.Empty<EditorGraphPortModel>();
            }

            var ports = new List<EditorGraphPortModel>();
            for (var i = 0; i < schema.Ports.Count; i++)
            {
                var port = schema.Ports[i];
                if (port.Direction != PortDirection.Output)
                {
                    continue;
                }

                ports.Add(new EditorGraphPortModel(
                    port.PortId,
                    port.Label,
                    EditorGraphPortDirection.Output,
                    port.Multiple ? EditorGraphPortCapacity.Multiple : EditorGraphPortCapacity.Single,
                    CategoryColor(schema.Category),
                    $"输出端口：{port.PortId}。",
                    m_Window.GraphDiagnostics.ForPort(node.NodeId, port.PortId)));
            }

            return ports;
        }

        private static IReadOnlyList<EditorGraphPortModel> BuildTemplatePorts(NodeSchema schema, EditorGraphPortDirection direction, bool skipInput)
        {
            if (direction == EditorGraphPortDirection.Input)
            {
                return skipInput
                    ? Array.Empty<EditorGraphPortModel>()
                    : new[]
                    {
                        new EditorGraphPortModel("in", "进入", EditorGraphPortDirection.Input, EditorGraphPortCapacity.Multiple, CategoryColor(schema.Category), "输入端口。")
                    };
            }

            if (PortPolicy.AllowsRuntimeFlowOutput(schema.Kind) is false)
            {
                return Array.Empty<EditorGraphPortModel>();
            }

            var ports = new List<EditorGraphPortModel>();
            for (var i = 0; i < schema.Ports.Count; i++)
            {
                var port = schema.Ports[i];
                if (port.Direction != PortDirection.Output)
                {
                    continue;
                }

                ports.Add(new EditorGraphPortModel(
                    port.PortId,
                    port.Label,
                    EditorGraphPortDirection.Output,
                    port.Multiple ? EditorGraphPortCapacity.Multiple : EditorGraphPortCapacity.Single,
                    CategoryColor(schema.Category),
                    $"输出端口：{port.PortId}。"));
            }

            return ports;
        }

        private IReadOnlyList<EditorGraphFieldModel> BuildFields(AuthoringNode node, NodeSchema schema)
        {
            var fields = new List<EditorGraphFieldModel>();

            for (var i = 0; i < schema.Parameters.Count; i++)
            {
                var parameter = schema.Parameters[i];
                var value = GetParameterValue(node, parameter.Key);
                var customType = ResolveCustomFieldType(node, parameter);
                fields.Add(new EditorGraphFieldModel(
                    parameter.Key,
                    parameter.Required ? $"{parameter.Label} *" : parameter.Label,
                    value,
                    ResolveFieldValueType(node, parameter),
                    OptionsFor(parameter),
                    ParameterTooltip(parameter),
                    ResolveEditorResourceType(parameter.ResourceType),
                    m_Window.GraphDiagnostics.ForField(node.NodeId, parameter.Key),
                    OptionItemsFor(node, parameter, value),
                    DisplayValueFor(node, parameter, value),
                    customType));
            }

            return fields;
        }

        private static IReadOnlyList<EditorGraphFieldModel> BuildTemplateFields(NodeSchema schema)
        {
            var fields = new List<EditorGraphFieldModel>();
            for (var i = 0; i < schema.Parameters.Count; i++)
            {
                var parameter = schema.Parameters[i];
                fields.Add(new EditorGraphFieldModel(
                    parameter.Key,
                    parameter.Required ? $"{parameter.Label} *" : parameter.Label,
                    string.Empty,
                    ToFieldValueType(parameter.ValueType),
                    OptionsFor(parameter),
                    ParameterTooltip(parameter),
                    ResolveEditorResourceType(parameter.ResourceType)));
            }

            return fields;
        }

        private static EditorGraphFieldValueType ToFieldValueType(ParameterValueType valueType)
        {
            switch (valueType)
            {
                case ParameterValueType.Number:
                    return EditorGraphFieldValueType.Number;
                case ParameterValueType.Boolean:
                    return EditorGraphFieldValueType.Boolean;
                case ParameterValueType.Option:
                    return EditorGraphFieldValueType.Option;
                case ParameterValueType.AssetReference:
                    return EditorGraphFieldValueType.AssetReference;
                default:
                    return EditorGraphFieldValueType.Text;
            }
        }

        private EditorGraphFieldValueType ResolveFieldValueType(AuthoringNode node, NodeParameterDefinition parameter)
        {
            if (string.IsNullOrWhiteSpace(ResolveCustomFieldType(node, parameter)) is false)
            {
                return EditorGraphFieldValueType.Custom;
            }

            if (node != null &&
                node.NodeKind == NodeKind.JumpChapter &&
                string.Equals(parameter.Key, "chapterId", StringComparison.Ordinal))
            {
                return EditorGraphFieldValueType.Option;
            }

            return ToFieldValueType(parameter.ValueType);
        }

        private string ResolveCustomFieldType(AuthoringNode node, NodeParameterDefinition parameter)
        {
            if (node == null)
            {
                return null;
            }

            if (string.Equals(parameter.Key, MediaCommandNames.ClipArgument, StringComparison.Ordinal))
            {
                if (node.NodeKind == NodeKind.PlayVideo) return VideoReferenceCustomType;
                if (node.NodeKind == NodeKind.PlayAudio) return AudioReferenceCustomType;
            }

            if (IsLocalizedTextField(node.NodeKind, parameter.Key)) return TextReferenceCustomType;
            if (node.NodeKind == NodeKind.SettleChapter && string.Equals(parameter.Key, SettlementCommandNames.PlanArgument, StringComparison.Ordinal)) return SettlementPlanCustomType;
            if (node.NodeKind == NodeKind.Event &&
                m_EventDefinitions.TryGet(GetParameterValue(node, EventCommandCodec.EventIdParameter), out var definition))
            {
                for (var i = 0; i < definition.Arguments.Count; i++)
                {
                    var argument = definition.Arguments[i];
                    if (string.Equals(argument.Key, parameter.Key, StringComparison.Ordinal))
                    {
                        return argument.FieldRendererKey;
                    }
                }
            }

            return null;
        }

        private static string SettlementSummary(string value)
        {
            return SettlementPlanCodec.TryDeserialize(value, out var plan, out var error)
                ? $"结算操作：{plan.Operations.Count} 项"
                : string.IsNullOrWhiteSpace(value) ? "尚未配置结算操作" : $"无效结算计划：{error}";
        }

        private static bool IsLocalizedTextField(NodeKind kind, string key)
        {
            if (string.Equals(key, "textKey", StringComparison.Ordinal) || string.Equals(key, "speaker", StringComparison.Ordinal))
            {
                return kind == NodeKind.Dialogue || kind == NodeKind.Narration || kind == NodeKind.Choice;
            }

            return false;
        }

        private static string TextReferenceSummary(string value)
        {
            if (TextReferenceCodec.TryDeserialize(value, out var reference, out var legacy, out var error) is false)
            {
                return string.IsNullOrWhiteSpace(value) ? "尚未配置文本" : $"无效文本引用：{error}";
            }

            if (reference.Mode == TextMode.Literal) return $"直接文本\n{reference.Value}";
            var catalog = LocalizationTextCatalog.Build();
            var preview = catalog.TryGetText(reference.Value, out var text) ? text : "<zh-CN 缺失>";
            return $"多语言 Key{(legacy ? "（旧值）" : string.Empty)} · {reference.Value}\n{preview}";
        }

        private static string AudioReferenceSummary(string value)
        {
            if (AudioReferenceCodec.TryDeserialize(value, out var reference, out _))
            {
                var id = string.IsNullOrWhiteSpace(reference.MediaId) ? string.Empty : $" · {reference.MediaId}";
                return $"{reference.Source}{id}\n{reference.Location}";
            }

            return string.IsNullOrWhiteSpace(value) ? "尚未选择音频" : $"旧 Resource 引用\n{value}";
        }

        private string VideoReferenceSummary(string nodeId, string value)
        {
            if (VideoReferenceCodec.TryDeserialize(value, out var reference, out _))
            {
                var source = reference.Primary.Source == MediaSource.Cdn ? "CDN" : "StreamingAssets";
                var id = string.IsNullOrWhiteSpace(reference.Primary.MediaId) ? string.Empty : $" · {reference.Primary.MediaId}";
                return $"{source} · {reference.Format}{id}\n{reference.Primary.Location}";
            }

            var node = m_Window.FindNode(nodeId);
            var sourceValue = node == null
                ? string.Empty
                : GetParameterValue(node, MediaCommandNames.VideoSourceArgument);
            if (string.IsNullOrWhiteSpace(value))
            {
                return "尚未选择视频";
            }

            return $"旧引用 · {sourceValue}\n{value}";
        }

        private static IReadOnlyList<string> OptionsFor(NodeParameterDefinition parameter)
        {
            return parameter.ValueType == ParameterValueType.Option
                ? parameter.Options
                : Array.Empty<string>();
        }

        private IReadOnlyList<EditorGraphFieldOption> OptionItemsFor(
            AuthoringNode node,
            NodeParameterDefinition parameter,
            string value)
        {
            if (node != null &&
                node.NodeKind == NodeKind.Event &&
                string.Equals(parameter.Key, EventCommandCodec.EventIdParameter, StringComparison.Ordinal))
            {
                var eventOptions = new EditorGraphFieldOption[m_EventDefinitions.Definitions.Count];
                for (var i = 0; i < m_EventDefinitions.Definitions.Count; i++)
                {
                    var definition = m_EventDefinitions.Definitions[i];
                    var label = string.IsNullOrWhiteSpace(definition.Group)
                        ? definition.DisplayName
                        : $"{definition.Group} / {definition.DisplayName}";
                    eventOptions[i] = new EditorGraphFieldOption(definition.EventId, label);
                }

                return eventOptions;
            }

            if (node != null &&
                node.NodeKind == NodeKind.JumpChapter &&
                string.Equals(parameter.Key, "chapterId", StringComparison.Ordinal))
            {
                return m_Window.GetJumpChapterFieldOptions(value);
            }

            if (parameter.ValueType != ParameterValueType.Option || parameter.Options == null || parameter.Options.Count == 0)
            {
                return Array.Empty<EditorGraphFieldOption>();
            }

            var options = new EditorGraphFieldOption[parameter.Options.Count];
            for (var i = 0; i < parameter.Options.Count; i++)
            {
                options[i] = new EditorGraphFieldOption(parameter.Options[i], parameter.Options[i]);
            }

            return options;
        }

        private string DisplayValueFor(AuthoringNode node, NodeParameterDefinition parameter, string value)
        {
            if (node != null &&
                node.NodeKind == NodeKind.JumpChapter &&
                string.Equals(parameter.Key, "chapterId", StringComparison.Ordinal))
            {
                return m_Window.GetJumpChapterFieldDisplayValue(value);
            }

            return value;
        }

        private static string ResolveEditorResourceType(string resourceType)
        {
            switch (resourceType)
            {
                case "video":
                    return "UnityEngine.Object";
                case "image":
                    return "UnityEngine.Texture2D";
                case "audio":
                    return "UnityEngine.AudioClip";
                default:
                    return resourceType;
            }
        }

        private static string ParameterTooltip(NodeParameterDefinition parameter)
        {
            var required = parameter.Required ? "必填。" : "可选。";
            var tooltip = string.IsNullOrWhiteSpace(parameter.Tooltip) ? string.Empty : $"{parameter.Tooltip} ";
            return $"{required} {tooltip}参数键：{parameter.Key}";
        }

        private static string GetParameterValue(AuthoringNode node, string key)
        {
            for (var i = 0; i < node.Parameters.Count; i++)
            {
                var parameter = node.Parameters[i];
                if (parameter != null && string.Equals(parameter.Key, key, StringComparison.Ordinal))
                {
                    return parameter.Value;
                }
            }

            return string.Empty;
        }

        private static Color CategoryColor(NodeCategory category)
        {
            switch (category)
            {
                case NodeCategory.Flow:
                    return new Color(0.36f, 0.72f, 0.56f);
                case NodeCategory.Action:
                    return new Color(0.32f, 0.61f, 0.85f);
                case NodeCategory.Interaction:
                    return new Color(0.86f, 0.67f, 0.31f);
                default:
                    return Color.gray;
            }
        }

        private static string CategoryLabel(NodeCategory category)
        {
            switch (category)
            {
                case NodeCategory.Flow:
                    return "流程";
                case NodeCategory.Action:
                    return "命令";
                case NodeCategory.Interaction:
                    return "交互";
                default:
                    return category.ToString();
            }
        }

        private static string CategoryStyleKey(NodeCategory category)
        {
            switch (category)
            {
                case NodeCategory.Flow:
                    return "flow";
                case NodeCategory.Action:
                    return "action";
                case NodeCategory.Interaction:
                    return "interaction";
                default:
                    return category.ToString();
            }
        }
    }
}

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

namespace GameDeveloperKit.StoryEditor
{
    internal sealed class StoryEditorGraphAdapter : IEditorNodeGraphAdapter
    {
        internal const string VideoWaitChoiceTemplateId = "story.pattern.video_wait_choice";
        internal const string VideoWaitQteTemplateId = "story.pattern.video_wait_qte";
        internal const string VideoWaitUnlockTemplateId = "story.pattern.video_wait_unlock";
        private const string InteractionPatternCategory = "互动模板";

        private readonly StoryEditorWindow m_Window;

        public StoryEditorGraphAdapter(StoryEditorWindow window)
        {
            m_Window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public IReadOnlyList<EditorGraphNodeModel> Nodes => BuildNodes();

        public IReadOnlyList<EditorGraphWireModel> Wires => BuildWires();

        public IReadOnlyList<EditorGraphNodeTemplate> Templates => BuildTemplates();

        public VisualElement CreateBlackboard()
        {
            return m_Window.CreateGraphBlackboard();
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

            var result = StoryEditorPortPolicy.CanConnect(m_Window.SelectedChapter, from, output.PortId, target);
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

            switch (template.TemplateId)
            {
                case VideoWaitChoiceTemplateId:
                case VideoWaitQteTemplateId:
                case VideoWaitUnlockTemplateId:
                    m_Window.AddInteractionPatternFromGraph(template.TemplateId, graphPosition, connectFrom);
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

                var schema = NodeSchemaRegistry.Get(node.NodeKind);
                nodes.Add(new EditorGraphNodeModel(
                    node.NodeId,
                    schema.DisplayName,
                    schema.DisplayName,
                    CategoryLabel(schema.Category),
                    m_Window.GetNodeGraphPosition(node, i),
                    BuildPorts(node, schema, EditorGraphPortDirection.Input, StoryEditorPortPolicy.AllowsRuntimeFlowInput(node.NodeKind) is false),
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
                ports.AddRange(BuildTemplatePorts(schema, EditorGraphPortDirection.Input, StoryEditorPortPolicy.AllowsRuntimeFlowInput(schema.Kind) is false));
                ports.AddRange(BuildTemplatePorts(schema, EditorGraphPortDirection.Output, false));
                templates.Add(new EditorGraphNodeTemplate(
                    schema.Kind.ToString(),
                    schema.DisplayName,
                    CategoryLabel(schema.Category),
                    schema.DisplayName,
                    ports,
                    BuildTemplateFields(schema),
                    $"{CategoryLabel(schema.Category)}节点；{(schema.RuntimeNode ? "会编译进 StoryProgram。" : "仅用于编辑组织。")}",
                    CategoryStyleKey(schema.Category)));
            }

            AddInteractionPatternTemplates(templates);
            return templates;
        }

        private static void AddInteractionPatternTemplates(List<EditorGraphNodeTemplate> templates)
        {
            templates.Add(CreateInteractionPatternTemplate(
                VideoWaitChoiceTemplateId,
                "视频中途选项",
                "创建 Parallel + PlayVideo + Wait + 多个 Choice item，用等待节点控制选项出现时机。"));
            templates.Add(CreateInteractionPatternTemplate(
                VideoWaitQteTemplateId,
                "视频中途 QTE",
                "创建 Parallel + PlayVideo + Wait + QTE command，用 success/fail outcome 推进剧情。"));
            templates.Add(CreateInteractionPatternTemplate(
                VideoWaitUnlockTemplateId,
                "视频中途 Unlock",
                "创建 Parallel + PlayVideo + Wait + Unlock command，用 success/fail outcome 推进剧情。"));
        }

        private static EditorGraphNodeTemplate CreateInteractionPatternTemplate(
            string templateId,
            string displayName,
            string tooltip)
        {
            return new EditorGraphNodeTemplate(
                templateId,
                displayName,
                InteractionPatternCategory,
                displayName,
                new[]
                {
                    new EditorGraphPortModel(
                        "in",
                        "进入",
                        EditorGraphPortDirection.Input,
                        EditorGraphPortCapacity.Multiple,
                        CategoryColor(NodeCategory.Interaction),
                        "可从已有流程端口拖入创建整个互动编排模板。")
                },
                Array.Empty<EditorGraphFieldModel>(),
                tooltip,
                CategoryStyleKey(NodeCategory.Interaction));
        }

        private IReadOnlyList<EditorGraphPortModel> BuildOutputPorts(StoryAuthoringNode node, NodeParameterSchema schema)
        {
            if (node.NodeKind == NodeKind.Parallel)
            {
                return BuildParallelOutputPorts(node, schema);
            }

            return BuildPorts(node, schema, EditorGraphPortDirection.Output, false);
        }

        private IReadOnlyList<EditorGraphPortModel> BuildParallelOutputPorts(StoryAuthoringNode node, NodeParameterSchema schema)
        {
            if (StoryEditorPortPolicy.AllowsRuntimeFlowOutput(schema.Kind) is false)
            {
                return Array.Empty<EditorGraphPortModel>();
            }

            var ports = new List<EditorGraphPortModel>();
            var edges = m_Window.SelectedChapter?.Edges ?? new List<StoryAuthoringEdge>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null ||
                    string.Equals(edge.FromNodeId, node.NodeId, StringComparison.Ordinal) is false ||
                    StoryEditorPortPolicy.IsParallelBranchPort(edge.FromPortId) is false ||
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
            StoryAuthoringNode node,
            NodeParameterSchema schema,
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

            if (StoryEditorPortPolicy.AllowsRuntimeFlowOutput(schema.Kind) is false)
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

        private static IReadOnlyList<EditorGraphPortModel> BuildTemplatePorts(NodeParameterSchema schema, EditorGraphPortDirection direction, bool skipInput)
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

            if (StoryEditorPortPolicy.AllowsRuntimeFlowOutput(schema.Kind) is false)
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

        private IReadOnlyList<EditorGraphFieldModel> BuildFields(StoryAuthoringNode node, NodeParameterSchema schema)
        {
            var fields = new List<EditorGraphFieldModel>();

            for (var i = 0; i < schema.Parameters.Count; i++)
            {
                var parameter = schema.Parameters[i];
                var value = GetParameterValue(node, parameter.Key);
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
                    DisplayValueFor(node, parameter, value)));
            }

            return fields;
        }

        private static IReadOnlyList<EditorGraphFieldModel> BuildTemplateFields(NodeParameterSchema schema)
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

        private static EditorGraphFieldValueType ResolveFieldValueType(StoryAuthoringNode node, NodeParameterDefinition parameter)
        {
            if (node != null &&
                node.NodeKind == NodeKind.JumpChapter &&
                string.Equals(parameter.Key, "chapterId", StringComparison.Ordinal))
            {
                return EditorGraphFieldValueType.Option;
            }

            return ToFieldValueType(parameter.ValueType);
        }

        private static IReadOnlyList<string> OptionsFor(NodeParameterDefinition parameter)
        {
            return parameter.ValueType == ParameterValueType.Option
                ? parameter.Options
                : Array.Empty<string>();
        }

        private IReadOnlyList<EditorGraphFieldOption> OptionItemsFor(
            StoryAuthoringNode node,
            NodeParameterDefinition parameter,
            string value)
        {
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

        private string DisplayValueFor(StoryAuthoringNode node, NodeParameterDefinition parameter, string value)
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

        private static string GetParameterValue(StoryAuthoringNode node, string key)
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

    internal static class StoryEditorPortPolicy
    {
        public static StoryEditorPortPolicyResult CanConnect(
            StoryAuthoringChapter chapter,
            StoryAuthoringNode from,
            string outputPortId,
            StoryAuthoringNode target)
        {
            if (chapter == null)
            {
                return StoryEditorPortPolicyResult.Fail("请先选择章节。");
            }

            if (from == null || target == null || string.IsNullOrWhiteSpace(outputPortId))
            {
                return StoryEditorPortPolicyResult.Fail("端口无效。");
            }

            if (string.Equals(from.NodeId, target.NodeId, StringComparison.Ordinal))
            {
                return StoryEditorPortPolicyResult.Fail("不能把节点连接到自己。");
            }

            if (target.NodeKind == NodeKind.Start)
            {
                return StoryEditorPortPolicyResult.Fail("开始节点不能作为目标。");
            }

            if (from.NodeKind == NodeKind.End)
            {
                return StoryEditorPortPolicyResult.Fail("结束节点没有输出端口。");
            }

            if (NodeSchemaRegistry.IsDefaultAuthoringNode(from.NodeKind) is false)
            {
                return StoryEditorPortPolicyResult.Fail("该节点已退出默认作者路径，不能再参与剧情流程连线。");
            }

            if (NodeSchemaRegistry.IsDefaultAuthoringNode(target.NodeKind) is false)
            {
                return StoryEditorPortPolicyResult.Fail("目标节点已退出默认作者路径，请改用内容、媒体、音频、等待、选项或章节跳转节点。");
            }

            if (from.NodeKind == NodeKind.Choice &&
                string.Equals(outputPortId, "selected", StringComparison.Ordinal) is false)
            {
                return StoryEditorPortPolicyResult.Fail("选项节点只能从“选择后”端口连接分支目标。");
            }

            if (target.NodeKind == NodeKind.Choice &&
                (CanOwnChoiceItems(from.NodeKind) is false ||
                 string.Equals(outputPortId, "completed", StringComparison.Ordinal) is false))
            {
                return StoryEditorPortPolicyResult.Fail("选项节点只能接在对白、旁白、等待或等待全部完成的完成端口后。");
            }

            if (!HasDeclaredOutputPort(from.NodeKind, outputPortId))
            {
                return StoryEditorPortPolicyResult.Fail("该节点没有这个输出端口。");
            }

            if (HasDuplicateEdge(chapter, from.NodeId, outputPortId, target.NodeId))
            {
                return StoryEditorPortPolicyResult.Fail("这条连线已经存在。");
            }

            return StoryEditorPortPolicyResult.Success;
        }

        public static bool IsMultipleOutputPort(
            StoryAuthoringNode node,
            string portId,
            StoryAuthoringNode targetNode)
        {
            if (node == null || string.IsNullOrWhiteSpace(portId))
            {
                return false;
            }

            if (IsLineChoicePort(node, portId, targetNode))
            {
                return true;
            }

            var schema = NodeSchemaRegistry.Get(node.NodeKind);
            for (var i = 0; i < schema.Ports.Count; i++)
            {
                var port = schema.Ports[i];
                if (port.Direction == PortDirection.Output &&
                    string.Equals(port.PortId, portId, StringComparison.Ordinal))
                {
                    return port.Multiple;
                }
            }

            return false;
        }

        public static bool HasDeclaredOutputPort(NodeKind kind, string portId)
        {
            if (string.IsNullOrWhiteSpace(portId))
            {
                return false;
            }

            if (IsParallelBranchOutput(kind, portId))
            {
                return true;
            }

            var schema = NodeSchemaRegistry.Get(kind);
            for (var i = 0; i < schema.Ports.Count; i++)
            {
                var port = schema.Ports[i];
                if (port.Direction == PortDirection.Output &&
                    string.Equals(port.PortId, portId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool AllowsRuntimeFlowInput(NodeKind kind)
        {
            return kind != NodeKind.Start;
        }

        public static bool AllowsRuntimeFlowOutput(NodeKind kind)
        {
            return kind != NodeKind.End;
        }

        public static bool IsParallelBranchPort(string portId)
        {
            return !string.IsNullOrWhiteSpace(portId) &&
                   portId.StartsWith("branch_", StringComparison.Ordinal);
        }

        public static bool IsLineChoicePort(StoryAuthoringNode node, string portId, StoryAuthoringNode targetNode)
        {
            return node != null &&
                   targetNode != null &&
                   CanOwnChoiceItems(node.NodeKind) &&
                   targetNode.NodeKind == NodeKind.Choice &&
                   string.Equals(portId, "completed", StringComparison.Ordinal);
        }

        private static bool IsParallelBranchOutput(NodeKind kind, string portId)
        {
            return kind == NodeKind.Parallel &&
                   (string.Equals(portId, "branch", StringComparison.Ordinal) || IsParallelBranchPort(portId));
        }

        private static bool IsLineNode(NodeKind kind)
        {
            return kind == NodeKind.Dialogue || kind == NodeKind.Narration;
        }

        private static bool CanOwnChoiceItems(NodeKind kind)
        {
            return IsLineNode(kind) || kind == NodeKind.Merge || kind == NodeKind.Wait;
        }

        private static bool HasDuplicateEdge(
            StoryAuthoringChapter chapter,
            string fromNodeId,
            string portId,
            string targetNodeId)
        {
            if (chapter == null)
            {
                return false;
            }

            for (var i = 0; i < chapter.Edges.Count; i++)
            {
                var edge = chapter.Edges[i];
                if (edge != null &&
                    edge.TargetKind == TransitionTargetKind.Node &&
                    string.Equals(edge.FromNodeId, fromNodeId, StringComparison.Ordinal) &&
                    string.Equals(edge.FromPortId, portId, StringComparison.Ordinal) &&
                    string.Equals(edge.TargetNodeId, targetNodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static StoryAuthoringNode FindNode(StoryAuthoringChapter chapter, string nodeId)
        {
            if (chapter == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            for (var i = 0; i < chapter.Nodes.Count; i++)
            {
                var node = chapter.Nodes[i];
                if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }
    }

    internal readonly struct StoryEditorPortPolicyResult
    {
        private StoryEditorPortPolicyResult(bool allowed, string message)
        {
            Allowed = allowed;
            Message = message;
        }

        public bool Allowed { get; }

        public string Message { get; }

        public static StoryEditorPortPolicyResult Success => new StoryEditorPortPolicyResult(true, null);

        public static StoryEditorPortPolicyResult Fail(string message)
        {
            return new StoryEditorPortPolicyResult(false, message);
        }
    }

    internal sealed class StoryEditorDiagnosticSet
    {
        public static readonly StoryEditorDiagnosticSet Empty = new StoryEditorDiagnosticSet(Array.Empty<StoryEditorDiagnosticItem>());

        private readonly IReadOnlyList<StoryEditorDiagnosticItem> m_Items;

        public StoryEditorDiagnosticSet(IReadOnlyList<StoryEditorDiagnosticItem> items)
        {
            m_Items = items ?? Array.Empty<StoryEditorDiagnosticItem>();
        }

        public IReadOnlyList<StoryEditorDiagnosticItem> Items => m_Items;

        public IReadOnlyList<EditorGraphDiagnostic> ForNode(string nodeId)
        {
            return Find(x => x.GraphDiagnostic.TargetKind == EditorGraphDiagnosticTargetKind.Node &&
                             string.Equals(x.GraphDiagnostic.NodeId, nodeId, StringComparison.Ordinal));
        }

        public IReadOnlyList<EditorGraphDiagnostic> ForField(string nodeId, string fieldId)
        {
            return Find(x => x.GraphDiagnostic.TargetKind == EditorGraphDiagnosticTargetKind.Field &&
                             string.Equals(x.GraphDiagnostic.NodeId, nodeId, StringComparison.Ordinal) &&
                             string.Equals(x.GraphDiagnostic.FieldId, fieldId, StringComparison.Ordinal));
        }

        public IReadOnlyList<EditorGraphDiagnostic> ForPort(string nodeId, string portId)
        {
            return Find(x => x.GraphDiagnostic.TargetKind == EditorGraphDiagnosticTargetKind.Port &&
                             string.Equals(x.GraphDiagnostic.NodeId, nodeId, StringComparison.Ordinal) &&
                             string.Equals(x.GraphDiagnostic.PortId, portId, StringComparison.Ordinal));
        }

        public IReadOnlyList<EditorGraphDiagnostic> ForWire(string wireId)
        {
            return Find(x => x.GraphDiagnostic.TargetKind == EditorGraphDiagnosticTargetKind.Wire &&
                             string.Equals(x.GraphDiagnostic.WireId, wireId, StringComparison.Ordinal));
        }

        public int Count(EditorGraphDiagnosticSeverity severity)
        {
            var count = 0;
            for (var i = 0; i < m_Items.Count; i++)
            {
                if (m_Items[i].GraphDiagnostic.Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }

        private IReadOnlyList<EditorGraphDiagnostic> Find(Func<StoryEditorDiagnosticItem, bool> predicate)
        {
            if (m_Items.Count == 0)
            {
                return Array.Empty<EditorGraphDiagnostic>();
            }

            var result = new List<EditorGraphDiagnostic>();
            for (var i = 0; i < m_Items.Count; i++)
            {
                if (predicate(m_Items[i]))
                {
                    result.Add(m_Items[i].GraphDiagnostic);
                }
            }

            return result;
        }
    }

    internal sealed class StoryEditorDiagnosticItem
    {
        public StoryEditorDiagnosticItem(
            EditorGraphDiagnostic graphDiagnostic,
            StoryEditorDiagnosticLocation location,
            string source,
            string originalMessage,
            bool visibleOnCurrentGraph)
        {
            GraphDiagnostic = graphDiagnostic ?? throw new ArgumentNullException(nameof(graphDiagnostic));
            Location = location;
            Source = source ?? string.Empty;
            OriginalMessage = originalMessage ?? string.Empty;
            VisibleOnCurrentGraph = visibleOnCurrentGraph;
        }

        public EditorGraphDiagnostic GraphDiagnostic { get; }

        public StoryEditorDiagnosticLocation Location { get; }

        public string Source { get; }

        public string OriginalMessage { get; }

        public bool VisibleOnCurrentGraph { get; }

        public string SummaryText
        {
            get
            {
                var prefix = GraphDiagnostic.Severity == EditorGraphDiagnosticSeverity.Error ? "错误" :
                    GraphDiagnostic.Severity == EditorGraphDiagnosticSeverity.Warning ? "警告" : "提示";
                if (!VisibleOnCurrentGraph && string.IsNullOrWhiteSpace(Location.ChapterId) is false)
                {
                    return $"{prefix}：{GraphDiagnostic.Message}（章节：{Location.ChapterId}）";
                }

                return $"{prefix}：{GraphDiagnostic.Message}";
            }
        }

        public string Tooltip
        {
            get
            {
                var tooltip = GraphDiagnostic.Tooltip;
                if (string.IsNullOrWhiteSpace(tooltip))
                {
                    tooltip = GraphDiagnostic.Message;
                }

                if (string.IsNullOrWhiteSpace(Source) && string.IsNullOrWhiteSpace(OriginalMessage))
                {
                    return tooltip;
                }

                return $"{tooltip}\nsource: {Source}\nmessage: {OriginalMessage}";
            }
        }
    }

    internal readonly struct StoryEditorDiagnosticLocation
    {
        public StoryEditorDiagnosticLocation(
            string storyId,
            string chapterId,
            string nodeId,
            string fieldId,
            string portId,
            string wireId)
        {
            StoryId = storyId;
            ChapterId = chapterId;
            NodeId = nodeId;
            FieldId = fieldId;
            PortId = portId;
            WireId = wireId;
        }

        public string StoryId { get; }

        public string ChapterId { get; }

        public string NodeId { get; }

        public string FieldId { get; }

        public string PortId { get; }

        public string WireId { get; }
    }

    internal static class StoryEditorDiagnostics
    {
        public static StoryEditorDiagnosticSet BuildLocal(StoryAuthoringAsset asset, StoryAuthoringChapter currentChapter)
        {
            var builder = new Builder(asset, currentChapter, false);
            builder.AddLocalDiagnostics();
            return builder.Build();
        }

        public static StoryEditorDiagnosticSet FromReport(
            StoryValidationReport report,
            StoryAuthoringAsset asset,
            StoryAuthoringChapter currentChapter,
            bool stale)
        {
            var builder = new Builder(asset, currentChapter, stale);
            var issues = report?.Issues ?? Array.Empty<StoryValidationIssue>();
            for (var i = 0; i < issues.Count; i++)
            {
                builder.AddReportIssue(issues[i]);
            }

            return builder.Build();
        }

        public static StoryEditorDiagnosticSet FromCompiledProgram(
            StoryProgram program,
            StoryAuthoringAsset asset,
            StoryAuthoringChapter currentChapter)
        {
            var builder = new Builder(asset, currentChapter, false);
            builder.AddSeekPolicyDiagnostics(program);
            return builder.Build();
        }

        private sealed class Builder
        {
            private readonly StoryAuthoringAsset m_Asset;
            private readonly StoryAuthoringChapter m_CurrentChapter;
            private readonly bool m_Stale;
            private readonly List<StoryEditorDiagnosticItem> m_Items = new List<StoryEditorDiagnosticItem>();
            private readonly HashSet<string> m_Keys = new HashSet<string>(StringComparer.Ordinal);

            public Builder(StoryAuthoringAsset asset, StoryAuthoringChapter currentChapter, bool stale)
            {
                m_Asset = asset;
                m_CurrentChapter = currentChapter;
                m_Stale = stale;
            }

            public StoryEditorDiagnosticSet Build()
            {
                return new StoryEditorDiagnosticSet(m_Items);
            }

            public void AddLocalDiagnostics()
            {
                if (m_CurrentChapter == null)
                {
                    return;
                }

                var nodes = m_CurrentChapter.Nodes
                    .Where(x => x != null && string.IsNullOrWhiteSpace(x.NodeId) is false)
                    .GroupBy(x => x.NodeId, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

                foreach (var node in nodes.Values)
                {
                    AddNodeFieldDiagnostics(node);
                }

                for (var i = 0; i < m_CurrentChapter.Edges.Count; i++)
                {
                    AddEdgeDiagnostics(m_CurrentChapter.Edges[i], nodes);
                }

                AddChoiceDiagnostics(nodes);
                AddChoiceOwnerMixDiagnostics(nodes);
                AddParallelDiagnostics(nodes);
            }

            public void AddReportIssue(StoryValidationIssue issue)
            {
                if (issue == null)
                {
                    return;
                }

                var location = ParseSource(issue.Source);
                var severity = ToSeverity(issue.Severity);
                var message = TranslateMessage(issue.Message);
                var visible = IsCurrentChapter(location);
                var diagnostic = CreateDiagnostic(issue.Source, severity, message, issue.Message, location, visible);
                AddItem(diagnostic, location, issue.Source, issue.Message, visible);
            }

            public void AddSeekPolicyDiagnostics(StoryProgram program)
            {
                if (program == null || m_CurrentChapter == null)
                {
                    return;
                }

                var compiledChapter = program.Chapters.FirstOrDefault(x =>
                    x != null &&
                    string.Equals(x.ChapterId, m_CurrentChapter.ChapterId, StringComparison.Ordinal));
                if (compiledChapter == null)
                {
                    return;
                }

                var steps = compiledChapter.Steps
                    .Where(x => x != null && string.IsNullOrWhiteSpace(x.StepId) is false)
                    .GroupBy(x => x.StepId, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
                for (var i = 0; i < m_CurrentChapter.Nodes.Count; i++)
                {
                    var node = m_CurrentChapter.Nodes[i];
                    if (node == null ||
                        node.NodeKind != NodeKind.PlayVideo ||
                        string.IsNullOrWhiteSpace(node.NodeId) ||
                        steps.TryGetValue(node.NodeId, out var step) is false ||
                        step.Kind != StoryStepKind.Command ||
                        string.Equals(step.Data.Command?.Name, StoryMediaCommandNames.PlayVideo, StringComparison.Ordinal) is false)
                    {
                        continue;
                    }

                    var transition = string.Equals(
                        step.Data.Command.Arguments.GetString(StoryMediaCommandNames.VideoSeekPolicyArgument),
                        StoryMediaCommandNames.VideoSeekPolicyTransition,
                        StringComparison.Ordinal);
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Info,
                        transition ? "seek policy: transition，可显示时间条。" : "seek policy: disabled，当前视频不开放 seek。",
                        transition
                            ? "编译器推导为纯过渡视频；播放窗口会显示视频时间条。"
                            : "编译产物没有 transition seek policy；播放窗口不会显示视频时间条。",
                        new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, node.NodeId, null, null, null));
                }
            }

            private void AddNodeFieldDiagnostics(StoryAuthoringNode node)
            {
                if (NodeSchemaRegistry.TryGet(node.NodeKind, out var schema) is false)
                {
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Error,
                        "节点类型未注册。",
                        "节点类型没有对应的 schema，无法编译到运行时。",
                        new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, node.NodeId, null, null, null));
                    return;
                }

                if (NodeSchemaRegistry.IsDefaultAuthoringNode(node.NodeKind) is false)
                {
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Error,
                        "节点已退出默认作者路径。",
                        "该节点不再作为 Story 默认剧情节点使用。请改用内容、媒体、音频、等待、选项、小游戏、事件或章节跳转节点。",
                        new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, node.NodeId, null, null, null));
                }

                for (var i = 0; i < schema.Parameters.Count; i++)
                {
                    var parameter = schema.Parameters[i];
                    var value = GetParameterValue(node, parameter.Key);
                    var location = new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, node.NodeId, parameter.Key, null, null);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        if (parameter.Required)
                        {
                            AddLocal(
                                EditorGraphDiagnosticSeverity.Error,
                                "必填命令字段未填写。",
                                $"字段“{parameter.Label}”是必填项。",
                                location);
                        }

                        continue;
                    }

                    switch (parameter.ValueType)
                    {
                        case ParameterValueType.Number:
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) is false)
                            {
                                AddLocal(EditorGraphDiagnosticSeverity.Error, "字段必须填写数字。", $"字段“{parameter.Label}”当前值不是数字。", location);
                            }

                            break;
                        case ParameterValueType.Boolean:
                            if (bool.TryParse(value, out _) is false)
                            {
                                AddLocal(EditorGraphDiagnosticSeverity.Error, "字段必须填写布尔值。", $"字段“{parameter.Label}”只能填写 true 或 false。", location);
                            }

                            break;
                        case ParameterValueType.Option:
                            if (IsValidOption(parameter, value) is false)
                            {
                                AddLocal(EditorGraphDiagnosticSeverity.Error, "字段必须使用有效选项。", $"字段“{parameter.Label}”只能使用已声明的选项。", location);
                            }

                            break;
                        case ParameterValueType.AssetReference:
                            if ((node.NodeKind != NodeKind.PlayVideo ||
                                 string.Equals(parameter.Key, StoryMediaCommandNames.ClipArgument, StringComparison.Ordinal) is false) &&
                                IsRecommendedAssetReference(value) is false)
                            {
                                AddLocal(EditorGraphDiagnosticSeverity.Warning, "资源引用不是项目资源路径。", $"字段“{parameter.Label}”建议使用 Assets/... 路径；如果这是业务资源 key，可以忽略。", location);
                            }

                            break;
                    }
                }

                if (node.NodeKind == NodeKind.PlayVideo)
                {
                    AddPlayVideoFieldDiagnostics(node);
                }
            }

            private void AddPlayVideoFieldDiagnostics(StoryAuthoringNode node)
            {
                var source = GetParameterValue(node, StoryMediaCommandNames.VideoSourceArgument);
                var clip = GetParameterValue(node, StoryMediaCommandNames.ClipArgument);
                if (string.IsNullOrWhiteSpace(source) ||
                    string.IsNullOrWhiteSpace(clip) ||
                    IsValidVideoSource(source) is false)
                {
                    return;
                }

                if (StoryVideoPathResolver.TryResolve(source, clip, out _, out var errorMessage))
                {
                    return;
                }

                AddLocal(
                    EditorGraphDiagnosticSeverity.Error,
                    "视频路径与来源不匹配。",
                    $"视频只支持 StreamingAssets、persistentDataPath 或网络流；当前路径无效：{errorMessage}",
                    new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, node.NodeId, StoryMediaCommandNames.ClipArgument, null, null));
            }

            private void AddEdgeDiagnostics(StoryAuthoringEdge edge, IReadOnlyDictionary<string, StoryAuthoringNode> nodes)
            {
                if (edge == null)
                {
                    return;
                }

                nodes.TryGetValue(edge.FromNodeId ?? string.Empty, out var fromNode);
                var baseLocation = new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, edge.FromNodeId, null, edge.FromPortId, edge.EdgeId);
                if (fromNode == null)
                {
                    AddLocal(EditorGraphDiagnosticSeverity.Error, "连线来源节点不存在。", "这条连线引用了不存在的来源节点。", baseLocation);
                    return;
                }

                if (NodeSchemaRegistry.IsDefaultAuthoringNode(fromNode.NodeKind) is false)
                {
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Error,
                        "旧节点不能进入运行时剧情流程。",
                        "这条连线的来源节点已退出默认作者路径，请改用线性内容节点和多轨帧表达。",
                        new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, fromNode.NodeId, null, edge.FromPortId, edge.EdgeId));
                }

                if (StoryEditorPortPolicy.HasDeclaredOutputPort(fromNode.NodeKind, edge.FromPortId) is false)
                {
                    AddLocal(EditorGraphDiagnosticSeverity.Error, "输出端口未在节点 schema 中声明。", $"端口“{edge.FromPortId}”不是该节点的输出端口。", baseLocation);
                }

                if (edge.TargetKind == TransitionTargetKind.Node)
                {
                    if (nodes.TryGetValue(edge.TargetNodeId ?? string.Empty, out var targetNode) is false)
                    {
                        AddLocal(EditorGraphDiagnosticSeverity.Error, "连线目标节点不存在。", "这条连线指向了当前章节中不存在的节点。", baseLocation);
                        return;
                    }

                    if (NodeSchemaRegistry.IsDefaultAuthoringNode(targetNode.NodeKind) is false)
                    {
                        AddLocal(
                            EditorGraphDiagnosticSeverity.Error,
                            "旧节点不能作为运行时流程目标。",
                            "这条连线的目标节点已退出默认作者路径，请改用内容、媒体、音频、等待、选项、小游戏、事件或章节跳转节点。",
                            new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, targetNode.NodeId, null, null, edge.EdgeId));
                    }
                }
                else if (edge.TargetKind == TransitionTargetKind.Chapter && FindChapter(edge.TargetChapterId) == null)
                {
                    AddLocal(EditorGraphDiagnosticSeverity.Error, "目标章节不存在。", "这条连线指向了不存在的章节。", baseLocation);
                }
            }

            private void AddChoiceDiagnostics(IReadOnlyDictionary<string, StoryAuthoringNode> nodes)
            {
                foreach (var node in nodes.Values)
                {
                    if (node.NodeKind != NodeKind.Choice)
                    {
                        continue;
                    }

                    var selectedCount = CountOutgoing(node.NodeId, "selected");
                    if (selectedCount != 1)
                    {
                        AddLocal(
                            EditorGraphDiagnosticSeverity.Error,
                            "选项必须且只能连接一个“选择后”目标。",
                            "每个选项节点需要且只能有一条选择后的目标连线。",
                            new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, node.NodeId, null, "selected", null));
                    }
                }
            }

            private void AddChoiceOwnerMixDiagnostics(IReadOnlyDictionary<string, StoryAuthoringNode> nodes)
            {
                foreach (var node in nodes.Values)
                {
                    if (node.NodeKind != NodeKind.Dialogue &&
                        node.NodeKind != NodeKind.Narration &&
                        node.NodeKind != NodeKind.Merge &&
                        node.NodeKind != NodeKind.Wait)
                    {
                        continue;
                    }

                    var hasChoice = false;
                    var hasNonChoice = false;
                    for (var i = 0; i < m_CurrentChapter.Edges.Count; i++)
                    {
                        var edge = m_CurrentChapter.Edges[i];
                        if (edge == null ||
                            edge.TargetKind != TransitionTargetKind.Node ||
                            string.Equals(edge.FromNodeId, node.NodeId, StringComparison.Ordinal) is false ||
                            string.Equals(edge.FromPortId, "completed", StringComparison.Ordinal) is false)
                        {
                            continue;
                        }

                        if (nodes.TryGetValue(edge.TargetNodeId ?? string.Empty, out var target) && target.NodeKind == NodeKind.Choice)
                        {
                            hasChoice = true;
                        }
                        else
                        {
                            hasNonChoice = true;
                        }
                    }

                    if (hasChoice && hasNonChoice)
                    {
                        AddLocal(
                            EditorGraphDiagnosticSeverity.Error,
                            "完成端口不能同时连接选项和普通流程。",
                            "对白、旁白、等待或等待全部完成节点接选项时，completed 端口不能再直连普通节点。",
                            new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, node.NodeId, null, "completed", null));
                    }
                }
            }

            private void AddParallelDiagnostics(IReadOnlyDictionary<string, StoryAuthoringNode> nodes)
            {
                foreach (var node in nodes.Values)
                {
                    if (node.NodeKind != NodeKind.Parallel)
                    {
                        continue;
                    }

                    var branches = GetOutgoingEdges(node.NodeId)
                        .Where(x => x != null && (string.Equals(x.FromPortId, "branch", StringComparison.Ordinal) || StoryEditorPortPolicy.IsParallelBranchPort(x.FromPortId)))
                        .ToList();
                    if (branches.Count < 2)
                    {
                        AddLocal(
                            EditorGraphDiagnosticSeverity.Error,
                            "并行节点至少需要两个轨道。",
                            "Parallel 必须通过两个或更多轨道端口连接到当前章节内的节点。",
                            new StoryEditorDiagnosticLocation(m_Asset?.StoryId, m_CurrentChapter.ChapterId, node.NodeId, null, "branch", null));
                    }
                }
            }

            private int CountOutgoing(string nodeId, string portId)
            {
                var count = 0;
                for (var i = 0; i < m_CurrentChapter.Edges.Count; i++)
                {
                    var edge = m_CurrentChapter.Edges[i];
                    if (edge != null &&
                        string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal) &&
                        string.Equals(edge.FromPortId, portId, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }

                return count;
            }

            private List<StoryAuthoringEdge> GetOutgoingEdges(string nodeId)
            {
                var edges = new List<StoryAuthoringEdge>();
                for (var i = 0; i < m_CurrentChapter.Edges.Count; i++)
                {
                    var edge = m_CurrentChapter.Edges[i];
                    if (edge != null && string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal))
                    {
                        edges.Add(edge);
                    }
                }

                return edges;
            }

            private StoryAuthoringChapter FindChapter(string chapterId)
            {
                if (m_Asset == null || string.IsNullOrWhiteSpace(chapterId))
                {
                    return null;
                }

                for (var i = 0; i < m_Asset.Chapters.Count; i++)
                {
                    var chapter = m_Asset.Chapters[i];
                    if (chapter != null && string.Equals(chapter.ChapterId, chapterId, StringComparison.Ordinal))
                    {
                        return chapter;
                    }
                }

                return null;
            }

            private void AddLocal(
                EditorGraphDiagnosticSeverity severity,
                string message,
                string tooltip,
                StoryEditorDiagnosticLocation location)
            {
                var source = BuildSource(location);
                var diagnostic = CreateDiagnostic(source, severity, message, tooltip, location, true);
                AddItem(diagnostic, location, source, tooltip, true);
            }

            private static bool IsValidOption(NodeParameterDefinition parameter, string value)
            {
                if (parameter.Options == null || parameter.Options.Count == 0)
                {
                    return true;
                }

                for (var i = 0; i < parameter.Options.Count; i++)
                {
                    if (string.Equals(parameter.Options[i], value, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsValidVideoSource(string source)
            {
                return string.Equals(source, StoryMediaCommandNames.VideoSourceStreamingAssets, StringComparison.Ordinal) ||
                       string.Equals(source, StoryMediaCommandNames.VideoSourcePersistentDataPath, StringComparison.Ordinal) ||
                       string.Equals(source, StoryMediaCommandNames.VideoSourceNetworkStream, StringComparison.Ordinal);
            }

            private void AddItem(
                EditorGraphDiagnostic diagnostic,
                StoryEditorDiagnosticLocation location,
                string source,
                string originalMessage,
                bool visibleOnCurrentGraph)
            {
                var key = $"{diagnostic.TargetKind}:{diagnostic.NodeId}:{diagnostic.FieldId}:{diagnostic.PortId}:{diagnostic.WireId}:{diagnostic.Severity}:{diagnostic.Message}:{diagnostic.Stale}";
                if (m_Keys.Add(key) is false)
                {
                    return;
                }

                m_Items.Add(new StoryEditorDiagnosticItem(diagnostic, location, source, originalMessage, visibleOnCurrentGraph));
            }

            private EditorGraphDiagnostic CreateDiagnostic(
                string source,
                EditorGraphDiagnosticSeverity severity,
                string message,
                string tooltip,
                StoryEditorDiagnosticLocation location,
                bool visibleOnCurrentGraph)
            {
                var targetKind = EditorGraphDiagnosticTargetKind.Graph;
                var nodeId = visibleOnCurrentGraph ? location.NodeId : null;
                var fieldId = visibleOnCurrentGraph ? location.FieldId : null;
                var portId = visibleOnCurrentGraph ? location.PortId : null;
                var wireId = visibleOnCurrentGraph && string.IsNullOrWhiteSpace(location.WireId) is false
                    ? ResolveWireId(location)
                    : null;

                if (string.IsNullOrWhiteSpace(wireId) is false)
                {
                    targetKind = EditorGraphDiagnosticTargetKind.Wire;
                    fieldId = null;
                    portId = null;
                }
                else if (string.IsNullOrWhiteSpace(fieldId) is false)
                {
                    targetKind = EditorGraphDiagnosticTargetKind.Field;
                }
                else if (string.IsNullOrWhiteSpace(portId) is false)
                {
                    targetKind = EditorGraphDiagnosticTargetKind.Port;
                }
                else if (string.IsNullOrWhiteSpace(nodeId) is false)
                {
                    targetKind = EditorGraphDiagnosticTargetKind.Node;
                }

                return new EditorGraphDiagnostic(
                    StableId(source, severity, message),
                    severity,
                    targetKind,
                    m_Stale ? $"{message}（需重新编译确认）" : message,
                    m_Stale ? $"{tooltip}\n图已修改，请重新编译确认。" : tooltip,
                    nodeId,
                    fieldId,
                    portId,
                    wireId,
                    m_Stale);
            }

            private string ResolveWireId(StoryEditorDiagnosticLocation location)
            {
                if (string.IsNullOrWhiteSpace(location.WireId) is false &&
                    m_CurrentChapter != null &&
                    m_CurrentChapter.Edges.Any(x =>
                        x != null &&
                        string.Equals(x.EdgeId, location.WireId, StringComparison.Ordinal) &&
                        x.TargetKind == TransitionTargetKind.Node &&
                        string.IsNullOrWhiteSpace(x.TargetNodeId) is false &&
                        m_CurrentChapter.Nodes.Any(node => node != null && string.Equals(node.NodeId, x.TargetNodeId, StringComparison.Ordinal))))
                {
                    return location.WireId;
                }

                if (string.IsNullOrWhiteSpace(location.WireId) is false)
                {
                    return null;
                }

                return null;
            }

            private bool IsCurrentChapter(StoryEditorDiagnosticLocation location)
            {
                if (m_CurrentChapter == null || string.IsNullOrWhiteSpace(location.ChapterId))
                {
                    return false;
                }

                return string.Equals(m_CurrentChapter.ChapterId, location.ChapterId, StringComparison.Ordinal);
            }

            private static string StableId(string source, EditorGraphDiagnosticSeverity severity, string message)
            {
                return $"{source}|{severity}|{message}";
            }
        }

        private static StoryEditorDiagnosticLocation ParseSource(string source)
        {
            string storyId = null;
            string chapterId = null;
            string nodeId = null;
            string fieldId = null;
            string portId = null;
            string wireId = null;
            var parts = (source ?? string.Empty).Split('/');
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var split = part.IndexOf(':');
                if (split <= 0)
                {
                    continue;
                }

                var key = part.Substring(0, split);
                var value = part.Substring(split + 1);
                switch (key)
                {
                    case "story":
                        storyId = value;
                        break;
                    case "chapter":
                        chapterId = value;
                        break;
                    case "node":
                        nodeId = value;
                        break;
                    case "field":
                        fieldId = value;
                        break;
                    case "port":
                        portId = value;
                        break;
                    case "edge":
                        wireId = value;
                        break;
                }
            }

            return new StoryEditorDiagnosticLocation(storyId, chapterId, nodeId, fieldId, portId, wireId);
        }

        private static string BuildSource(StoryEditorDiagnosticLocation location)
        {
            var source = $"story:{location.StoryId ?? string.Empty}/chapter:{location.ChapterId ?? string.Empty}";
            if (string.IsNullOrWhiteSpace(location.NodeId) is false)
            {
                source += $"/node:{location.NodeId}";
            }

            if (string.IsNullOrWhiteSpace(location.WireId) is false)
            {
                source += $"/edge:{location.WireId}";
            }

            if (string.IsNullOrWhiteSpace(location.FieldId) is false)
            {
                source += $"/field:{location.FieldId}";
            }

            if (string.IsNullOrWhiteSpace(location.PortId) is false)
            {
                source += $"/port:{location.PortId}";
            }

            return source;
        }

        private static EditorGraphDiagnosticSeverity ToSeverity(StoryValidationSeverity severity)
        {
            switch (severity)
            {
                case StoryValidationSeverity.Error:
                    return EditorGraphDiagnosticSeverity.Error;
                case StoryValidationSeverity.Warning:
                    return EditorGraphDiagnosticSeverity.Warning;
                default:
                    return EditorGraphDiagnosticSeverity.Info;
            }
        }

        private static string TranslateMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "未知剧情问题。";
            }

            if (message.StartsWith("Parallel branch must wait on the same Merge node.", StringComparison.Ordinal))
            {
                return "并行轨道如果接入等待节点，必须接入同一个“等待全部完成”。";
            }

            if (message.StartsWith("Merge node cannot belong to multiple Parallel blocks.", StringComparison.Ordinal))
            {
                return "等待全部完成节点必须只属于一个并行块。";
            }

            if (message.StartsWith("Video clip path does not match video source.", StringComparison.Ordinal))
            {
                return "视频路径与来源不匹配。";
            }

            switch (message)
            {
                case "Required command field is missing.":
                    return "必填命令字段未填写。";
                case "Command field must be a number.":
                case "Wait duration must be a number.":
                    return "字段必须填写数字。";
                case "Command field must be a boolean.":
                    return "字段必须填写布尔值。";
                case "Command field must use a valid option.":
                    return "字段必须使用有效选项。";
                case "Asset reference uses a manual string fallback.":
                    return "资源引用不是项目资源路径。";
                case "Choice item node must have exactly one selected target.":
                    return "选项必须且只能连接一个“选择后”目标。";
                case "Line completed output cannot mix choice items and direct flow targets.":
                case "Merge completed port cannot connect choices and ordinary targets at the same time.":
                case "Wait completed output cannot mix choice items and direct flow targets.":
                    return "完成端口不能同时连接选项和普通流程。";
                case "Node kind is no longer supported in Story default authoring path.":
                    return "节点已退出默认作者路径。";
                case "Parallel node must define a valid branch block.":
                    return "并行节点没有有效分支块。";
                case "Parallel node must have at least two branch outputs.":
                    return "并行节点至少需要两个轨道。";
                case "Parallel branch port id must be unique.":
                    return "并行轨道端口 ID 必须唯一。";
                case "Parallel output must use a branch port.":
                    return "并行节点只能从轨道端口连出。";
                case "Parallel branch must target a node in the same chapter.":
                    return "并行轨道必须连接到当前章节内的节点。";
                case "Parallel branch must connect to a Merge node.":
                    return "并行轨道可以自然结束；需要继续后续流程时请接入“等待全部完成”。";
                case "Parallel branch cannot contain Choice before Merge.":
                    return "对白或旁白可以连接选项；这是旧诊断，请重新编译刷新。";
                case "Nested Parallel blocks are not supported.":
                    return "暂不支持嵌套并行块。";
                case "Parallel branch cannot jump to another chapter before Merge.":
                    return "并行轨道在等待全部完成前不能跳转章节。";
                case "Parallel branch cannot end the story before Merge.":
                    return "并行轨道在等待全部完成前不能结束剧情。";
                case "Parallel branch must stay in the same chapter.":
                    return "并行轨道必须留在当前章节。";
                case "Parallel branch target is invalid.":
                    return "并行轨道目标无效。";
                case "All paths in a Parallel branch must reach the same Merge node.":
                    return "并行轨道内的所有等待路径必须到达同一个“等待全部完成”。";
                case "Merge node must belong to exactly one Parallel block.":
                    return "等待全部完成节点必须只属于一个并行块。";
                case "Merge node must connect a completed target.":
                    return "等待全部完成节点可以不连接后续目标。";
                case "Merge node must have only one completed target.":
                    return "等待全部完成节点只能有一个完成目标。";
                default:
                    return message;
            }
        }

        private static bool IsRecommendedAssetReference(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value) != null;
            }

            return false;
        }

        private static string GetParameterValue(StoryAuthoringNode node, string key)
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
    }
}

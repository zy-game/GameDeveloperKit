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
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Logic;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Logic;
using GameDeveloperKit.StoryEditor.Validation;

namespace GameDeveloperKit.StoryEditor.Graph
{
    internal sealed class DiagnosticSet
    {
        public static readonly DiagnosticSet Empty = new DiagnosticSet(Array.Empty<DiagnosticItem>());

        private readonly IReadOnlyList<DiagnosticItem> m_Items;

        public DiagnosticSet(IReadOnlyList<DiagnosticItem> items)
        {
            m_Items = items ?? Array.Empty<DiagnosticItem>();
        }

        public IReadOnlyList<DiagnosticItem> Items => m_Items;

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

        private IReadOnlyList<EditorGraphDiagnostic> Find(Func<DiagnosticItem, bool> predicate)
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

    internal sealed class DiagnosticItem
    {
        public DiagnosticItem(
            EditorGraphDiagnostic graphDiagnostic,
            DiagnosticLocation location,
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

        public DiagnosticLocation Location { get; }

        public string Source { get; }

        public string OriginalMessage { get; }

        public bool VisibleOnCurrentGraph { get; }

        public string SummaryText
        {
            get
            {
                var prefix = GraphDiagnostic.Severity == EditorGraphDiagnosticSeverity.Error ? "错误" :
                    GraphDiagnostic.Severity == EditorGraphDiagnosticSeverity.Warning ? "警告" : "提示";
                if (!VisibleOnCurrentGraph && string.IsNullOrWhiteSpace(Location.EpisodeId) is false)
                {
                    return $"{prefix}：{GraphDiagnostic.Message}（章节：{Location.EpisodeId}）";
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

    internal readonly struct DiagnosticLocation
    {
        public DiagnosticLocation(
            string storyId,
            string episodeId,
            string nodeId,
            string fieldId,
            string portId,
            string wireId)
        {
            StoryId = storyId;
            EpisodeId = episodeId;
            NodeId = nodeId;
            FieldId = fieldId;
            PortId = portId;
            WireId = wireId;
        }

        public string StoryId { get; }

        public string EpisodeId { get; }

        public string NodeId { get; }

        public string FieldId { get; }

        public string PortId { get; }

        public string WireId { get; }
    }

    internal static class Diagnostics
    {
        public static DiagnosticSet BuildLocal(
            AuthoringAsset asset,
            AuthoringEpisode currentEpisode,
            LogicDefinitionCatalog logicDefinitions = null)
        {
            var builder = new Builder(asset, currentEpisode, false, logicDefinitions);
            builder.AddLocalDiagnostics();
            return builder.Build();
        }

        public static DiagnosticSet FromReport(
            ValidationReport report,
            AuthoringAsset asset,
            AuthoringEpisode currentEpisode,
            bool stale)
        {
            var builder = new Builder(asset, currentEpisode, stale, null);
            var issues = report?.Issues ?? Array.Empty<ValidationIssue>();
            for (var i = 0; i < issues.Count; i++)
            {
                builder.AddReportIssue(issues[i]);
            }

            return builder.Build();
        }

        public static DiagnosticSet FromCompiledProgram(
            Program program,
            AuthoringAsset asset,
            AuthoringEpisode currentEpisode)
        {
            var builder = new Builder(asset, currentEpisode, false, null);
            return builder.Build();
        }

        private sealed class Builder
        {
            private readonly AuthoringAsset m_Asset;
            private readonly AuthoringEpisode m_CurrentEpisode;
            private readonly bool m_Stale;
            private readonly LogicDefinitionCatalog m_LogicDefinitions;
            private readonly List<DiagnosticItem> m_Items = new List<DiagnosticItem>();
            private readonly HashSet<string> m_Keys = new HashSet<string>(StringComparer.Ordinal);

            public Builder(
                AuthoringAsset asset,
                AuthoringEpisode currentEpisode,
                bool stale,
                LogicDefinitionCatalog logicDefinitions)
            {
                m_Asset = asset;
                m_CurrentEpisode = currentEpisode;
                m_Stale = stale;
                m_LogicDefinitions = logicDefinitions ?? LogicDefinitionCatalog.Shared;
            }

            public DiagnosticSet Build()
            {
                return new DiagnosticSet(m_Items);
            }

            public void AddLocalDiagnostics()
            {
                if (m_CurrentEpisode == null)
                {
                    return;
                }

                for (var i = 0; i < m_LogicDefinitions.Errors.Count; i++)
                {
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Error,
                        "代码节点目录存在错误。",
                        m_LogicDefinitions.Errors[i],
                        new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, null, null, null, null));
                }

                var nodes = m_CurrentEpisode.Nodes
                    .Where(x => x != null && string.IsNullOrWhiteSpace(x.NodeId) is false)
                    .GroupBy(x => x.NodeId, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

                foreach (var node in nodes.Values)
                {
                    AddNodeFieldDiagnostics(node);
                }

                for (var i = 0; i < m_CurrentEpisode.Edges.Count; i++)
                {
                    AddEdgeDiagnostics(m_CurrentEpisode.Edges[i], nodes);
                }

                AddChoiceOwnerMixDiagnostics(nodes);
                AddParallelDiagnostics(nodes);
            }

            public void AddReportIssue(ValidationIssue issue)
            {
                if (issue == null)
                {
                    return;
                }

                var location = ParseSource(issue.Source);
                var severity = ToSeverity(issue.Severity);
                var message = TranslateMessage(issue.Message);
                var visible = IsCurrentEpisode(location);
                var diagnostic = CreateDiagnostic(issue.Source, severity, message, message, location, visible);
                AddItem(diagnostic, location, issue.Source, issue.Message, visible);
            }

            private void AddNodeFieldDiagnostics(AuthoringNode node)
            {
                if (NodeSchemaRegistry.TryGet(node.NodeKind, out _) is false)
                {
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Error,
                        "节点类型未注册。",
                        "节点类型没有对应的 schema，无法编译到运行时。",
                        new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, node.NodeId, null, null, null));
                    return;
                }

                var schema = NodeSchemaResolver.Resolve(node, m_LogicDefinitions);

                if (NodeSchemaRegistry.IsDefaultAuthoringNode(node.NodeKind) is false)
                {
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Error,
                        "节点已退出默认作者路径。",
                        "该节点不再作为 Story 默认剧情节点使用。请改用内容、媒体、音频、等待、选项、小游戏、事件或章节跳转节点。",
                        new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, node.NodeId, null, null, null));
                }

                if (node.NodeKind == NodeKind.Logic)
                {
                    AddLogicNodeDiagnostics(node);
                }

                for (var i = 0; i < schema.Parameters.Count; i++)
                {
                    var parameter = schema.Parameters[i];
                    var value = GetParameterValue(node, parameter.Key);
                    var location = new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, node.NodeId, parameter.Key, null, null);
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
                                 string.Equals(parameter.Key, MediaCommandNames.ClipArgument, StringComparison.Ordinal) is false) &&
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

            private void AddLogicNodeDiagnostics(AuthoringNode node)
            {
                var logicId = GetParameterValue(node, LogicCommandCodec.LogicIdParameter);
                var logicLocation = new DiagnosticLocation(
                    m_Asset?.StoryId,
                    m_CurrentEpisode.EpisodeId,
                    node.NodeId,
                    LogicCommandCodec.LogicIdParameter,
                    null,
                    null);
                if (string.IsNullOrWhiteSpace(logicId))
                {
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Error,
                        "代码节点尚未选择逻辑定义。",
                        "请选择一个有效的代码逻辑；现有参数和连线不会被自动删除。",
                        logicLocation);
                }

                m_LogicDefinitions.TryGet(logicId, out var definition);
                if (string.IsNullOrWhiteSpace(logicId) is false && definition == null)
                {
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Error,
                        "代码节点定义不存在。",
                        $"找不到 LogicId“{logicId}”对应的代码节点定义；原参数和连线已保留。",
                        logicLocation);
                }

                var declaredKeys = new HashSet<string>(StringComparer.Ordinal)
                {
                    LogicCommandCodec.LogicIdParameter
                };
                if (definition != null)
                {
                    for (var i = 0; i < definition.Parameters.Count; i++)
                    {
                        declaredKeys.Add(definition.Parameters[i].Key);
                        if (definition.FieldRendererKeys.TryGetValue(
                                definition.Parameters[i].Key,
                                out var rendererKey) &&
                            IsLogicRendererAvailable(rendererKey) is false)
                        {
                            AddLocal(
                                EditorGraphDiagnosticSeverity.Error,
                                "代码节点自定义字段渲染器未注册。",
                                $"字段“{definition.Parameters[i].Label}”需要渲染器“{rendererKey}”。",
                                new DiagnosticLocation(
                                    m_Asset?.StoryId,
                                    m_CurrentEpisode.EpisodeId,
                                    node.NodeId,
                                    definition.Parameters[i].Key,
                                    null,
                                    null));
                        }
                    }
                }

                for (var i = 0; i < node.Parameters.Count; i++)
                {
                    var parameter = node.Parameters[i];
                    if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key) ||
                        declaredKeys.Contains(parameter.Key))
                    {
                        continue;
                    }

                    AddLocal(
                        EditorGraphDiagnosticSeverity.Error,
                        "代码节点包含已失效参数。",
                        $"参数“{parameter.Key}”不属于当前 LogicId“{logicId}”的定义；数据已保留，请确认后修复。",
                        new DiagnosticLocation(
                            m_Asset?.StoryId,
                            m_CurrentEpisode.EpisodeId,
                            node.NodeId,
                            parameter.Key,
                            null,
                            null));
                }
            }

            private void AddPlayVideoFieldDiagnostics(AuthoringNode node)
            {
                var clip = GetParameterValue(node, MediaCommandNames.ClipArgument);
                if (string.IsNullOrWhiteSpace(clip))
                {
                    return;
                }

                if (VideoReferenceCodec.TryDeserialize(clip, out _, out _))
                {
                    return;
                }

                var source = GetParameterValue(node, MediaCommandNames.VideoSourceArgument);
                var legacyArguments = new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [MediaCommandNames.VideoSourceArgument] = Value.FromString(source),
                    [MediaCommandNames.ClipArgument] = Value.FromString(clip)
                });
                if (VideoReferenceCodec.TryDeserializeCommand(legacyArguments, out _, out var legacy, out var errorMessage) && legacy)
                {
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Warning,
                        "旧视频引用待迁移。",
                        "该 StreamingAssets 视频仍可编译；请用视频选择器重新选择以写入完整引用。",
                        new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, node.NodeId, MediaCommandNames.ClipArgument, null, null));
                    return;
                }

                AddLocal(
                    EditorGraphDiagnosticSeverity.Error,
                    "视频引用无效。",
                    $"视频只支持 CDN 绝对 HTTPS URL 或 StreamingAssets 相对路径：{errorMessage}",
                    new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, node.NodeId, MediaCommandNames.ClipArgument, null, null));
            }

            private void AddEdgeDiagnostics(AuthoringEdge edge, IReadOnlyDictionary<string, AuthoringNode> nodes)
            {
                if (edge == null)
                {
                    return;
                }

                nodes.TryGetValue(edge.FromNodeId ?? string.Empty, out var fromNode);
                var baseLocation = new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, edge.FromNodeId, null, edge.FromPortId, edge.EdgeId);
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
                        new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, fromNode.NodeId, null, edge.FromPortId, edge.EdgeId));
                }

                var hasDeclaredOutput = fromNode.NodeKind == NodeKind.Logic
                    ? HasDeclaredLogicOutput(fromNode, edge.FromPortId)
                    : PortPolicy.HasDeclaredOutputPort(fromNode, edge.FromPortId);
                if (hasDeclaredOutput is false)
                {
                    AddLocal(
                        EditorGraphDiagnosticSeverity.Error,
                        fromNode.NodeKind == NodeKind.Logic ? "代码节点出口已失效。" : "输出端口未在节点 schema 中声明。",
                        fromNode.NodeKind == NodeKind.Logic
                            ? $"出口“{edge.FromPortId}”不属于当前代码节点定义；连线已保留，请确认后修复。"
                            : $"端口“{edge.FromPortId}”不是该节点的输出端口。",
                        baseLocation);
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
                            "这条连线的目标节点已退出默认作者路径，请改用基础表现、等待、选项或代码节点。",
                            new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, targetNode.NodeId, null, null, edge.EdgeId));
                    }
                }
                else if (edge.TargetKind != TransitionTargetKind.StoryEnd)
                {
                    AddLocal(EditorGraphDiagnosticSeverity.Error, "连线目标类型无效。", "跨剧情段目标只能通过卷路线编辑器配置。", baseLocation);
                }
            }

            private bool HasDeclaredLogicOutput(AuthoringNode node, string portId)
            {
                var schema = LogicNodeSchemaResolver.Resolve(node, m_LogicDefinitions);
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

            private static bool IsLogicRendererAvailable(string rendererKey)
            {
                return string.Equals(rendererKey, "story.video-reference", StringComparison.Ordinal) ||
                       string.Equals(rendererKey, "story.audio-reference", StringComparison.Ordinal) ||
                       string.Equals(rendererKey, "story.text-reference", StringComparison.Ordinal) ||
                       LogicParameterRendererRegistry.IsRegistered(rendererKey);
            }

            private void AddChoiceOwnerMixDiagnostics(IReadOnlyDictionary<string, AuthoringNode> nodes)
            {
                foreach (var node in nodes.Values)
                {
                    if (node.NodeKind != NodeKind.Dialogue &&
                        node.NodeKind != NodeKind.Narration &&
                        node.NodeKind != NodeKind.Wait)
                    {
                        continue;
                    }

                    var hasChoice = false;
                    var hasNonChoice = false;
                    for (var i = 0; i < m_CurrentEpisode.Edges.Count; i++)
                    {
                        var edge = m_CurrentEpisode.Edges[i];
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
                            "对白、旁白或等待节点接选项时，completed 端口不能再直连普通节点。",
                            new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, node.NodeId, null, "completed", null));
                    }
                }
            }

            private void AddParallelDiagnostics(IReadOnlyDictionary<string, AuthoringNode> nodes)
            {
                foreach (var node in nodes.Values)
                {
                    if (node.NodeKind != NodeKind.Parallel)
                    {
                        continue;
                    }

                    var branches = GetOutgoingEdges(node.NodeId)
                        .Where(x => x != null && (string.Equals(x.FromPortId, "branch", StringComparison.Ordinal) || PortPolicy.IsParallelBranchPort(x.FromPortId)))
                        .ToList();
                    if (branches.Count < 2)
                    {
                        AddLocal(
                            EditorGraphDiagnosticSeverity.Error,
                            "并行节点至少需要两个轨道。",
                            "Parallel 必须通过两个或更多轨道端口连接到当前章节内的节点。",
                            new DiagnosticLocation(m_Asset?.StoryId, m_CurrentEpisode.EpisodeId, node.NodeId, null, "branch", null));
                    }

                }
            }

            private int CountOutgoing(string nodeId, string portId)
            {
                var count = 0;
                for (var i = 0; i < m_CurrentEpisode.Edges.Count; i++)
                {
                    var edge = m_CurrentEpisode.Edges[i];
                    if (edge != null &&
                        string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal) &&
                        string.Equals(edge.FromPortId, portId, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }

                return count;
            }

            private List<AuthoringEdge> GetOutgoingEdges(string nodeId)
            {
                var edges = new List<AuthoringEdge>();
                for (var i = 0; i < m_CurrentEpisode.Edges.Count; i++)
                {
                    var edge = m_CurrentEpisode.Edges[i];
                    if (edge != null && string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal))
                    {
                        edges.Add(edge);
                    }
                }

                return edges;
            }

            private AuthoringEpisode FindEpisode(string episodeId)
            {
                if (m_Asset == null || string.IsNullOrWhiteSpace(episodeId))
                {
                    return null;
                }

                for (var i = 0; i < m_Asset.Episodes.Count; i++)
                {
                    var episode = m_Asset.Episodes[i];
                    if (episode != null && string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))
                    {
                        return episode;
                    }
                }

                return null;
            }

            private void AddLocal(
                EditorGraphDiagnosticSeverity severity,
                string message,
                string tooltip,
                DiagnosticLocation location)
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

            private void AddItem(
                EditorGraphDiagnostic diagnostic,
                DiagnosticLocation location,
                string source,
                string originalMessage,
                bool visibleOnCurrentGraph)
            {
                var key = $"{diagnostic.TargetKind}:{diagnostic.NodeId}:{diagnostic.FieldId}:{diagnostic.PortId}:{diagnostic.WireId}:{diagnostic.Severity}:{diagnostic.Message}:{diagnostic.Stale}";
                if (m_Keys.Add(key) is false)
                {
                    return;
                }

                m_Items.Add(new DiagnosticItem(diagnostic, location, source, originalMessage, visibleOnCurrentGraph));
            }

            private EditorGraphDiagnostic CreateDiagnostic(
                string source,
                EditorGraphDiagnosticSeverity severity,
                string message,
                string tooltip,
                DiagnosticLocation location,
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

            private string ResolveWireId(DiagnosticLocation location)
            {
                if (string.IsNullOrWhiteSpace(location.WireId) is false &&
                    m_CurrentEpisode != null &&
                    m_CurrentEpisode.Edges.Any(x =>
                        x != null &&
                        string.Equals(x.EdgeId, location.WireId, StringComparison.Ordinal) &&
                        x.TargetKind == TransitionTargetKind.Node &&
                        string.IsNullOrWhiteSpace(x.TargetNodeId) is false &&
                        m_CurrentEpisode.Nodes.Any(node => node != null && string.Equals(node.NodeId, x.TargetNodeId, StringComparison.Ordinal))))
                {
                    return location.WireId;
                }

                if (string.IsNullOrWhiteSpace(location.WireId) is false)
                {
                    return null;
                }

                return null;
            }

            private bool IsCurrentEpisode(DiagnosticLocation location)
            {
                if (m_CurrentEpisode == null || string.IsNullOrWhiteSpace(location.EpisodeId))
                {
                    return false;
                }

                return string.Equals(m_CurrentEpisode.EpisodeId, location.EpisodeId, StringComparison.Ordinal);
            }

            private static string StableId(string source, EditorGraphDiagnosticSeverity severity, string message)
            {
                return $"{source}|{severity}|{message}";
            }
        }

        private static DiagnosticLocation ParseSource(string source)
        {
            string storyId = null;
            string episodeId = null;
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
                    case "episode":
                        episodeId = value;
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

            return new DiagnosticLocation(storyId, episodeId, nodeId, fieldId, portId, wireId);
        }

        private static string BuildSource(DiagnosticLocation location)
        {
            var source = $"story:{location.StoryId ?? string.Empty}/episode:{location.EpisodeId ?? string.Empty}";
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

        private static EditorGraphDiagnosticSeverity ToSeverity(ValidationSeverity severity)
        {
            switch (severity)
            {
                case ValidationSeverity.Error:
                    return EditorGraphDiagnosticSeverity.Error;
                case ValidationSeverity.Warning:
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

            if (message.StartsWith("Video clip path does not match video source.", StringComparison.Ordinal))
            {
                return "视频路径与来源不匹配。";
            }

            if (message.StartsWith("Volume route requires at least one Episode.", StringComparison.Ordinal))
            {
                return "卷路线至少需要包含一个剧情段。";
            }

            if (message.StartsWith("Route layout references an unknown Episode.", StringComparison.Ordinal))
            {
                return "路线布局引用了不存在的剧情段。";
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
                case "Parallel branch must target a node in the same episode.":
                    return "并行轨道必须连接到当前章节内的节点。";
                case "Parallel branch target is invalid.":
                    return "并行轨道目标无效。";
                case "Parallel branch contains a cycle before it ends.":
                    return "并行轨道在自然结束前存在循环。";
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
    }
}

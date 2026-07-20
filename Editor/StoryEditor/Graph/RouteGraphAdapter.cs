using System;
using System.Collections.Generic;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.Graph
{
    internal sealed class RouteGraphAdapter : IEditorNodeGraphAdapter
    {
        private const string RootPortId = "root";
        private const string InputPortId = "in";
        private const float HorizontalSpacing = 360f;
        private const float VerticalSpacing = 190f;

        private static readonly Color s_RootPortColor = new Color(0.38f, 0.78f, 0.64f);
        private static readonly Color s_EpisodePortColor = new Color(0.36f, 0.68f, 0.9f);

        private readonly RouteGraphActions m_Actions;
        private readonly Dictionary<string, Vector2> m_SessionPositions =
            new Dictionary<string, Vector2>(StringComparer.Ordinal);

        private AuthoringVolume m_Volume;
        private Volume m_CompiledVolume;
        private ValidationReport m_Report;
        private string m_SelectedNodeId;
        private string m_SelectedWireId;
        private AuthoringRouteLayout m_Layout;

        public RouteGraphAdapter(RouteGraphActions actions)
        {
            m_Actions = actions ?? new RouteGraphActions();
        }

        public IReadOnlyList<EditorGraphNodeModel> Nodes => BuildNodes();

        public IReadOnlyList<EditorGraphWireModel> Wires => BuildWires();

        public IReadOnlyList<EditorGraphNodeTemplate> Templates => Array.Empty<EditorGraphNodeTemplate>();

        public EditorGraphCanvasModel Canvas => m_Layout == null
            ? null
            : new EditorGraphCanvasModel(
                new Vector2(m_Layout.ReferenceWidth, m_Layout.ReferenceHeight),
                m_Layout.BackgroundImage,
                m_Layout.EditorGuideImage);

        internal string VirtualRootNodeId => GetVirtualRootNodeId(m_Volume?.VolumeId);

        internal void SetRoute(
            AuthoringVolume volume,
            Volume compiledVolume,
            ValidationReport report,
            string selectedNodeId,
            AuthoringRouteLayout layout = null,
            string selectedWireId = null)
        {
            m_Volume = volume;
            m_CompiledVolume = compiledVolume != null &&
                               string.Equals(compiledVolume.VolumeId, volume?.VolumeId, StringComparison.Ordinal)
                ? compiledVolume
                : null;
            m_Report = report;
            m_Layout = layout;
            m_SelectedNodeId = ContainsNode(selectedNodeId) ? selectedNodeId : VirtualRootNodeId;
            m_SelectedWireId = ContainsWire(selectedWireId) ? selectedWireId : null;
            EnsureAutomaticPositions();
        }

        internal static string GetVirtualRootNodeId(string volumeId)
        {
            return $"route-root:{volumeId ?? string.Empty}";
        }

        internal bool IsVirtualRoot(string nodeId)
        {
            return string.Equals(nodeId, VirtualRootNodeId, StringComparison.Ordinal);
        }

        internal bool ContainsEpisode(string episodeId)
        {
            if (m_Volume?.Episodes == null || string.IsNullOrWhiteSpace(episodeId))
            {
                return false;
            }

            for (var i = 0; i < m_Volume.Episodes.Count; i++)
            {
                if (string.Equals(m_Volume.Episodes[i]?.EpisodeId, episodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public VisualElement CreateBlackboard()
        {
            var blackboard = new VisualElement();
            blackboard.AddToClassList("story-route-blackboard");

            if (m_CompiledVolume == null)
            {
                var errorCount = CountErrors(m_Report);
                blackboard.Add(new Label(errorCount > 0
                    ? $"路线不可用：编译存在 {errorCount} 个错误。"
                    : "路线不可用：没有有效的编译结果。"));
                blackboard.AddToClassList("story-route-blackboard--error");
                return blackboard;
            }

            blackboard.Add(new Label(
                $"{SafeText(m_Volume?.Title, m_Volume?.VolumeId)} · {m_Volume?.Episodes.Count ?? 0} 个剧情段 · {m_CompiledVolume.Route.Edges.Count} 条路线"));
            return blackboard;
        }

        public VisualElement CreateCustomField(
            string nodeId,
            EditorGraphFieldModel field,
            Action<string> valueChanged)
        {
            return null;
        }

        public EditorGraphConnectionResult CanConnect(EditorGraphPortRef output, EditorGraphPortRef input)
        {
            return EditorGraphConnectionResult.Fail("路线视图为只读。");
        }

        public void CreateNode(EditorGraphNodeTemplate template, Vector2 graphPosition, EditorGraphPortRef connectFrom)
        {
        }

        public void MoveNode(string nodeId, Vector2 graphPosition)
        {
            if (ContainsNode(nodeId) is false)
            {
                return;
            }

            if (m_Layout == null)
            {
                m_SessionPositions[nodeId] = graphPosition;
                return;
            }

            m_Actions.MoveNodes?.Invoke(new[] { new EditorNodeGraphMove(nodeId, graphPosition) });
        }

        public void MoveNodes(IReadOnlyList<EditorNodeGraphMove> moves)
        {
            if (moves == null)
            {
                return;
            }

            if (m_Layout != null)
            {
                m_Actions.MoveNodes?.Invoke(moves);
                return;
            }

            for (var i = 0; i < moves.Count; i++)
            {
                MoveNode(moves[i].NodeId, moves[i].Position);
            }
        }

        public void SelectNode(string nodeId)
        {
            if (ContainsNode(nodeId) is false)
            {
                return;
            }

            m_SelectedNodeId = nodeId;
            m_SelectedWireId = null;
            m_Actions.SelectedNode?.Invoke(nodeId);
        }

        public void ActivateNode(string nodeId)
        {
            if (ContainsEpisode(nodeId))
            {
                m_Actions.ActivatedNode?.Invoke(nodeId);
            }
        }

        public bool PopulateNodeContextMenu(string nodeId, GenericMenu menu)
        {
            if (menu == null || ContainsNode(nodeId) is false)
            {
                return false;
            }

            if (IsVirtualRoot(nodeId))
            {
                menu.AddItem(new GUIContent("添加首层剧情段"), false, () => m_Actions.AddRootEpisode?.Invoke());
                return true;
            }

            var populated = false;
            var episode = FindAuthoringEpisode(nodeId);
            var exits = BuildAuthoringExits(episode);
            for (var i = 0; i < exits.Count; i++)
            {
                var exit = exits[i];
                if (IsExitBound(nodeId, exit.ExitId))
                {
                    continue;
                }

                var exitId = exit.ExitId;
                var label = SafeText(exit.DisplayName, exitId).Replace('/', '／');
                menu.AddItem(
                    new GUIContent($"添加分支剧情/{label}"),
                    false,
                    () => m_Actions.AddChildEpisode?.Invoke(nodeId, exitId));
                populated = true;
            }

            if (HasChildren(nodeId) is false)
            {
                if (populated)
                {
                    menu.AddSeparator(string.Empty);
                }

                menu.AddItem(new GUIContent("删除剧情段"), false, () => m_Actions.RemoveEpisode?.Invoke(nodeId));
                populated = true;
            }

            return populated;
        }

        public void SelectNodes(IReadOnlyList<string> nodeIds)
        {
            if (nodeIds != null && nodeIds.Count == 1)
            {
                SelectNode(nodeIds[0]);
            }
        }

        public void SelectWire(string wireId)
        {
            if (ContainsWire(wireId))
            {
                m_SelectedWireId = wireId;
                m_Actions.SelectedWire?.Invoke(wireId);
            }
        }

        public void MoveWireControlPoint(string wireId, int pointIndex, Vector2 graphPosition)
        {
            var edge = FindEdgePlacement(wireId);
            if (edge == null || pointIndex < 0 || pointIndex >= edge.ControlPoints.Count)
            {
                return;
            }

            var points = CopyPoints(edge);
            points[pointIndex] = graphPosition;
            m_Actions.UpdateEdgePath?.Invoke(wireId, points, edge.StyleKey);
        }

        public void InsertWireControlPoint(string wireId, int segmentIndex, Vector2 graphPosition)
        {
            var edge = FindEdgePlacement(wireId);
            if (edge == null)
            {
                return;
            }

            var points = CopyPoints(edge);
            points.Insert(Mathf.Clamp(segmentIndex, 0, points.Count), graphPosition);
            m_Actions.UpdateEdgePath?.Invoke(wireId, points, edge.StyleKey);
        }

        public void RemoveWireControlPoint(string wireId, int pointIndex)
        {
            var edge = FindEdgePlacement(wireId);
            if (edge == null || pointIndex < 0 || pointIndex >= edge.ControlPoints.Count)
            {
                return;
            }

            var points = CopyPoints(edge);
            points.RemoveAt(pointIndex);
            m_Actions.UpdateEdgePath?.Invoke(wireId, points, edge.StyleKey);
        }

        public void Connect(EditorGraphPortRef output, EditorGraphPortRef input)
        {
        }

        public void Disconnect(string wireId)
        {
        }

        public void DeleteSelection()
        {
        }

        public void SetNodeField(string nodeId, string fieldId, string value)
        {
        }

        private IReadOnlyList<EditorGraphNodeModel> BuildNodes()
        {
            if (m_Volume == null)
            {
                return Array.Empty<EditorGraphNodeModel>();
            }

            var nodes = new List<EditorGraphNodeModel>(m_Volume.Episodes.Count + 1)
            {
                new EditorGraphNodeModel(
                    VirtualRootNodeId,
                    SafeText(m_Volume.Title, m_Volume.VolumeId),
                    "卷",
                    "路线根",
                    GetPosition(VirtualRootNodeId),
                    Array.Empty<EditorGraphPortModel>(),
                    new[]
                    {
                        new EditorGraphPortModel(
                            RootPortId,
                            "剧情段",
                            EditorGraphPortDirection.Output,
                            EditorGraphPortCapacity.Multiple,
                            s_RootPortColor)
                    },
                    Array.Empty<EditorGraphFieldModel>(),
                    selected: IsVirtualRoot(m_SelectedNodeId),
                    styleKey: "route-root")
            };

            for (var i = 0; i < m_Volume.Episodes.Count; i++)
            {
                var episode = m_Volume.Episodes[i];
                if (episode == null || string.IsNullOrWhiteSpace(episode.EpisodeId))
                {
                    continue;
                }

                nodes.Add(new EditorGraphNodeModel(
                    episode.EpisodeId,
                    SafeText(episode.Title, episode.EpisodeId),
                    "剧情段",
                    "路线",
                    GetPosition(episode.EpisodeId),
                    new[]
                    {
                        new EditorGraphPortModel(
                            InputPortId,
                            "进入",
                            EditorGraphPortDirection.Input,
                            EditorGraphPortCapacity.Single,
                            s_EpisodePortColor)
                    },
                    BuildExitPorts(episode),
                    Array.Empty<EditorGraphFieldModel>(),
                    selected: string.Equals(m_SelectedNodeId, episode.EpisodeId, StringComparison.Ordinal),
                    styleKey: "route-episode"));
            }

            return nodes;
        }

        private IReadOnlyList<EditorGraphWireModel> BuildWires()
        {
            var route = m_Volume?.Route;
            var wires = new List<EditorGraphWireModel>(route?.Edges.Count ?? 0);
            for (var i = 0; i < (route?.Edges.Count ?? 0); i++)
            {
                var edge = route.Edges[i];
                if (CanProjectEdge(edge) is false)
                {
                    continue;
                }

                var output = edge.SourceKind == RouteEdgeSourceKind.Root
                    ? new EditorGraphPortRef(VirtualRootNodeId, RootPortId)
                    : new EditorGraphPortRef(edge.FromEpisodeId, edge.FromExitId);
                wires.Add(new EditorGraphWireModel(
                    edge.EdgeId,
                    output,
                    new EditorGraphPortRef(edge.ToEpisodeId, InputPortId),
                    edge.SourceKind == RouteEdgeSourceKind.Root ? "根" : edge.FromExitId,
                    selected: string.Equals(m_SelectedWireId, edge.EdgeId, StringComparison.Ordinal),
                    controlPoints: GetControlPoints(edge.EdgeId),
                    styleKey: FindEdgePlacement(edge.EdgeId)?.StyleKey,
                    controlPointsEditable: m_Layout != null));
            }

            return wires;
        }

        private static IReadOnlyList<EditorGraphPortModel> BuildExitPorts(AuthoringEpisode episode)
        {
            var exits = BuildAuthoringExits(episode);
            var ports = new List<EditorGraphPortModel>(exits.Count);
            for (var i = 0; i < exits.Count; i++)
            {
                var exit = exits[i];
                ports.Add(new EditorGraphPortModel(
                    exit.ExitId,
                    SafeText(exit.DisplayName, exit.ExitId),
                    EditorGraphPortDirection.Output,
                    EditorGraphPortCapacity.Single,
                    s_EpisodePortColor));
            }

            return ports;
        }

        private static List<EpisodeExit> BuildAuthoringExits(AuthoringEpisode episode)
        {
            var exits = new List<EpisodeExit>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < (episode?.Nodes.Count ?? 0); i++)
            {
                var node = episode.Nodes[i];
                if (node != null &&
                    (node.NodeKind == NodeKind.Choice || node.NodeKind == NodeKind.End) &&
                    string.IsNullOrWhiteSpace(node.NodeId) is false &&
                    ids.Add(node.NodeId))
                {
                    exits.Add(new EpisodeExit(node.NodeId, SafeText(node.Title, node.NodeId)));
                }
            }

            return exits;
        }

        private AuthoringEpisode FindAuthoringEpisode(string episodeId)
        {
            for (var i = 0; i < (m_Volume?.Episodes.Count ?? 0); i++)
            {
                var episode = m_Volume.Episodes[i];
                if (episode != null && string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))
                {
                    return episode;
                }
            }

            return null;
        }

        private bool IsExitBound(string episodeId, string exitId)
        {
            for (var i = 0; i < (m_Volume?.Route?.Edges.Count ?? 0); i++)
            {
                var edge = m_Volume.Route.Edges[i];
                if (edge != null &&
                    edge.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                    string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal) &&
                    string.Equals(edge.FromExitId, exitId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasChildren(string episodeId)
        {
            for (var i = 0; i < (m_Volume?.Route?.Edges.Count ?? 0); i++)
            {
                var edge = m_Volume.Route.Edges[i];
                if (edge != null &&
                    edge.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                    string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureAutomaticPositions()
        {
            if (m_Volume == null || m_Layout != null)
            {
                return;
            }

            var depths = BuildDepths();
            var rowsByDepth = new Dictionary<int, int>();
            SetInitialPosition(VirtualRootNodeId, 0, rowsByDepth);
            for (var i = 0; i < m_Volume.Episodes.Count; i++)
            {
                var episodeId = m_Volume.Episodes[i]?.EpisodeId;
                if (string.IsNullOrWhiteSpace(episodeId))
                {
                    continue;
                }

                var depth = depths.TryGetValue(episodeId, out var value) ? value : 1;
                SetInitialPosition(episodeId, depth, rowsByDepth);
            }
        }

        private Dictionary<string, int> BuildDepths()
        {
            var depths = new Dictionary<string, int>(StringComparer.Ordinal);
            var unresolved = new List<AuthoringRouteEdge>(m_Volume?.Route?.Edges ?? new List<AuthoringRouteEdge>());
            while (unresolved.Count > 0)
            {
                var changed = false;
                for (var i = unresolved.Count - 1; i >= 0; i--)
                {
                    var edge = unresolved[i];
                    if (edge == null)
                    {
                        unresolved.RemoveAt(i);
                        changed = true;
                        continue;
                    }

                    if (edge.SourceKind == RouteEdgeSourceKind.Root)
                    {
                        depths[edge.ToEpisodeId] = 1;
                    }
                    else if (depths.TryGetValue(edge.FromEpisodeId, out var parentDepth))
                    {
                        depths[edge.ToEpisodeId] = parentDepth + 1;
                    }
                    else
                    {
                        continue;
                    }

                    unresolved.RemoveAt(i);
                    changed = true;
                }

                if (changed is false)
                {
                    break;
                }
            }

            return depths;
        }

        private void SetInitialPosition(string nodeId, int depth, IDictionary<int, int> rowsByDepth)
        {
            if (m_SessionPositions.ContainsKey(nodeId))
            {
                return;
            }

            rowsByDepth.TryGetValue(depth, out var row);
            m_SessionPositions[nodeId] = new Vector2(80f + depth * HorizontalSpacing, 80f + row * VerticalSpacing);
            rowsByDepth[depth] = row + 1;
        }

        private Vector2 GetPosition(string nodeId)
        {
            if (m_Layout != null)
            {
                if (IsVirtualRoot(nodeId))
                {
                    return m_Layout.RootPlacement?.Position ?? Vector2.zero;
                }

                for (var i = 0; i < m_Layout.Episodes.Count; i++)
                {
                    var placement = m_Layout.Episodes[i];
                    if (placement != null && string.Equals(placement.EpisodeId, nodeId, StringComparison.Ordinal))
                    {
                        return placement.Position?.Position ?? Vector2.zero;
                    }
                }
            }

            return m_SessionPositions.TryGetValue(nodeId, out var position) ? position : new Vector2(80f, 80f);
        }

        private bool ContainsWire(string wireId)
        {
            for (var i = 0; i < (m_Volume?.Route?.Edges.Count ?? 0); i++)
            {
                if (string.Equals(m_Volume.Route.Edges[i]?.EdgeId, wireId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanProjectEdge(AuthoringRouteEdge edge)
        {
            if (edge == null || string.IsNullOrWhiteSpace(edge.EdgeId) || ContainsEpisode(edge.ToEpisodeId) is false)
            {
                return false;
            }

            if (edge.SourceKind == RouteEdgeSourceKind.Root)
            {
                return true;
            }

            if (edge.SourceKind != RouteEdgeSourceKind.EpisodeExit || ContainsEpisode(edge.FromEpisodeId) is false)
            {
                return false;
            }

            var exits = BuildAuthoringExits(FindAuthoringEpisode(edge.FromEpisodeId));
            for (var i = 0; i < exits.Count; i++)
            {
                if (string.Equals(exits[i].ExitId, edge.FromExitId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private AuthoringRouteEdgePlacement FindEdgePlacement(string edgeId)
        {
            for (var i = 0; i < (m_Layout?.Edges.Count ?? 0); i++)
            {
                if (m_Layout.Edges[i] != null &&
                    string.Equals(m_Layout.Edges[i].EdgeId, edgeId, StringComparison.Ordinal))
                {
                    return m_Layout.Edges[i];
                }
            }

            return null;
        }

        private IReadOnlyList<Vector2> GetControlPoints(string edgeId)
        {
            return CopyPoints(FindEdgePlacement(edgeId));
        }

        private static List<Vector2> CopyPoints(AuthoringRouteEdgePlacement edge)
        {
            var result = new List<Vector2>();
            for (var i = 0; i < (edge?.ControlPoints.Count ?? 0); i++)
            {
                if (edge.ControlPoints[i] != null)
                {
                    result.Add(edge.ControlPoints[i].Position);
                }
            }

            return result;
        }

        private bool ContainsNode(string nodeId)
        {
            return IsVirtualRoot(nodeId) || ContainsEpisode(nodeId);
        }

        private static int CountErrors(ValidationReport report)
        {
            var count = 0;
            if (report == null)
            {
                return count;
            }

            for (var i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Severity == ValidationSeverity.Error)
                {
                    count++;
                }
            }

            return count;
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }
}

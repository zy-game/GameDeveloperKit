using System;
using System.Collections.Generic;
using GameDeveloperKit.EditorNodeGraph;
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

        private readonly Action<string> m_Selected;
        private readonly Action<string> m_Activated;
        private readonly Action m_AddRootEpisode;
        private readonly Action<string, string> m_AddChildEpisode;
        private readonly Action<string> m_RemoveEpisode;
        private readonly Dictionary<string, Vector2> m_SessionPositions =
            new Dictionary<string, Vector2>(StringComparer.Ordinal);

        private AuthoringVolume m_Volume;
        private Volume m_CompiledVolume;
        private ValidationReport m_Report;
        private string m_SelectedNodeId;

        public RouteGraphAdapter(
            Action<string> selected,
            Action<string> activated,
            Action addRootEpisode = null,
            Action<string, string> addChildEpisode = null,
            Action<string> removeEpisode = null)
        {
            m_Selected = selected;
            m_Activated = activated;
            m_AddRootEpisode = addRootEpisode;
            m_AddChildEpisode = addChildEpisode;
            m_RemoveEpisode = removeEpisode;
        }

        public IReadOnlyList<EditorGraphNodeModel> Nodes => BuildNodes();

        public IReadOnlyList<EditorGraphWireModel> Wires => BuildWires();

        public IReadOnlyList<EditorGraphNodeTemplate> Templates => Array.Empty<EditorGraphNodeTemplate>();

        internal string VirtualRootNodeId => GetVirtualRootNodeId(m_Volume?.VolumeId);

        internal void SetRoute(
            AuthoringVolume volume,
            Volume compiledVolume,
            ValidationReport report,
            string selectedNodeId)
        {
            m_Volume = volume;
            m_CompiledVolume = compiledVolume != null &&
                               string.Equals(compiledVolume.VolumeId, volume?.VolumeId, StringComparison.Ordinal)
                ? compiledVolume
                : null;
            m_Report = report;
            m_SelectedNodeId = ContainsNode(selectedNodeId) ? selectedNodeId : VirtualRootNodeId;
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
            if (m_Volume?.Chapters == null || string.IsNullOrWhiteSpace(episodeId))
            {
                return false;
            }

            for (var i = 0; i < m_Volume.Chapters.Count; i++)
            {
                if (string.Equals(m_Volume.Chapters[i]?.ChapterId, episodeId, StringComparison.Ordinal))
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
                $"{SafeText(m_Volume?.Title, m_Volume?.VolumeId)} · {m_Volume?.Chapters.Count ?? 0} 个剧情段 · {m_CompiledVolume.Route.Edges.Count} 条路线"));
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
            if (ContainsNode(nodeId))
            {
                m_SessionPositions[nodeId] = graphPosition;
            }
        }

        public void MoveNodes(IReadOnlyList<EditorNodeGraphMove> moves)
        {
            if (moves == null)
            {
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
            m_Selected?.Invoke(nodeId);
        }

        public void ActivateNode(string nodeId)
        {
            if (ContainsEpisode(nodeId))
            {
                m_Activated?.Invoke(nodeId);
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
                menu.AddItem(new GUIContent("添加首层剧情段"), false, () => m_AddRootEpisode?.Invoke());
                return true;
            }

            if (m_CompiledVolume == null)
            {
                return false;
            }

            var populated = false;
            var episode = FindCompiledEpisode(nodeId);
            for (var i = 0; i < (episode?.Exits.Count ?? 0); i++)
            {
                var exit = episode.Exits[i];
                if (IsExitBound(nodeId, exit.ExitId))
                {
                    continue;
                }

                var exitId = exit.ExitId;
                var label = SafeText(exit.DisplayName, exitId).Replace('/', '／');
                menu.AddItem(
                    new GUIContent($"从出口添加剧情段/{label}"),
                    false,
                    () => m_AddChildEpisode?.Invoke(nodeId, exitId));
                populated = true;
            }

            if (HasChildren(nodeId) is false)
            {
                if (populated)
                {
                    menu.AddSeparator(string.Empty);
                }

                menu.AddItem(new GUIContent("删除剧情段"), false, () => m_RemoveEpisode?.Invoke(nodeId));
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

            var nodes = new List<EditorGraphNodeModel>(m_Volume.Chapters.Count + 1)
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

            var compiledEpisodes = BuildCompiledEpisodeLookup();
            for (var i = 0; i < m_Volume.Chapters.Count; i++)
            {
                var episode = m_Volume.Chapters[i];
                if (episode == null || string.IsNullOrWhiteSpace(episode.ChapterId))
                {
                    continue;
                }

                compiledEpisodes.TryGetValue(episode.ChapterId, out var compiledEpisode);
                nodes.Add(new EditorGraphNodeModel(
                    episode.ChapterId,
                    SafeText(episode.Title, episode.ChapterId),
                    "剧情段",
                    "路线",
                    GetPosition(episode.ChapterId),
                    new[]
                    {
                        new EditorGraphPortModel(
                            InputPortId,
                            "进入",
                            EditorGraphPortDirection.Input,
                            EditorGraphPortCapacity.Single,
                            s_EpisodePortColor)
                    },
                    BuildExitPorts(compiledEpisode),
                    Array.Empty<EditorGraphFieldModel>(),
                    selected: string.Equals(m_SelectedNodeId, episode.ChapterId, StringComparison.Ordinal),
                    styleKey: "route-episode"));
            }

            return nodes;
        }

        private IReadOnlyList<EditorGraphWireModel> BuildWires()
        {
            if (m_CompiledVolume == null)
            {
                return Array.Empty<EditorGraphWireModel>();
            }

            var wires = new List<EditorGraphWireModel>(m_CompiledVolume.Route.Edges.Count);
            for (var i = 0; i < m_CompiledVolume.Route.Edges.Count; i++)
            {
                var edge = m_CompiledVolume.Route.Edges[i];
                var output = edge.SourceKind == RouteEdgeSourceKind.Root
                    ? new EditorGraphPortRef(VirtualRootNodeId, RootPortId)
                    : new EditorGraphPortRef(edge.FromEpisodeId, edge.FromExitId);
                wires.Add(new EditorGraphWireModel(
                    edge.EdgeId,
                    output,
                    new EditorGraphPortRef(edge.ToEpisodeId, InputPortId),
                    edge.SourceKind == RouteEdgeSourceKind.Root ? "根" : edge.FromExitId));
            }

            return wires;
        }

        private IReadOnlyList<EditorGraphPortModel> BuildExitPorts(Episode episode)
        {
            if (episode == null || episode.Exits.Count == 0)
            {
                return Array.Empty<EditorGraphPortModel>();
            }

            var ports = new List<EditorGraphPortModel>(episode.Exits.Count);
            for (var i = 0; i < episode.Exits.Count; i++)
            {
                var exit = episode.Exits[i];
                ports.Add(new EditorGraphPortModel(
                    exit.ExitId,
                    SafeText(exit.DisplayName, exit.ExitId),
                    EditorGraphPortDirection.Output,
                    EditorGraphPortCapacity.Single,
                    s_EpisodePortColor));
            }

            return ports;
        }

        private Dictionary<string, Episode> BuildCompiledEpisodeLookup()
        {
            var lookup = new Dictionary<string, Episode>(StringComparer.Ordinal);
            if (m_CompiledVolume == null)
            {
                return lookup;
            }

            for (var i = 0; i < m_CompiledVolume.Episodes.Count; i++)
            {
                var episode = m_CompiledVolume.Episodes[i];
                if (episode != null && string.IsNullOrWhiteSpace(episode.EpisodeId) is false)
                {
                    lookup[episode.EpisodeId] = episode;
                }
            }

            return lookup;
        }

        private Episode FindCompiledEpisode(string episodeId)
        {
            for (var i = 0; i < (m_CompiledVolume?.Episodes.Count ?? 0); i++)
            {
                var episode = m_CompiledVolume.Episodes[i];
                if (episode != null && string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))
                {
                    return episode;
                }
            }

            return null;
        }

        private bool IsExitBound(string episodeId, string exitId)
        {
            for (var i = 0; i < (m_CompiledVolume?.Route.Edges.Count ?? 0); i++)
            {
                var edge = m_CompiledVolume.Route.Edges[i];
                if (edge.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
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
            for (var i = 0; i < (m_CompiledVolume?.Route.Edges.Count ?? 0); i++)
            {
                var edge = m_CompiledVolume.Route.Edges[i];
                if (edge.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                    string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureAutomaticPositions()
        {
            if (m_Volume == null)
            {
                return;
            }

            var depths = BuildDepths();
            var rowsByDepth = new Dictionary<int, int>();
            SetInitialPosition(VirtualRootNodeId, 0, rowsByDepth);
            for (var i = 0; i < m_Volume.Chapters.Count; i++)
            {
                var episodeId = m_Volume.Chapters[i]?.ChapterId;
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
            if (m_CompiledVolume == null)
            {
                return depths;
            }

            var unresolved = new List<RouteEdge>(m_CompiledVolume.Route.Edges);
            while (unresolved.Count > 0)
            {
                var changed = false;
                for (var i = unresolved.Count - 1; i >= 0; i--)
                {
                    var edge = unresolved[i];
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
            return m_SessionPositions.TryGetValue(nodeId, out var position) ? position : new Vector2(80f, 80f);
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

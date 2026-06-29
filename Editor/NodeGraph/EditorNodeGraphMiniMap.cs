using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorNodeGraph
{
    public sealed class EditorNodeGraphMiniMap : VisualElement
    {
        private const float MapWidth = 160f;
        private const float MapHeight = 82f;
        private const float NodeWidth = 14f;
        private const float NodeHeight = 8f;

        private readonly VisualElement m_Nodes = new VisualElement();

        public EditorNodeGraphMiniMap()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("editor-node-graph-minimap");
            Add(new Label("导航") { tooltip = "当前画布的小地图。" });
            m_Nodes.AddToClassList("editor-node-graph-minimap__nodes");
            Add(m_Nodes);
        }

        public void Rebuild(IReadOnlyList<EditorGraphNodeModel> nodes, IReadOnlyList<string> selectedNodeIds)
        {
            m_Nodes.Clear();
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            var positions = new List<Vector2>();
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                {
                    positions.Add(nodes[i].Position);
                }
            }

            if (positions.Count == 0)
            {
                return;
            }

            var selected = new HashSet<string>(selectedNodeIds ?? Array.Empty<string>(), StringComparer.Ordinal);

            var min = positions[0];
            var max = positions[0];
            for (var i = 1; i < positions.Count; i++)
            {
                min = Vector2.Min(min, positions[i]);
                max = Vector2.Max(max, positions[i]);
            }

            var range = max - min;
            range.x = Mathf.Max(range.x, 1f);
            range.y = Mathf.Max(range.y, 1f);

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                var normalized = new Vector2(
                    Mathf.InverseLerp(min.x, max.x, node.Position.x),
                    Mathf.InverseLerp(min.y, max.y, node.Position.y));
                var marker = new VisualElement { tooltip = SafeText(node.Title, node.NodeId) };
                marker.AddToClassList("editor-node-graph-minimap__node");
                marker.EnableInClassList("editor-node-graph-minimap__node--selected", selected.Contains(node.NodeId));
                marker.EnableInClassList("editor-node-graph-minimap__node--entry", node.Entry);
                marker.style.position = UnityEngine.UIElements.Position.Absolute;
                marker.style.left = Mathf.Clamp(normalized.x * (MapWidth - NodeWidth), 0f, MapWidth - NodeWidth);
                marker.style.top = Mathf.Clamp(normalized.y * (MapHeight - NodeHeight), 0f, MapHeight - NodeHeight);
                marker.style.width = NodeWidth;
                marker.style.height = NodeHeight;
                m_Nodes.Add(marker);
            }
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}

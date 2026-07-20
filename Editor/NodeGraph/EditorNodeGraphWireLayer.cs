using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorNodeGraph
{
    public sealed class EditorNodeGraphWireLayer : VisualElement
    {
        private IReadOnlyList<EditorGraphWireModel> m_Wires = Array.Empty<EditorGraphWireModel>();
        private IReadOnlyDictionary<string, EditorNodeGraphNodeView> m_NodeViews = new Dictionary<string, EditorNodeGraphNodeView>(StringComparer.Ordinal);
        private Vector2 m_Pan;
        private float m_Zoom = 1f;
        private bool m_VerticalFlow;
        private EditorGraphPortRef m_PendingOutput;
        private Vector2 m_PendingEnd;
        private bool m_HasPendingWire;
        private bool m_PendingAllowed = true;

        public EditorNodeGraphWireLayer()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("editor-node-graph-wire-layer");
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetGraph(
            IReadOnlyList<EditorGraphWireModel> wires,
            IReadOnlyDictionary<string, EditorNodeGraphNodeView> nodeViews,
            Vector2 pan,
            float zoom)
        {
            m_Wires = wires ?? Array.Empty<EditorGraphWireModel>();
            m_NodeViews = nodeViews ?? new Dictionary<string, EditorNodeGraphNodeView>(StringComparer.Ordinal);
            m_Pan = pan;
            m_Zoom = zoom;
            MarkDirtyRepaint();
        }

        public void SetViewTransform(Vector2 pan, float zoom)
        {
            m_Pan = pan;
            m_Zoom = zoom;
            MarkDirtyRepaint();
        }

        public void SetVerticalFlow(bool verticalFlow)
        {
            m_VerticalFlow = verticalFlow;
            MarkDirtyRepaint();
        }

        public void SetPendingWire(EditorGraphPortRef output, Vector2 canvasEnd, bool allowed)
        {
            m_PendingOutput = output;
            m_PendingEnd = canvasEnd;
            m_PendingAllowed = allowed;
            m_HasPendingWire = true;
            MarkDirtyRepaint();
        }

        public void ClearPendingWire()
        {
            m_HasPendingWire = false;
            m_PendingOutput = default;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            DrawGrid(painter);
            DrawWires(painter);
            DrawPendingWire(painter);
        }

        private void DrawGrid(Painter2D painter)
        {
            var rect = contentRect;
            var minor = Mathf.Max(8f, 24f * m_Zoom);
            var major = minor * 5f;
            DrawGridLines(painter, rect, minor, new Color(0.32f, 0.39f, 0.48f, 0.18f), 1f);
            DrawGridLines(painter, rect, major, new Color(0.42f, 0.52f, 0.64f, 0.28f), 1.2f);
        }

        private void DrawGridLines(Painter2D painter, Rect rect, float spacing, Color color, float width)
        {
            if (spacing <= 0.1f)
            {
                return;
            }

            painter.strokeColor = color;
            painter.lineWidth = width;
            var startX = m_Pan.x % spacing;
            var startY = m_Pan.y % spacing;
            for (var x = startX; x < rect.width; x += spacing)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, rect.height));
                painter.Stroke();
            }

            for (var y = startY; y < rect.height; y += spacing)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(rect.width, y));
                painter.Stroke();
            }
        }

        private void DrawWires(Painter2D painter)
        {
            for (var i = 0; i < m_Wires.Count; i++)
            {
                var wire = m_Wires[i];
                if (wire == null ||
                    TryResolveAnchor(wire.Output, out var start) is false ||
                    TryResolveAnchor(wire.Input, out var end) is false)
                {
                    continue;
                }

                painter.strokeColor = ResolveWireColor(wire);
                painter.lineWidth = wire.Selected ? 3f : 2f;
                DrawWire(painter, wire, start, end);
            }
        }

        private void DrawPendingWire(Painter2D painter)
        {
            if (!m_HasPendingWire || TryResolveAnchor(m_PendingOutput, out var start) is false)
            {
                return;
            }

            painter.strokeColor = m_PendingAllowed
                ? new Color(0.73f, 0.86f, 1f, 0.78f)
                : new Color(1f, 0.42f, 0.42f, 0.85f);
            painter.lineWidth = 2f;
            DrawBezier(painter, ToCanvas(start), m_PendingEnd);
        }

        private void DrawBezier(Painter2D painter, Vector2 start, Vector2 end)
        {
            painter.BeginPath();
            painter.MoveTo(start);
            var delta = m_VerticalFlow ? Mathf.Abs(end.y - start.y) : Mathf.Abs(end.x - start.x);
            var offset = Mathf.Max(70f, delta * 0.45f);
            var direction = m_VerticalFlow ? new Vector2(0f, offset) : new Vector2(offset, 0f);
            painter.BezierCurveTo(
                start + direction,
                end - direction,
                end);
            painter.Stroke();
        }

        private void DrawWire(Painter2D painter, EditorGraphWireModel wire, Vector2 start, Vector2 end)
        {
            if (wire.ControlPoints.Count == 0)
            {
                DrawBezier(painter, ToCanvas(start), ToCanvas(end));
                return;
            }

            painter.BeginPath();
            painter.MoveTo(ToCanvas(start));
            for (var i = 0; i < wire.ControlPoints.Count; i++)
            {
                painter.LineTo(ToCanvas(wire.ControlPoints[i]));
            }

            painter.LineTo(ToCanvas(end));
            painter.Stroke();
        }

        private bool TryResolveAnchor(EditorGraphPortRef portRef, out Vector2 graphPosition)
        {
            if (portRef.IsValid &&
                m_NodeViews.TryGetValue(portRef.NodeId, out var nodeView) &&
                nodeView.TryGetPortAnchor(portRef, out graphPosition))
            {
                return true;
            }

            graphPosition = Vector2.zero;
            return false;
        }

        private Vector2 ToCanvas(Vector2 graphPosition)
        {
            return graphPosition * m_Zoom + m_Pan;
        }

        private static Color ResolveWireColor(EditorGraphWireModel wire)
        {
            var severity = HighestSeverity(wire.Diagnostics);
            if (severity == EditorGraphDiagnosticSeverity.Error)
            {
                return wire.Selected ? new Color(1f, 0.36f, 0.36f, 1f) : new Color(1f, 0.36f, 0.36f, 0.88f);
            }

            if (severity == EditorGraphDiagnosticSeverity.Warning)
            {
                return wire.Selected ? new Color(1f, 0.74f, 0.28f, 1f) : new Color(1f, 0.74f, 0.28f, 0.86f);
            }

            return wire.Selected
                ? new Color(0.39f, 0.67f, 1f, 1f)
                : new Color(0.52f, 0.67f, 0.83f, 0.82f);
        }

        private static EditorGraphDiagnosticSeverity HighestSeverity(IReadOnlyList<EditorGraphDiagnostic> diagnostics)
        {
            var severity = EditorGraphDiagnosticSeverity.Info;
            for (var i = 0; i < (diagnostics?.Count ?? 0); i++)
            {
                if (diagnostics[i].Severity > severity)
                {
                    severity = diagnostics[i].Severity;
                }
            }

            return severity;
        }
    }
}

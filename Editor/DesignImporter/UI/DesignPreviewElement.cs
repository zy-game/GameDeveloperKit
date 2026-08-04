using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.DesignImporter
{
    internal sealed class DesignPreviewElement : IMGUIContainer
    {
        private DesignPage m_Page;
        private Texture2D m_Texture;
        private IReadOnlyList<DesignNodeRow> m_Nodes = Array.Empty<DesignNodeRow>();
        private DesignNode m_SelectedNode;
        private float m_Zoom = 1f;
        private Vector2 m_Pan;
        private Vector2 m_PanPointerStart;
        private Vector2 m_PanStart;
        private bool m_IsPanning;
        private int m_PanControlId;

        public DesignPreviewElement()
        {
            onGUIHandler = DrawPreview;
            focusable = true;
        }

        public event Action<DesignNode> NodeSelected;

        public event Action<float> ZoomRequested;

        public void ResetPan()
        {
            ReleasePanControl();
            m_Pan = Vector2.zero;
            m_IsPanning = false;
            MarkDirtyRepaint();
        }

        public void SetPage(
            DesignPage page,
            Texture2D texture,
            IReadOnlyList<DesignNodeRow> nodes,
            DesignNode selectedNode,
            float zoom)
        {
            if (!ReferenceEquals(m_Page, page))
            {
                ReleasePanControl();
                m_Pan = Vector2.zero;
                m_IsPanning = false;
            }

            m_Page = page;
            m_Texture = texture;
            m_Nodes = nodes ?? Array.Empty<DesignNodeRow>();
            m_SelectedNode = selectedNode;
            m_Zoom = Mathf.Clamp(zoom, 0.25f, 2f);
            MarkDirtyRepaint();
        }

        private void DrawPreview()
        {
            var bounds = contentRect;
            EditorGUI.DrawRect(bounds, EditorGUIUtility.isProSkin
                ? new Color(0.105f, 0.115f, 0.125f)
                : new Color(0.82f, 0.84f, 0.86f));
            if (m_Page == null)
            {
                DrawCenteredLabel(bounds, "未选择页面", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            HandleNavigation(bounds);
            var pageRect = GetPageRect(bounds);
            EditorGUI.DrawRect(new Rect(pageRect.x - 1, pageRect.y - 1, pageRect.width + 2, pageRect.height + 2),
                new Color(0f, 0f, 0f, 0.35f));
            if (m_Texture != null)
            {
                GUI.DrawTexture(pageRect, m_Texture, ScaleMode.StretchToFill, false);
            }
            else
            {
                EditorGUI.DrawRect(pageRect, Color.white);
                DrawStructure(pageRect);
            }

            DrawSelection(pageRect);
            HandleClick(pageRect);
        }

        private void DrawStructure(Rect pageRect)
        {
            for (var i = 0; i < m_Nodes.Count; i++)
            {
                var row = m_Nodes[i];
                var node = row.Node;
                if (node == null || !node.Visible || row.Depth == 0)
                {
                    continue;
                }

                var rect = ToPreviewRect(pageRect, row, m_Page);
                if (rect.width < 1f || rect.height < 1f)
                {
                    continue;
                }

                switch (node.Kind)
                {
                    case DesignNodeKind.Image:
                        EditorGUI.DrawRect(rect, new Color(0.20f, 0.55f, 0.52f, 0.45f));
                        break;
                    case DesignNodeKind.Text:
                        GUI.Label(rect, node.Text, EditorStyles.miniLabel);
                        break;
                    default:
                        if (ColorUtility.TryParseHtmlString(node.BackgroundColor, out var color))
                        {
                            EditorGUI.DrawRect(rect, color);
                        }
                        break;
                }
            }
        }

        private void DrawSelection(Rect pageRect)
        {
            if (m_SelectedNode == null)
            {
                return;
            }

            for (var i = 0; i < m_Nodes.Count; i++)
            {
                if (!ReferenceEquals(m_Nodes[i].Node, m_SelectedNode))
                {
                    continue;
                }

                var rect = ToPreviewRect(pageRect, m_Nodes[i], m_Page);
                Handles.BeginGUI();
                Handles.DrawSolidRectangleWithOutline(rect, Color.clear, new Color(0.10f, 0.52f, 0.85f));
                Handles.EndGUI();
                break;
            }
        }

        private void HandleClick(Rect pageRect)
        {
            var current = UnityEngine.Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || !pageRect.Contains(current.mousePosition))
            {
                return;
            }

            for (var i = m_Nodes.Count - 1; i >= 0; i--)
            {
                var row = m_Nodes[i];
                if (row.Node == null || !row.Node.Visible || row.Depth == 0)
                {
                    continue;
                }

                if (!ToPreviewRect(pageRect, row, m_Page).Contains(current.mousePosition))
                {
                    continue;
                }

                NodeSelected?.Invoke(row.Node);
                current.Use();
                return;
            }
        }

        private void HandleNavigation(Rect bounds)
        {
            var current = UnityEngine.Event.current;
            if (current == null)
            {
                return;
            }

            var controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (current.type == EventType.ScrollWheel && bounds.Contains(current.mousePosition))
            {
                var oldRect = GetPageRect(bounds);
                var zoomFactor = current.delta.y < 0f ? 1.12f : 1f / 1.12f;
                var zoom = Mathf.Clamp(m_Zoom * zoomFactor, 0.25f, 2f);
                if (!Mathf.Approximately(zoom, m_Zoom))
                {
                    var normalizedPoint = new Vector2(
                        Mathf.InverseLerp(oldRect.xMin, oldRect.xMax, current.mousePosition.x),
                        Mathf.InverseLerp(oldRect.yMin, oldRect.yMax, current.mousePosition.y));
                    m_Zoom = zoom;
                    var nextBaseRect = CalculatePageRect(bounds, m_Page, m_Zoom);
                    m_Pan = current.mousePosition - new Vector2(
                        nextBaseRect.x + normalizedPoint.x * nextBaseRect.width,
                        nextBaseRect.y + normalizedPoint.y * nextBaseRect.height);
                    m_Pan = ClampPan(bounds, m_Pan);
                    ZoomRequested?.Invoke(m_Zoom);
                    MarkDirtyRepaint();
                }

                current.Use();
                return;
            }

            var startsPan = current.type == EventType.MouseDown &&
                            (current.button == 2 || (current.button == 0 && current.alt));
            if (startsPan && bounds.Contains(current.mousePosition))
            {
                m_IsPanning = true;
                m_PanControlId = controlId;
                GUIUtility.hotControl = controlId;
                m_PanPointerStart = current.mousePosition;
                m_PanStart = m_Pan;
                current.Use();
                return;
            }

            if (m_IsPanning && GUIUtility.hotControl == controlId && current.type == EventType.MouseDrag)
            {
                m_Pan = ClampPan(bounds, m_PanStart + current.mousePosition - m_PanPointerStart);
                MarkDirtyRepaint();
                current.Use();
                return;
            }

            if (m_IsPanning && GUIUtility.hotControl == controlId && current.type == EventType.MouseUp)
            {
                ReleasePanControl();
                m_IsPanning = false;
                current.Use();
            }
        }

        private void ReleasePanControl()
        {
            if (m_PanControlId != 0 && GUIUtility.hotControl == m_PanControlId)
            {
                GUIUtility.hotControl = 0;
            }

            m_PanControlId = 0;
        }

        private Rect GetPageRect(Rect bounds)
        {
            var pageRect = CalculatePageRect(bounds, m_Page, m_Zoom);
            m_Pan = ClampPan(bounds, m_Pan);
            pageRect.position += m_Pan;
            return pageRect;
        }

        private Vector2 ClampPan(Rect bounds, Vector2 pan)
        {
            if (m_Page == null)
            {
                return Vector2.zero;
            }

            const float visibleMargin = 16f;
            var baseRect = CalculatePageRect(bounds, m_Page, m_Zoom);
            pan.x = ClampPanAxis(
                pan.x,
                bounds.xMax - baseRect.xMax + visibleMargin,
                bounds.xMin - baseRect.xMin - visibleMargin);
            pan.y = ClampPanAxis(
                pan.y,
                bounds.yMax - baseRect.yMax + visibleMargin,
                bounds.yMin - baseRect.yMin - visibleMargin);
            return pan;
        }

        private static float ClampPanAxis(float value, float minimum, float maximum)
        {
            return minimum > maximum ? 0f : Mathf.Clamp(value, minimum, maximum);
        }

        private static Rect CalculatePageRect(Rect bounds, DesignPage page, float zoom)
        {
            const float padding = 24f;
            var availableWidth = Mathf.Max(1f, bounds.width - padding * 2f);
            var availableHeight = Mathf.Max(1f, bounds.height - padding * 2f);
            var scale = Mathf.Min(availableWidth / page.Width, availableHeight / page.Height) * zoom;
            var width = page.Width * scale;
            var height = page.Height * scale;
            return new Rect(
                bounds.x + (bounds.width - width) * 0.5f,
                bounds.y + (bounds.height - height) * 0.5f,
                width,
                height);
        }

        private static Rect ToPreviewRect(Rect pageRect, DesignNodeRow row, DesignPage page)
        {
            var scaleX = pageRect.width / Mathf.Max(1f, page.Width);
            var scaleY = pageRect.height / Mathf.Max(1f, page.Height);
            return new Rect(
                pageRect.x + row.AbsoluteX * scaleX,
                pageRect.y + row.AbsoluteY * scaleY,
                row.Node.Width * scaleX,
                row.Node.Height * scaleY);
        }

        private static void DrawCenteredLabel(Rect bounds, string text, GUIStyle style)
        {
            GUI.Label(bounds, text, style);
        }
    }

    internal sealed class DesignPageThumbnailElement : IMGUIContainer
    {
        private DesignPage m_Page;
        private Texture2D m_Texture;

        public DesignPageThumbnailElement()
        {
            onGUIHandler = DrawThumbnail;
            pickingMode = PickingMode.Ignore;
        }

        public void SetPage(DesignPage page, Texture2D texture)
        {
            m_Page = page;
            m_Texture = texture;
            MarkDirtyRepaint();
        }

        private void DrawThumbnail()
        {
            var bounds = contentRect;
            EditorGUI.DrawRect(bounds, EditorGUIUtility.isProSkin
                ? new Color(0.09f, 0.1f, 0.11f)
                : new Color(0.84f, 0.86f, 0.89f));
            if (m_Page == null || m_Page.Width <= 0f || m_Page.Height <= 0f)
            {
                return;
            }

            var scale = Mathf.Min(bounds.width / m_Page.Width, bounds.height / m_Page.Height);
            var pageRect = new Rect(
                bounds.x + (bounds.width - m_Page.Width * scale) * 0.5f,
                bounds.y + (bounds.height - m_Page.Height * scale) * 0.5f,
                m_Page.Width * scale,
                m_Page.Height * scale);
            EditorGUI.DrawRect(pageRect, Color.white);
            if (m_Texture != null)
            {
                GUI.DrawTexture(pageRect, m_Texture, ScaleMode.StretchToFill, false);
                return;
            }

            DrawChildren(m_Page.Root, pageRect, 0f, 0f);
        }

        private void DrawChildren(DesignNode parent, Rect pageRect, float parentX, float parentY)
        {
            if (parent == null)
            {
                return;
            }

            foreach (var node in parent.Children)
            {
                if (node == null || !node.Visible)
                {
                    continue;
                }

                var absoluteX = parentX + node.X;
                var absoluteY = parentY + node.Y;
                var rect = new Rect(
                    pageRect.x + absoluteX / m_Page.Width * pageRect.width,
                    pageRect.y + absoluteY / m_Page.Height * pageRect.height,
                    node.Width / m_Page.Width * pageRect.width,
                    node.Height / m_Page.Height * pageRect.height);
                if (rect.width >= 1f && rect.height >= 1f)
                {
                    EditorGUI.DrawRect(rect, ThumbnailColor(node));
                }

                DrawChildren(node, pageRect, absoluteX, absoluteY);
            }
        }

        private static Color ThumbnailColor(DesignNode node)
        {
            if (ColorUtility.TryParseHtmlString(node.BackgroundColor, out var background) && background.a > 0.02f)
            {
                background.a = Mathf.Max(0.35f, background.a);
                return background;
            }

            return node.Kind switch
            {
                DesignNodeKind.Image => new Color(0.16f, 0.50f, 0.47f, 0.72f),
                DesignNodeKind.Text => new Color(0.20f, 0.28f, 0.36f, 0.76f),
                _ => new Color(0.55f, 0.60f, 0.66f, 0.25f)
            };
        }
    }

    internal sealed class DesignNodeRow
    {
        public DesignNodeRow(DesignNode node, int depth, float absoluteX, float absoluteY)
        {
            Node = node;
            Depth = depth;
            AbsoluteX = absoluteX;
            AbsoluteY = absoluteY;
        }

        public DesignNode Node { get; }
        public int Depth { get; }
        public float AbsoluteX { get; }
        public float AbsoluteY { get; }
    }
}

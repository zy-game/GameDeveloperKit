using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorNodeGraph
{
    public sealed class EditorNodeGraphPaletteView : VisualElement
    {
        private readonly VisualElement m_Content = new VisualElement();
        private EditorGraphNodeTemplate m_DragTemplate;
        private Vector2 m_DragStart;
        private bool m_Dragging;

        public event Action<EditorGraphNodeTemplate, Vector2> TemplateDragStarted;

        public event Action<EditorGraphNodeTemplate, Vector2> TemplateDragMoved;

        public event Action<EditorGraphNodeTemplate, Vector2> TemplateDragReleased;

        public event Action TemplateDragCancelled;

        public EditorNodeGraphPaletteView()
        {
            AddToClassList("editor-node-graph-palette");
            var header = new Label("节点库") { tooltip = "把节点拖到画布中创建；单击不会创建节点。" };
            header.AddToClassList("editor-node-graph-palette__header");
            Add(header);

            var scroll = new ScrollView();
            scroll.AddToClassList("editor-node-graph-palette__scroll");
            scroll.Add(m_Content);
            Add(scroll);
        }

        public void Rebuild(IReadOnlyList<EditorGraphNodeTemplate> templates)
        {
            m_Content.Clear();

            foreach (var group in (templates ?? Array.Empty<EditorGraphNodeTemplate>())
                         .Where(x => x != null)
                         .GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "默认" : x.Category)
                         .OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var foldout = new Foldout
                {
                    text = group.Key,
                    value = true,
                    tooltip = $"{group.Key}节点。"
                };
                foldout.AddToClassList("editor-node-graph-palette__group");

                foreach (var template in group.OrderBy(x => x.DisplayName, StringComparer.Ordinal))
                {
                    foldout.Add(CreateItem(template));
                }

                m_Content.Add(foldout);
            }
        }

        private VisualElement CreateItem(EditorGraphNodeTemplate template)
        {
            var item = new VisualElement
            {
                userData = template,
                tooltip = string.IsNullOrWhiteSpace(template.Tooltip) ? "拖到画布中创建节点。" : template.Tooltip
            };
            item.AddToClassList("editor-node-graph-palette__item");
            item.AddToClassList($"editor-node-graph-palette__item--{CssName(template.StyleKey, template.Category)}");
            item.Add(new Label(template.DisplayName));

            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                m_DragTemplate = template;
                m_DragStart = ToPanelPosition(item, evt.localMousePosition);
                m_Dragging = false;
                item.CaptureMouse();
                evt.StopPropagation();
            });
            item.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (m_DragTemplate == null)
                {
                    return;
                }

                var panelPosition = ToPanelPosition(item, evt.localMousePosition);
                if (!m_Dragging && Vector2.Distance(m_DragStart, panelPosition) > 6f)
                {
                    m_Dragging = true;
                    item.AddToClassList("editor-node-graph-palette__item--dragging");
                    TemplateDragStarted?.Invoke(m_DragTemplate, panelPosition);
                }

                if (m_Dragging)
                {
                    TemplateDragMoved?.Invoke(m_DragTemplate, panelPosition);
                }

                evt.StopPropagation();
            });
            item.RegisterCallback<MouseUpEvent>(evt =>
            {
                var templateToRelease = m_DragTemplate;
                var wasDragging = m_Dragging;
                ResetDrag(item);
                if (templateToRelease != null && item.HasMouseCapture())
                {
                    item.ReleaseMouse();
                }

                if (wasDragging)
                {
                    TemplateDragReleased?.Invoke(templateToRelease, ToPanelPosition(item, evt.localMousePosition));
                }

                evt.StopPropagation();
            });
            item.RegisterCallback<MouseCaptureOutEvent>(_ =>
            {
                if (m_DragTemplate != template)
                {
                    return;
                }

                var wasDragging = m_Dragging;
                ResetDrag(item);
                if (wasDragging)
                {
                    TemplateDragCancelled?.Invoke();
                }
            });

            return item;
        }

        private void ResetDrag(VisualElement item)
        {
            item.RemoveFromClassList("editor-node-graph-palette__item--dragging");
            m_DragTemplate = null;
            m_Dragging = false;
        }

        private static Vector2 ToPanelPosition(VisualElement element, Vector2 localMousePosition)
        {
            return element.LocalToWorld(localMousePosition);
        }

        private static string CssName(string styleKey, string category)
        {
            var value = string.IsNullOrWhiteSpace(styleKey) ? category : styleKey;
            if (string.IsNullOrWhiteSpace(value))
            {
                return "default";
            }

            var text = value.Trim().ToLowerInvariant();
            var chars = new char[text.Length];
            var count = 0;
            var pendingDash = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                var allowed = ch >= 'a' && ch <= 'z' ||
                              ch >= '0' && ch <= '9' ||
                              ch == '_' ||
                              ch == '-';
                if (allowed)
                {
                    if (pendingDash && count > 0 && chars[count - 1] != '-')
                    {
                        chars[count++] = '-';
                    }

                    chars[count++] = ch;
                    pendingDash = false;
                }
                else
                {
                    pendingDash = true;
                }
            }

            var cssName = count == 0 ? string.Empty : new string(chars, 0, count).Trim('-');
            return string.IsNullOrWhiteSpace(cssName) ? "default" : cssName;
        }
    }
}

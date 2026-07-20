using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorNodeGraph
{
    public sealed class EditorNodeGraphCanvas : VisualElement
    {
        private readonly EditorNodeGraphPaletteView m_Palette = new EditorNodeGraphPaletteView();
        private readonly VisualElement m_GraphArea = new VisualElement();
        private readonly EditorNodeGraphWireLayer m_WireLayer = new EditorNodeGraphWireLayer();
        private readonly VisualElement m_Content = new VisualElement();
        private readonly VisualElement m_ReferenceStrip = new VisualElement();
        private readonly VisualElement m_ReferenceCanvas = new VisualElement();
        private readonly Image m_BackgroundImage = new Image();
        private readonly Image m_GuideImage = new Image();
        private readonly VisualElement m_BlackboardHost = new VisualElement();
        private readonly Label m_Status = new Label();
        private readonly EditorNodeGraphMiniMap m_MiniMap = new EditorNodeGraphMiniMap();
        private readonly Label m_PaletteDragPreview = new Label();
        private readonly VisualElement m_SelectionBox = new VisualElement();
        private readonly Dictionary<string, EditorNodeGraphNodeView> m_NodeViews = new Dictionary<string, EditorNodeGraphNodeView>(StringComparer.Ordinal);

        private IEditorNodeGraphAdapter m_Adapter;
        private EditorGraphPortRef m_PendingOutput;
        private Vector2 m_Pan = new Vector2(80f, 80f);
        private float m_Zoom = 1f;
        private Vector2 m_ReferenceStripGraphOffset;
        private bool m_VerticalFlow;
        private bool m_Panning;
        private bool m_BoxSelecting;
        private bool m_BoxSelectionStarted;
        private Vector2 m_BoxSelectionStart;
        private Vector2 m_BoxSelectionCurrent;
        private bool m_PaletteDragging;
        private EditorGraphNodeTemplate m_PaletteDragTemplate;
        private Vector2 m_LastMousePosition;

        public EditorNodeGraphCanvas()
        {
            AddToClassList("editor-node-graph");
            focusable = true;

            Add(m_Palette);
            m_Palette.TemplateDragStarted += OnPaletteTemplateDragStarted;
            m_Palette.TemplateDragMoved += OnPaletteTemplateDragMoved;
            m_Palette.TemplateDragReleased += OnPaletteTemplateDragReleased;
            m_Palette.TemplateDragCancelled += OnPaletteTemplateDragCancelled;

            m_GraphArea.AddToClassList("editor-node-graph__graph-area");
            m_GraphArea.focusable = true;
            Add(m_GraphArea);

            m_ReferenceStrip.AddToClassList("editor-node-graph-reference-strip");
            m_ReferenceStrip.pickingMode = PickingMode.Ignore;
            m_ReferenceStrip.style.position = UnityEngine.UIElements.Position.Absolute;
            m_ReferenceStrip.style.transformOrigin = new TransformOrigin(0f, 0f);
            m_GraphArea.Add(m_ReferenceStrip);

            m_ReferenceCanvas.AddToClassList("editor-node-graph-reference-canvas");
            m_ReferenceCanvas.pickingMode = PickingMode.Position;
            m_ReferenceCanvas.style.position = UnityEngine.UIElements.Position.Absolute;
            m_ReferenceCanvas.style.transformOrigin = new TransformOrigin(0f, 0f);
            m_BackgroundImage.StretchToParentSize();
            m_BackgroundImage.scaleMode = ScaleMode.StretchToFill;
            m_BackgroundImage.pickingMode = PickingMode.Ignore;
            m_ReferenceCanvas.Add(m_BackgroundImage);
            m_GuideImage.StretchToParentSize();
            m_GuideImage.scaleMode = ScaleMode.StretchToFill;
            m_GuideImage.pickingMode = PickingMode.Ignore;
            m_GuideImage.AddToClassList("editor-node-graph-reference-canvas__guide");
            m_ReferenceCanvas.Add(m_GuideImage);
            m_GraphArea.Add(m_ReferenceCanvas);

            m_WireLayer.StretchToParentSize();
            m_GraphArea.Add(m_WireLayer);

            m_Content.AddToClassList("editor-node-graph__content");
            m_Content.StretchToParentSize();
            m_Content.style.transformOrigin = new TransformOrigin(0f, 0f);
            m_GraphArea.Add(m_Content);

            m_BlackboardHost.AddToClassList("editor-node-graph__blackboard");
            m_GraphArea.Add(m_BlackboardHost);

            m_Status.AddToClassList("editor-node-graph__status");
            m_GraphArea.Add(m_Status);

            m_GraphArea.Add(m_MiniMap);

            m_SelectionBox.AddToClassList("editor-node-graph__selection-box");
            m_SelectionBox.pickingMode = PickingMode.Ignore;
            m_SelectionBox.style.position = UnityEngine.UIElements.Position.Absolute;
            m_SelectionBox.style.display = DisplayStyle.None;
            m_GraphArea.Add(m_SelectionBox);

            m_PaletteDragPreview.AddToClassList("editor-node-graph__palette-drag-preview");
            m_PaletteDragPreview.pickingMode = PickingMode.Ignore;
            m_PaletteDragPreview.style.position = UnityEngine.UIElements.Position.Absolute;
            m_PaletteDragPreview.style.display = DisplayStyle.None;
            Add(m_PaletteDragPreview);

            m_GraphArea.RegisterCallback<MouseDownEvent>(OnMouseDown);
            m_GraphArea.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            m_GraphArea.RegisterCallback<MouseUpEvent>(OnMouseUp);
            m_GraphArea.RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            m_GraphArea.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public void SetAdapter(IEditorNodeGraphAdapter adapter)
        {
            m_Adapter = adapter;
            Rebuild();
        }

        public void Rebuild()
        {
            m_NodeViews.Clear();
            m_Content.Clear();
            m_Status.text = string.Empty;
            m_VerticalFlow = UsesVerticalFlow(m_Adapter?.Canvas);
            m_WireLayer.SetVerticalFlow(m_VerticalFlow);
            RebuildReferenceCanvas();

            if (m_Adapter == null)
            {
                m_Palette.Rebuild(Array.Empty<EditorGraphNodeTemplate>());
                m_WireLayer.SetGraph(Array.Empty<EditorGraphWireModel>(), m_NodeViews, m_Pan, m_Zoom);
                m_MiniMap.Rebuild(Array.Empty<EditorGraphNodeModel>(), Array.Empty<string>());
                return;
            }

            m_Palette.Rebuild(m_Adapter.Templates);
            RebuildBlackboard();

            var nodes = m_Adapter.Nodes ?? Array.Empty<EditorGraphNodeModel>();
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                var view = new EditorNodeGraphNodeView(
                    node,
                    () => m_Zoom,
                    OnNodeSelected,
                    OnNodeActivated,
                    FocusCanvas,
                    OnNodeMoved,
                    OnNodeMoveDelta,
                    OnOutputDragMoved,
                    OnOutputDragReleased,
                    OnNodeFieldChanged,
                    m_Adapter.CreateCustomField);
                view.SetVerticalFlow(m_VerticalFlow);
                m_NodeViews[node.NodeId] = view;
                m_Content.Add(view);
            }

            RebuildControlPoints();

            m_WireLayer.SetGraph(m_Adapter.Wires, m_NodeViews, m_Pan, m_Zoom);
            m_MiniMap.Rebuild(nodes, nodes.Where(x => x != null && x.Selected).Select(x => x.NodeId).ToList());
            ApplyTransform();
        }

        public Vector2 GetGraphCenterPosition()
        {
            var center = new Vector2(m_GraphArea.contentRect.width * 0.5f, m_GraphArea.contentRect.height * 0.5f);
            return CanvasToGraph(center);
        }

        public Vector2 CanvasToGraph(Vector2 position)
        {
            return (position - m_Pan) / m_Zoom;
        }

        public Vector2 GraphToCanvas(Vector2 position)
        {
            return position * m_Zoom + m_Pan;
        }

        public void FrameAll()
        {
            FrameNodes(false, true);
        }

        internal void ZoomAt(Vector2 canvasPosition, float zoomFactor)
        {
            var before = CanvasToGraph(canvasPosition);
            m_Zoom = Mathf.Clamp(m_Zoom * zoomFactor, 0.35f, 2.25f);
            var afterCanvas = GraphToCanvas(before);
            m_Pan += canvasPosition - afterCanvas;
            ApplyTransform();
        }

        internal EditorGraphConnectionResult ConnectPorts(EditorGraphPortRef output, EditorGraphPortRef input)
        {
            var result = m_Adapter?.CanConnect(output, input) ?? EditorGraphConnectionResult.Fail("没有图适配器。");
            if (result.Allowed)
            {
                m_Adapter?.Connect(output, input);
                Rebuild();
            }
            else
            {
                SetStatus(result.Message);
            }

            return result;
        }

        internal bool TryCreateTemplateFromPaletteDrop(EditorGraphNodeTemplate template, Vector2 panelPosition)
        {
            if (template == null || IsPanelPositionInsideGraphArea(panelPosition) is false)
            {
                return false;
            }

            CreateTemplateAt(template, CanvasToGraph(m_GraphArea.WorldToLocal(panelPosition)), m_PendingOutput);
            m_PendingOutput = default;
            return true;
        }

        internal void DeleteSelection()
        {
            m_Adapter?.DeleteSelection();
            Rebuild();
        }

        internal IReadOnlyList<string> FindNodesInGraphRect(Rect graphRect)
        {
            var nodes = m_Adapter?.Nodes ?? Array.Empty<EditorGraphNodeModel>();
            var selected = new List<string>();
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                if (graphRect.Overlaps(ResolveNodeGraphRect(node), true))
                {
                    selected.Add(node.NodeId);
                }
            }

            return selected;
        }

        internal IReadOnlyList<string> SelectNodesInGraphRect(Rect graphRect)
        {
            var nodeIds = FindNodesInGraphRect(graphRect);
            SelectNodes(nodeIds);
            return nodeIds;
        }

        internal void CancelPendingConnection()
        {
            CancelBoxSelection();
            m_PendingOutput = default;
            m_WireLayer.ClearPendingWire();
            ClearPaletteDragPreview();
        }

        private void RebuildBlackboard()
        {
            m_BlackboardHost.Clear();
            var blackboard = m_Adapter?.CreateBlackboard();
            if (blackboard == null)
            {
                m_BlackboardHost.style.display = DisplayStyle.None;
                return;
            }

            m_BlackboardHost.style.display = DisplayStyle.Flex;
            m_BlackboardHost.Add(blackboard);
        }

        private void OnNodeSelected(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                SelectNodes(Array.Empty<string>());
                return;
            }

            if (m_NodeViews.TryGetValue(nodeId, out var clickedView) &&
                clickedView.ClassListContains("editor-node-graph-node--selected"))
            {
                var selectedCount = 0;
                foreach (var pair in m_NodeViews)
                {
                    if (pair.Value.ClassListContains("editor-node-graph-node--selected"))
                    {
                        selectedCount++;
                    }
                }

                if (selectedCount > 1)
                {
                    return;
                }
            }

            m_Adapter?.SelectNode(nodeId);
            foreach (var pair in m_NodeViews)
            {
                pair.Value.SetSelected(string.Equals(pair.Key, nodeId, StringComparison.Ordinal));
            }

            m_MiniMap.Rebuild(m_Adapter?.Nodes, new[] { nodeId });
        }

        private void OnNodeActivated(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            m_Adapter?.ActivateNode(nodeId);
        }

        private void FocusCanvas()
        {
            Focus();
            m_GraphArea.Focus();
        }

        private void OnNodeMoved(string nodeId, Vector2 position)
        {
            position = ClampToReferenceCanvas(position);
            if (m_NodeViews.TryGetValue(nodeId, out var movedView))
            {
                movedView.Position = position;
                movedView.ApplyPosition();
            }

            var selectedNodeIds = new List<string>();
            foreach (var pair in m_NodeViews)
            {
                if (pair.Value.ClassListContains("editor-node-graph-node--selected"))
                {
                    selectedNodeIds.Add(pair.Key);
                }
            }

            if (selectedNodeIds.Count > 1)
            {
                var moves = new List<EditorNodeGraphMove>(selectedNodeIds.Count);
                for (var i = 0; i < selectedNodeIds.Count; i++)
                {
                    var id = selectedNodeIds[i];
                    if (string.Equals(id, nodeId, StringComparison.Ordinal))
                    {
                        moves.Add(new EditorNodeGraphMove(nodeId, position));
                    }
                    else if (m_NodeViews.TryGetValue(id, out var view))
                    {
                        moves.Add(new EditorNodeGraphMove(id, view.Position));
                    }
                }

                m_Adapter?.MoveNodes(moves);
            }
            else
            {
                m_Adapter?.MoveNode(nodeId, position);
            }

            m_WireLayer.MarkDirtyRepaint();
            var nodes = m_Adapter?.Nodes ?? Array.Empty<EditorGraphNodeModel>();
            m_MiniMap.Rebuild(nodes, selectedNodeIds);
        }

        private void OnNodeMoveDelta(string nodeId, Vector2 delta)
        {
            m_WireLayer.MarkDirtyRepaint();
            var otherSelectedIds = new List<string>();
            foreach (var pair in m_NodeViews)
            {
                if (!string.Equals(pair.Key, nodeId, StringComparison.Ordinal) &&
                    pair.Value.ClassListContains("editor-node-graph-node--selected"))
                {
                    otherSelectedIds.Add(pair.Key);
                }
            }

            if (otherSelectedIds.Count == 0)
            {
                return;
            }

            for (var i = 0; i < otherSelectedIds.Count; i++)
            {
                if (m_NodeViews.TryGetValue(otherSelectedIds[i], out var view))
                {
                    view.Position = ClampToReferenceCanvas(view.Position + delta);
                    view.ApplyPosition();
                }
            }

        }

        private void OnNodeFieldChanged(string nodeId, string fieldId, string value)
        {
            m_Adapter?.SetNodeField(nodeId, fieldId, value);
            Rebuild();
        }

        private void OnOutputDragMoved(EditorGraphPortRef output, Vector2 panelPosition)
        {
            m_PendingOutput = output;
            var graphPosition = CanvasToGraph(m_GraphArea.WorldToLocal(panelPosition));
            var allowed = TryFindInputPort(graphPosition, out var input) &&
                          (m_Adapter?.CanConnect(output, input)?.Allowed ?? false);
            m_WireLayer.SetPendingWire(output, m_GraphArea.WorldToLocal(panelPosition), allowed);
        }

        private void OnOutputDragReleased(EditorGraphPortRef output, Vector2 panelPosition)
        {
            var canvasPosition = m_GraphArea.WorldToLocal(panelPosition);
            var graphPosition = CanvasToGraph(canvasPosition);
            m_WireLayer.ClearPendingWire();

            if (TryFindInputPort(graphPosition, out var input))
            {
                if (ConnectPorts(output, input).Allowed)
                {
                    m_PendingOutput = default;
                    return;
                }

                m_PendingOutput = default;
                return;
            }

            m_PendingOutput = output;
            ShowCreateAndConnectMenu(graphPosition);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            FocusCanvas();

            if (evt.button == 1)
            {
                var canvasPosition = m_GraphArea.WorldToLocal(evt.mousePosition);
                if (TryFindControlPoint(evt.target as VisualElement, out var pointRef))
                {
                    var pointMenu = new GenericMenu();
                    pointMenu.AddItem(
                        new GUIContent("删除控制点"),
                        false,
                        () =>
                        {
                            m_Adapter?.RemoveWireControlPoint(pointRef.WireId, pointRef.PointIndex);
                            Rebuild();
                        });
                    pointMenu.ShowAsContext();
                    evt.StopPropagation();
                    return;
                }

                if (TryFindNodeId(evt.target as VisualElement, out var nodeId))
                {
                    var nodeMenu = new GenericMenu();
                    if (m_Adapter?.PopulateNodeContextMenu(nodeId, nodeMenu) == true)
                    {
                        nodeMenu.ShowAsContext();
                        evt.StopPropagation();
                        return;
                    }
                }

                if (TryFindWire(canvasPosition, out var pathWireId))
                {
                    var wire = FindWire(pathWireId);
                    if (wire?.ControlPointsEditable == true)
                    {
                        var graphPosition = CanvasToGraph(canvasPosition);
                        var segmentIndex = FindClosestSegmentIndex(wire, graphPosition);
                        var pathMenu = new GenericMenu();
                        pathMenu.AddItem(
                            new GUIContent("添加控制点"),
                            false,
                            () =>
                            {
                                m_Adapter?.InsertWireControlPoint(pathWireId, segmentIndex, graphPosition);
                                Rebuild();
                            });
                        pathMenu.ShowAsContext();
                        evt.StopPropagation();
                        return;
                    }
                }

                ShowCreateMenu(CanvasToGraph(canvasPosition), m_PendingOutput);
                evt.StopPropagation();
                return;
            }

            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                m_Panning = true;
                m_LastMousePosition = evt.mousePosition;
                m_GraphArea.CaptureMouse();
                evt.StopPropagation();
                return;
            }

            if (evt.button == 0 && TryFindWire(m_GraphArea.WorldToLocal(evt.mousePosition), out var wireId))
            {
                m_Adapter?.SelectWire(wireId);
                Rebuild();
                evt.StopPropagation();
                return;
            }

            if (evt.button == 0 && IsGraphBackgroundTarget(evt.target as VisualElement))
            {
                BeginBoxSelection(m_GraphArea.WorldToLocal(evt.mousePosition));
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (m_BoxSelecting)
            {
                UpdateBoxSelection(m_GraphArea.WorldToLocal(evt.mousePosition));
                evt.StopPropagation();
                return;
            }

            if (!m_Panning)
            {
                return;
            }

            var delta = evt.mousePosition - m_LastMousePosition;
            m_LastMousePosition = evt.mousePosition;
            m_Pan += delta;
            ApplyTransform();
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (m_BoxSelecting)
            {
                EndBoxSelection(m_GraphArea.WorldToLocal(evt.mousePosition));
                evt.StopPropagation();
                return;
            }

            if (!m_Panning)
            {
                return;
            }

            m_Panning = false;
            m_GraphArea.ReleaseMouse();
            evt.StopPropagation();
        }

        private void OnWheel(WheelEvent evt)
        {
            ZoomAt(m_GraphArea.WorldToLocal(evt.mousePosition), evt.delta.y > 0f ? 0.92f : 1.08f);
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (IsEditingText(evt.target as VisualElement))
            {
                return;
            }

            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                DeleteSelection();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                CancelPendingConnection();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Space)
            {
                ShowCreateMenu(GetGraphCenterPosition(), default);
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.F)
            {
                FrameNodes(true, false);
                evt.StopPropagation();
            }
        }

        private void OnPaletteTemplateDragStarted(EditorGraphNodeTemplate template, Vector2 panelPosition)
        {
            m_PaletteDragging = true;
            m_PaletteDragTemplate = template;
            m_PaletteDragPreview.text = template?.DisplayName ?? string.Empty;
            m_PaletteDragPreview.style.display = DisplayStyle.Flex;
            UpdatePaletteDragPreview(panelPosition);
        }

        private void OnPaletteTemplateDragMoved(EditorGraphNodeTemplate template, Vector2 panelPosition)
        {
            if (!m_PaletteDragging || !ReferenceEquals(template, m_PaletteDragTemplate))
            {
                return;
            }

            UpdatePaletteDragPreview(panelPosition);
        }

        private void OnPaletteTemplateDragReleased(EditorGraphNodeTemplate template, Vector2 panelPosition)
        {
            if (!m_PaletteDragging || !ReferenceEquals(template, m_PaletteDragTemplate))
            {
                ClearPaletteDragPreview();
                return;
            }

            TryCreateTemplateFromPaletteDrop(template, panelPosition);
            ClearPaletteDragPreview();
        }

        private void OnPaletteTemplateDragCancelled()
        {
            ClearPaletteDragPreview();
        }

        private void UpdatePaletteDragPreview(Vector2 panelPosition)
        {
            var local = this.WorldToLocal(panelPosition);
            m_PaletteDragPreview.style.left = local.x + 12f;
            m_PaletteDragPreview.style.top = local.y + 12f;
            m_PaletteDragPreview.EnableInClassList(
                "editor-node-graph__palette-drag-preview--invalid",
                IsPanelPositionInsideGraphArea(panelPosition) is false);
        }

        private void ClearPaletteDragPreview()
        {
            m_PaletteDragging = false;
            m_PaletteDragTemplate = null;
            m_PaletteDragPreview.style.display = DisplayStyle.None;
            m_PaletteDragPreview.RemoveFromClassList("editor-node-graph__palette-drag-preview--invalid");
        }

        private bool IsPanelPositionInsideGraphArea(Vector2 panelPosition)
        {
            var local = m_GraphArea.WorldToLocal(panelPosition);
            return m_GraphArea.contentRect.Contains(local);
        }

        private void BeginBoxSelection(Vector2 canvasPosition)
        {
            m_BoxSelecting = true;
            m_BoxSelectionStarted = false;
            m_BoxSelectionStart = canvasPosition;
            m_BoxSelectionCurrent = canvasPosition;
            m_GraphArea.CaptureMouse();
        }

        private void UpdateBoxSelection(Vector2 canvasPosition)
        {
            m_BoxSelectionCurrent = canvasPosition;
            if (!m_BoxSelectionStarted && Vector2.Distance(m_BoxSelectionStart, m_BoxSelectionCurrent) > 4f)
            {
                m_BoxSelectionStarted = true;
                m_SelectionBox.style.display = DisplayStyle.Flex;
            }

            if (m_BoxSelectionStarted)
            {
                UpdateSelectionBox();
            }
        }

        private void EndBoxSelection(Vector2 canvasPosition)
        {
            m_BoxSelectionCurrent = canvasPosition;
            if (m_BoxSelectionStarted)
            {
                SelectNodesInGraphRect(CanvasRectToGraphRect(GetBoxSelectionCanvasRect()));
            }
            else
            {
                SelectNodes(Array.Empty<string>());
            }

            CancelBoxSelection();
        }

        private void CancelBoxSelection()
        {
            if (m_BoxSelecting && m_GraphArea.HasMouseCapture())
            {
                m_GraphArea.ReleaseMouse();
            }

            m_BoxSelecting = false;
            m_BoxSelectionStarted = false;
            m_SelectionBox.style.display = DisplayStyle.None;
        }

        private void UpdateSelectionBox()
        {
            var rect = GetBoxSelectionCanvasRect();
            m_SelectionBox.style.left = rect.xMin;
            m_SelectionBox.style.top = rect.yMin;
            m_SelectionBox.style.width = rect.width;
            m_SelectionBox.style.height = rect.height;
        }

        private Rect GetBoxSelectionCanvasRect()
        {
            var min = Vector2.Min(m_BoxSelectionStart, m_BoxSelectionCurrent);
            var max = Vector2.Max(m_BoxSelectionStart, m_BoxSelectionCurrent);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Rect CanvasRectToGraphRect(Rect canvasRect)
        {
            var min = CanvasToGraph(new Vector2(canvasRect.xMin, canvasRect.yMin));
            var max = CanvasToGraph(new Vector2(canvasRect.xMax, canvasRect.yMax));
            return Rect.MinMaxRect(
                Mathf.Min(min.x, max.x),
                Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y));
        }

        private Rect ResolveNodeGraphRect(EditorGraphNodeModel node)
        {
            if (node != null &&
                m_NodeViews.TryGetValue(node.NodeId, out var view) &&
                view.worldBound.width > 0.1f &&
                view.worldBound.height > 0.1f)
            {
                var minCanvas = m_GraphArea.WorldToLocal(view.worldBound.min);
                var maxCanvas = m_GraphArea.WorldToLocal(view.worldBound.max);
                return CanvasRectToGraphRect(Rect.MinMaxRect(minCanvas.x, minCanvas.y, maxCanvas.x, maxCanvas.y));
            }

            return new Rect(
                node?.Position ?? Vector2.zero,
                new Vector2(EditorNodeGraphNodeView.DefaultWidth, 160f));
        }

        private void SelectNodes(IReadOnlyList<string> nodeIds)
        {
            nodeIds = nodeIds ?? Array.Empty<string>();
            var selected = new HashSet<string>(nodeIds, StringComparer.Ordinal);
            m_Adapter?.SelectNodes(nodeIds);
            foreach (var pair in m_NodeViews)
            {
                pair.Value.SetSelected(selected.Contains(pair.Key));
            }

            m_MiniMap.Rebuild(m_Adapter?.Nodes, nodeIds);
        }

        private void ShowCreateMenu(Vector2 graphPosition, EditorGraphPortRef connectFrom)
        {
            var menu = new GenericMenu();
            if (connectFrom.IsValid)
            {
                menu.AddItem(new GUIContent("取消连接"), false, () =>
                {
                    m_PendingOutput = default;
                    m_WireLayer.ClearPendingWire();
                });
                menu.AddSeparator(string.Empty);
                AppendCreateMenu(menu, graphPosition, connectFrom, "创建并连接");
            }
            else
            {
                AppendCreateMenu(menu, graphPosition, default, "创建");
            }

            menu.ShowAsContext();
        }

        private void AppendCreateMenu(GenericMenu menu, Vector2 graphPosition, EditorGraphPortRef connectFrom, string root)
        {
            var templates = m_Adapter?.Templates ?? Array.Empty<EditorGraphNodeTemplate>();
            foreach (var template in templates.Where(x => x != null).OrderBy(x => x.Category).ThenBy(x => x.DisplayName))
            {
                var item = template;
                menu.AddItem(
                    new GUIContent($"{root}/{SafeText(item.Category, "默认")}/{item.DisplayName}"),
                    false,
                    () =>
                    {
                        CreateTemplateAt(item, graphPosition, connectFrom);
                        if (connectFrom.IsValid)
                        {
                            m_PendingOutput = default;
                            m_WireLayer.ClearPendingWire();
                        }
                    });
            }
        }

        private void ShowCreateAndConnectMenu(Vector2 graphPosition)
        {
            ShowCreateMenu(graphPosition, m_PendingOutput);
        }

        private bool TryFindNodeId(VisualElement target, out string nodeId)
        {
            while (target != null && target != m_GraphArea)
            {
                if (target.ClassListContains("editor-node-graph-node") &&
                    target.userData is string value &&
                    string.IsNullOrWhiteSpace(value) is false)
                {
                    nodeId = value;
                    return true;
                }

                target = target.parent;
            }

            nodeId = null;
            return false;
        }

        internal void CreateTemplateAt(EditorGraphNodeTemplate template, Vector2 graphPosition, EditorGraphPortRef connectFrom)
        {
            if (m_Adapter == null || template == null)
            {
                return;
            }

            m_Adapter.CreateNode(template, graphPosition, connectFrom);
            Rebuild();
        }

        private bool TryFindInputPort(Vector2 graphPosition, out EditorGraphPortRef input)
        {
            foreach (var pair in m_NodeViews)
            {
                if (pair.Value.TryFindInputPort(graphPosition, out input))
                {
                    return true;
                }
            }

            input = default;
            return false;
        }

        private bool TryFindWire(Vector2 canvasPosition, out string wireId)
        {
            var wires = m_Adapter?.Wires ?? Array.Empty<EditorGraphWireModel>();
            for (var i = 0; i < wires.Count; i++)
            {
                var wire = wires[i];
                if (wire == null ||
                    TryResolvePortCanvasPosition(wire.Output, out var start) is false ||
                    TryResolvePortCanvasPosition(wire.Input, out var end) is false)
                {
                    continue;
                }

                if (DistanceToWire(canvasPosition, wire, start, end) <= 8f)
                {
                    wireId = wire.WireId;
                    return true;
                }
            }

            wireId = null;
            return false;
        }

        private bool TryResolvePortCanvasPosition(EditorGraphPortRef portRef, out Vector2 canvasPosition)
        {
            if (portRef.IsValid &&
                m_NodeViews.TryGetValue(portRef.NodeId, out var nodeView) &&
                nodeView.TryGetPortAnchor(portRef, out var graphPosition))
            {
                canvasPosition = GraphToCanvas(graphPosition);
                return true;
            }

            canvasPosition = Vector2.zero;
            return false;
        }

        private float DistanceToBezier(Vector2 point, Vector2 start, Vector2 end)
        {
            var delta = m_VerticalFlow ? Mathf.Abs(end.y - start.y) : Mathf.Abs(end.x - start.x);
            var offset = Mathf.Max(70f, delta * 0.45f);
            var direction = m_VerticalFlow ? new Vector2(0f, offset) : new Vector2(offset, 0f);
            var c1 = start + direction;
            var c2 = end - direction;
            var best = float.MaxValue;
            var previous = start;
            for (var i = 1; i <= 24; i++)
            {
                var t = i / 24f;
                var current = Cubic(start, c1, c2, end, t);
                best = Mathf.Min(best, DistanceToSegment(point, previous, current));
                previous = current;
            }

            return best;
        }

        private static bool UsesVerticalFlow(EditorGraphCanvasModel canvas)
        {
            return canvas?.ConstrainsXAxis == true && canvas.ConstrainsYAxis is false;
        }

        private float DistanceToWire(
            Vector2 point,
            EditorGraphWireModel wire,
            Vector2 start,
            Vector2 end)
        {
            if (wire.ControlPoints.Count == 0)
            {
                return DistanceToBezier(point, start, end);
            }

            var best = float.MaxValue;
            var previous = start;
            for (var i = 0; i < wire.ControlPoints.Count; i++)
            {
                var current = GraphToCanvas(wire.ControlPoints[i]);
                best = Mathf.Min(best, DistanceToSegment(point, previous, current));
                previous = current;
            }

            return Mathf.Min(best, DistanceToSegment(point, previous, end));
        }

        private void RebuildReferenceCanvas()
        {
            var canvas = m_Adapter?.Canvas;
            var visible = canvas?.HasReferenceSize == true;
            m_ReferenceCanvas.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                m_ReferenceStrip.style.display = DisplayStyle.None;
                m_ReferenceStripGraphOffset = Vector2.zero;
                m_BackgroundImage.image = null;
                m_GuideImage.image = null;
                return;
            }

            m_ReferenceCanvas.style.width = canvas.ReferenceSize.x;
            m_ReferenceCanvas.style.height = canvas.ReferenceSize.y;
            m_BackgroundImage.image = canvas.BackgroundImage;
            m_GuideImage.image = canvas.GuideImage;
            RebuildReferenceStrip();
        }

        private void RebuildReferenceStrip()
        {
            var canvas = m_Adapter?.Canvas;
            var horizontal = canvas?.ConstrainsYAxis == true && canvas.ConstrainsXAxis is false;
            var vertical = canvas?.ConstrainsXAxis == true && canvas.ConstrainsYAxis is false;
            m_ReferenceStrip.style.display = horizontal || vertical ? DisplayStyle.Flex : DisplayStyle.None;
            if (!horizontal && !vertical)
            {
                m_ReferenceStripGraphOffset = Vector2.zero;
                return;
            }

            var areaSize = new Vector2(
                Mathf.Max(1f, m_GraphArea.contentRect.width),
                Mathf.Max(1f, m_GraphArea.contentRect.height));
            var visibleMin = -m_Pan / m_Zoom;
            var visibleMax = (areaSize - m_Pan) / m_Zoom;
            var min = Vector2.Min(Vector2.zero, visibleMin);
            var max = Vector2.Max(canvas.ReferenceSize, visibleMax);
            foreach (var pair in m_NodeViews)
            {
                var view = pair.Value;
                if (view == null)
                {
                    continue;
                }

                min = Vector2.Min(min, view.Position - new Vector2(80f, 80f));
                max = Vector2.Max(
                    max,
                    view.Position + new Vector2(EditorNodeGraphNodeView.DefaultWidth + 80f, 240f));
            }

            if (horizontal)
            {
                m_ReferenceStripGraphOffset = new Vector2(min.x, 0f);
                m_ReferenceStrip.style.left = 0f;
                m_ReferenceStrip.style.top = 0f;
                m_ReferenceStrip.style.width = Mathf.Max(1f, max.x - min.x);
                m_ReferenceStrip.style.height = canvas.ReferenceSize.y;
                SetReferenceStripBorders(0f, 0f, 1f, 1f);
            }
            else
            {
                m_ReferenceStripGraphOffset = new Vector2(0f, min.y);
                m_ReferenceStrip.style.left = 0f;
                m_ReferenceStrip.style.top = 0f;
                m_ReferenceStrip.style.width = canvas.ReferenceSize.x;
                m_ReferenceStrip.style.height = Mathf.Max(1f, max.y - min.y);
                SetReferenceStripBorders(1f, 1f, 0f, 0f);
            }
        }

        private void SetReferenceStripBorders(float left, float right, float top, float bottom)
        {
            m_ReferenceStrip.style.borderLeftWidth = left;
            m_ReferenceStrip.style.borderRightWidth = right;
            m_ReferenceStrip.style.borderTopWidth = top;
            m_ReferenceStrip.style.borderBottomWidth = bottom;
        }

        private void RebuildControlPoints()
        {
            var wires = m_Adapter?.Wires ?? Array.Empty<EditorGraphWireModel>();
            for (var wireIndex = 0; wireIndex < wires.Count; wireIndex++)
            {
                var wire = wires[wireIndex];
                if (wire?.Selected != true || !wire.ControlPointsEditable)
                {
                    continue;
                }

                for (var pointIndex = 0; pointIndex < wire.ControlPoints.Count; pointIndex++)
                {
                    m_Content.Add(new EditorNodeGraphControlPointView(
                        new EditorGraphControlPointRef(wire.WireId, pointIndex),
                        wire.ControlPoints[pointIndex],
                        () => m_Zoom,
                        OnControlPointMoved));
                }
            }
        }

        private void OnControlPointMoved(EditorGraphControlPointRef pointRef, Vector2 position)
        {
            m_Adapter?.MoveWireControlPoint(
                pointRef.WireId,
                pointRef.PointIndex,
                ClampToReferenceCanvas(position));
            Rebuild();
        }

        private Vector2 ClampToReferenceCanvas(Vector2 position)
        {
            var canvas = m_Adapter?.Canvas;
            if (canvas?.HasReferenceSize != true)
            {
                return position;
            }

            return new Vector2(
                canvas.ConstrainsXAxis
                    ? Mathf.Clamp(position.x, 0f, canvas.ReferenceSize.x)
                    : position.x,
                canvas.ConstrainsYAxis
                    ? Mathf.Clamp(position.y, 0f, canvas.ReferenceSize.y)
                    : position.y);
        }

        private static bool TryFindControlPoint(VisualElement target, out EditorGraphControlPointRef pointRef)
        {
            while (target != null)
            {
                if (target.userData is EditorGraphControlPointRef value && value.IsValid)
                {
                    pointRef = value;
                    return true;
                }

                target = target.parent;
            }

            pointRef = default;
            return false;
        }

        private EditorGraphWireModel FindWire(string wireId)
        {
            var wires = m_Adapter?.Wires ?? Array.Empty<EditorGraphWireModel>();
            for (var i = 0; i < wires.Count; i++)
            {
                if (wires[i] != null && string.Equals(wires[i].WireId, wireId, StringComparison.Ordinal))
                {
                    return wires[i];
                }
            }

            return null;
        }

        private int FindClosestSegmentIndex(EditorGraphWireModel wire, Vector2 graphPosition)
        {
            if (!TryResolvePortCanvasPosition(wire.Output, out var startCanvas) ||
                !TryResolvePortCanvasPosition(wire.Input, out var endCanvas))
            {
                return wire.ControlPoints.Count;
            }

            var best = float.MaxValue;
            var bestIndex = 0;
            var previous = CanvasToGraph(startCanvas);
            for (var i = 0; i <= wire.ControlPoints.Count; i++)
            {
                var current = i < wire.ControlPoints.Count
                    ? wire.ControlPoints[i]
                    : CanvasToGraph(endCanvas);
                var distance = DistanceToSegment(graphPosition, previous, current);
                if (distance < best)
                {
                    best = distance;
                    bestIndex = i;
                }

                previous = current;
            }

            return bestIndex;
        }

        private static Vector2 Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            var u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var segment = b - a;
            var length = segment.sqrMagnitude;
            if (length <= 0.0001f)
            {
                return Vector2.Distance(point, a);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / length);
            return Vector2.Distance(point, a + segment * t);
        }

        private void ApplyTransform()
        {
            m_Content.transform.position = new Vector3(m_Pan.x, m_Pan.y, 0f);
            m_Content.transform.scale = new Vector3(m_Zoom, m_Zoom, 1f);
            m_ReferenceCanvas.transform.position = new Vector3(m_Pan.x, m_Pan.y, 0f);
            m_ReferenceCanvas.transform.scale = new Vector3(m_Zoom, m_Zoom, 1f);
            RebuildReferenceStrip();
            var stripPosition = m_Pan + m_ReferenceStripGraphOffset * m_Zoom;
            m_ReferenceStrip.transform.position = new Vector3(stripPosition.x, stripPosition.y, 0f);
            m_ReferenceStrip.transform.scale = new Vector3(m_Zoom, m_Zoom, 1f);
            m_WireLayer.SetViewTransform(m_Pan, m_Zoom);
        }

        private void FrameNodes(bool preferSelection, bool includeReferenceCanvas)
        {
            var nodes = m_Adapter?.Nodes ?? Array.Empty<EditorGraphNodeModel>();
            var selected = nodes.Where(x => x != null && x.Selected).ToList();
            var targets = preferSelection && selected.Count > 0
                ? selected
                : nodes.Where(x => x != null).ToList();
            var canvas = m_Adapter?.Canvas;
            var includeCanvas = includeReferenceCanvas && canvas?.HasReferenceSize == true;
            if (targets.Count == 0 && includeCanvas is false)
            {
                m_Pan = new Vector2(80f, 80f);
                m_Zoom = 1f;
                ApplyTransform();
                return;
            }

            var min = includeCanvas ? Vector2.zero : targets[0].Position;
            var max = includeCanvas
                ? canvas.ReferenceSize
                : targets[0].Position + new Vector2(EditorNodeGraphNodeView.DefaultWidth, 160f);
            for (var i = includeCanvas ? 0 : 1; i < targets.Count; i++)
            {
                var position = targets[i].Position;
                min = Vector2.Min(min, position);
                max = Vector2.Max(max, position + new Vector2(EditorNodeGraphNodeView.DefaultWidth, 160f));
            }

            var size = max - min;
            var areaSize = new Vector2(
                Mathf.Max(1f, m_GraphArea.contentRect.width),
                Mathf.Max(1f, m_GraphArea.contentRect.height));
            var scaleX = (areaSize.x - 160f) / Mathf.Max(1f, size.x);
            var scaleY = (areaSize.y - 160f) / Mathf.Max(1f, size.y);
            m_Zoom = Mathf.Clamp(Mathf.Min(scaleX, scaleY, 1f), 0.35f, 2.25f);
            m_Pan = areaSize * 0.5f - (min + size * 0.5f) * m_Zoom;
            ApplyTransform();
        }

        private void SetStatus(string message)
        {
            m_Status.text = message ?? string.Empty;
        }

        private static bool IsEditingText(VisualElement element)
        {
            while (element != null)
            {
                if (element is TextField || element is FloatField || element is DropdownField)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private bool IsGraphBackgroundTarget(VisualElement element)
        {
            while (element != null && element != m_GraphArea)
            {
                if (element == m_Content || element == m_WireLayer || element == m_ReferenceCanvas)
                {
                    return true;
                }

                if (element == m_BlackboardHost || element == m_Status || element == m_MiniMap)
                {
                    return false;
                }

                if (element.ClassListContains("editor-node-graph-node") ||
                    element.ClassListContains("editor-node-graph-node__port-dot") ||
                    element.ClassListContains("editor-node-graph__selection-box") ||
                    element.ClassListContains("editor-node-graph-control-point") ||
                    element is TextField ||
                    element is FloatField ||
                    element is Toggle ||
                    element is DropdownField)
                {
                    return false;
                }

                element = element.parent;
            }

            return element == m_GraphArea;
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}

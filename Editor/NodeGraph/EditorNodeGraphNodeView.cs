using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorNodeGraph
{
    public sealed class EditorNodeGraphNodeView : VisualElement
    {
        public const float DefaultWidth = 280f;

        private readonly EditorGraphNodeModel m_Node;
        private readonly Func<float> m_GetZoom;
        private readonly Action<string, Vector2> m_Moved;
        private readonly Action<string, Vector2> m_MoveDeltaApplied;
        private readonly Action<EditorGraphPortRef, Vector2> m_OutputDragMoved;
        private readonly Action<EditorGraphPortRef, Vector2> m_OutputDragReleased;
        private readonly Action<string> m_Selected;
        private readonly Action m_FocusCanvas;
        private readonly Action<string, string, string> m_FieldChanged;
        private readonly Func<string, EditorGraphFieldModel, Action<string>, VisualElement> m_CreateCustomField;
        private readonly Dictionary<string, VisualElement> m_InputPorts = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_OutputPorts = new Dictionary<string, VisualElement>(StringComparer.Ordinal);

        private bool m_Dragging;
        private Vector2 m_LastMousePosition;

        public EditorNodeGraphNodeView(
            EditorGraphNodeModel node,
            Func<float> getZoom,
            Action<string> selected,
            Action focusCanvas,
            Action<string, Vector2> moved,
            Action<string, Vector2> moveDeltaApplied,
            Action<EditorGraphPortRef, Vector2> outputDragMoved,
            Action<EditorGraphPortRef, Vector2> outputDragReleased,
            Action<string, string, string> fieldChanged,
            Func<string, EditorGraphFieldModel, Action<string>, VisualElement> createCustomField = null)
        {
            m_Node = node ?? throw new ArgumentNullException(nameof(node));
            m_GetZoom = getZoom;
            m_Selected = selected;
            m_FocusCanvas = focusCanvas;
            m_Moved = moved;
            m_MoveDeltaApplied = moveDeltaApplied;
            m_OutputDragMoved = outputDragMoved;
            m_OutputDragReleased = outputDragReleased;
            m_FieldChanged = fieldChanged;
            m_CreateCustomField = createCustomField;

            Position = node.Position;
            userData = node.NodeId;
            focusable = true;
            AddToClassList("editor-node-graph-node");
            AddToClassList($"editor-node-graph-node--{CssName(node.StyleKey, node.Category)}");
            EnableInClassList("editor-node-graph-node--selected", node.Selected);
            EnableInClassList("editor-node-graph-node--entry", node.Entry);
            ApplyDiagnosticClasses(this, HighestSeverity(node), HasStale(node), "editor-node-graph-node");
            tooltip = DiagnosticsTooltip(node.Diagnostics);

            Build();
            ApplyPosition();

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        public string NodeId => m_Node.NodeId;

        public Vector2 Position { get; internal set; }

        public void SetSelected(bool selected)
        {
            EnableInClassList("editor-node-graph-node--selected", selected);
        }

        public bool TryGetPortAnchor(EditorGraphPortRef portRef, out Vector2 anchor)
        {
            var ports = m_InputPorts;
            if (ports.TryGetValue(portRef.PortId ?? string.Empty, out var input))
            {
                anchor = ResolvePortAnchor(input, true);
                return true;
            }

            ports = m_OutputPorts;
            if (ports.TryGetValue(portRef.PortId ?? string.Empty, out var output))
            {
                anchor = ResolvePortAnchor(output, false);
                return true;
            }

            anchor = Position + new Vector2(DefaultWidth, 56f);
            return false;
        }

        public bool TryFindInputPort(Vector2 graphPosition, out EditorGraphPortRef portRef)
        {
            foreach (var pair in m_InputPorts)
            {
                var anchor = ResolvePortAnchor(pair.Value, true);
                if (Vector2.Distance(anchor, graphPosition) <= 14f)
                {
                    portRef = new EditorGraphPortRef(NodeId, pair.Key);
                    return true;
                }
            }

            portRef = default;
            return false;
        }

        private void Build()
        {
            var header = new VisualElement();
            header.AddToClassList("editor-node-graph-node__header");

            var titleText = SafeText(m_Node.Title, m_Node.NodeId);
            var typeText = m_Node.Entry ? "入口" : SafeText(m_Node.Subtitle, m_Node.Category);
            if (ShouldShowTypeLabel(typeText, titleText, m_Node.Entry))
            {
                var type = new Label(typeText);
                type.AddToClassList("editor-node-graph-node__type");
                type.tooltip = "节点类型。";
                header.Add(type);
            }

            var title = new Label(titleText);
            title.AddToClassList("editor-node-graph-node__title");
            title.tooltip = AppendTooltip("节点类型。", DiagnosticsTooltip(m_Node.Diagnostics));
            header.Add(title);
            var badge = CreateDiagnosticBadge(HighestSeverity(m_Node), HasStale(m_Node));
            if (badge != null)
            {
                header.Add(badge);
            }

            Add(header);

            if (m_Node.InputPorts.Count > 0)
            {
                var inputs = new VisualElement();
                inputs.AddToClassList("editor-node-graph-node__ports");
                for (var i = 0; i < m_Node.InputPorts.Count; i++)
                {
                    inputs.Add(CreatePort(m_Node.InputPorts[i]));
                }

                Add(inputs);
            }

            var body = new VisualElement();
            body.AddToClassList("editor-node-graph-node__body");
            if (m_Node.Fields.Count == 0)
            {
                var empty = new Label("无参数") { tooltip = "这个节点没有需要填写的节点内字段。" };
                empty.AddToClassList("editor-node-graph-node__empty");
                body.Add(empty);
            }
            else
            {
                for (var i = 0; i < m_Node.Fields.Count; i++)
                {
                    body.Add(CreateField(m_Node.Fields[i]));
                }
            }

            Add(body);

            if (m_Node.OutputPorts.Count > 0)
            {
                var outputs = new VisualElement();
                outputs.AddToClassList("editor-node-graph-node__ports");
                for (var i = 0; i < m_Node.OutputPorts.Count; i++)
                {
                    outputs.Add(CreatePort(m_Node.OutputPorts[i]));
                }

                Add(outputs);
            }
        }

        private VisualElement CreatePort(EditorGraphPortModel port)
        {
            var row = new VisualElement
            {
                tooltip = string.IsNullOrWhiteSpace(port.Tooltip)
                    ? $"{(port.Direction == EditorGraphPortDirection.Input ? "输入" : "输出")}端口：{port.PortId}"
                    : port.Tooltip
            };
            row.AddToClassList("editor-node-graph-node__port-row");
            row.AddToClassList(port.Direction == EditorGraphPortDirection.Input
                ? "editor-node-graph-node__port-row--input"
                : "editor-node-graph-node__port-row--output");
            row.tooltip = AppendTooltip(row.tooltip, DiagnosticsTooltip(port.Diagnostics));
            ApplyDiagnosticClasses(row, HighestSeverity(port.Diagnostics), HasStale(port.Diagnostics), "editor-node-graph-node__port-row");

            var dot = new VisualElement { userData = new EditorGraphPortRef(NodeId, port.PortId) };
            dot.AddToClassList("editor-node-graph-node__port-dot");
            dot.style.backgroundColor = port.Color;
            dot.tooltip = row.tooltip;
            ApplyDiagnosticClasses(dot, HighestSeverity(port.Diagnostics), HasStale(port.Diagnostics), "editor-node-graph-node__port-dot");

            var label = new Label(SafeText(port.Label, port.PortId));
            label.AddToClassList("editor-node-graph-node__port-label");

            if (port.Direction == EditorGraphPortDirection.Input)
            {
                row.Add(dot);
                row.Add(label);
                m_InputPorts[port.PortId] = dot;
            }
            else
            {
                row.Add(label);
                row.Add(dot);
                m_OutputPorts[port.PortId] = dot;
                RegisterOutputPortDrag(dot, new EditorGraphPortRef(NodeId, port.PortId));
            }

            return row;
        }

        private VisualElement CreateField(EditorGraphFieldModel field)
        {
            switch (field.ValueType)
            {
                case EditorGraphFieldValueType.Number:
                    var number = new FloatField(field.Label) { tooltip = AppendTooltip(field.Tooltip, DiagnosticsTooltip(field.Diagnostics)) };
                    number.SetValueWithoutNotify(ParseFloat(field.Value));
                    number.RegisterValueChangedCallback(evt => m_FieldChanged?.Invoke(NodeId, field.FieldId, evt.newValue.ToString(CultureInfo.InvariantCulture)));
                    return DecorateField(number, field);
                case EditorGraphFieldValueType.Boolean:
                    var toggle = new Toggle(field.Label) { tooltip = AppendTooltip(field.Tooltip, DiagnosticsTooltip(field.Diagnostics)) };
                    toggle.SetValueWithoutNotify(IsTrue(field.Value));
                    toggle.RegisterValueChangedCallback(evt => m_FieldChanged?.Invoke(NodeId, field.FieldId, evt.newValue ? "true" : "false"));
                    return DecorateField(toggle, field);
                case EditorGraphFieldValueType.Option:
                    var optionItems = new List<EditorGraphFieldOption>(field.OptionItems ?? Array.Empty<EditorGraphFieldOption>());
                    if (optionItems.Count == 0)
                    {
                        var fallbackOptions = field.Options ?? Array.Empty<string>();
                        for (var i = 0; i < fallbackOptions.Count; i++)
                        {
                            optionItems.Add(new EditorGraphFieldOption(fallbackOptions[i], fallbackOptions[i]));
                        }
                    }

                    var choices = new List<string>(optionItems.Count);
                    for (var i = 0; i < optionItems.Count; i++)
                    {
                        choices.Add(optionItems[i].Label);
                    }

                    if (choices.Count == 0)
                    {
                        choices.Add(string.Empty);
                    }

                    var displayValue = string.IsNullOrWhiteSpace(field.DisplayValue) ? field.Value ?? string.Empty : field.DisplayValue;
                    if (choices.Contains(displayValue) is false)
                    {
                        choices.Insert(0, displayValue);
                    }

                    var dropdown = new DropdownField(field.Label, choices, displayValue) { tooltip = AppendTooltip(field.Tooltip, DiagnosticsTooltip(field.Diagnostics)) };
                    dropdown.RegisterValueChangedCallback(evt =>
                    {
                        var selectedValue = evt.newValue;
                        for (var i = 0; i < optionItems.Count; i++)
                        {
                            if (string.Equals(optionItems[i].Label, evt.newValue, StringComparison.Ordinal))
                            {
                                selectedValue = optionItems[i].Value;
                                break;
                            }
                        }

                        m_FieldChanged?.Invoke(NodeId, field.FieldId, selectedValue);
                    });
                    return DecorateField(dropdown, field);
                case EditorGraphFieldValueType.AssetReference:
                    return CreateAssetField(field);
                case EditorGraphFieldValueType.Custom:
                    return CreateCustomField(field);
                default:
                    var text = new TextField(field.Label) { isDelayed = true, tooltip = AppendTooltip(field.Tooltip, DiagnosticsTooltip(field.Diagnostics)) };
                    text.SetValueWithoutNotify(field.Value ?? string.Empty);
                    text.RegisterValueChangedCallback(evt => m_FieldChanged?.Invoke(NodeId, field.FieldId, evt.newValue));
                    return DecorateField(text, field);
            }
        }

        private VisualElement CreateCustomField(EditorGraphFieldModel field)
        {
            var custom = m_CreateCustomField?.Invoke(
                NodeId,
                field,
                value => m_FieldChanged?.Invoke(NodeId, field.FieldId, value));
            if (custom != null)
            {
                return DecorateField(custom, field);
            }

            var fallback = new TextField(field.Label)
            {
                isReadOnly = true,
                tooltip = AppendTooltip(
                    $"Custom field renderer is unavailable. type:{field.CustomType ?? "unknown"}",
                    DiagnosticsTooltip(field.Diagnostics))
            };
            fallback.SetValueWithoutNotify(field.Value ?? string.Empty);
            return DecorateField(fallback, field);
        }

        private VisualElement CreateAssetField(EditorGraphFieldModel field)
        {
            var container = new VisualElement { tooltip = AppendTooltip(field.Tooltip, DiagnosticsTooltip(field.Diagnostics)) };
            container.AddToClassList("editor-node-graph-node__field");
            ApplyDiagnosticClasses(container, HighestSeverity(field.Diagnostics), HasStale(field.Diagnostics), "editor-node-graph-node__field");

            var objectField = new ObjectField(field.Label)
            {
                tooltip = AppendTooltip(field.Tooltip, DiagnosticsTooltip(field.Diagnostics)),
                objectType = ResolveObjectType(field.ResourceType),
                allowSceneObjects = false
            };
            objectField.SetValueWithoutNotify(LoadAsset(field.Value, objectField.objectType));
            objectField.RegisterValueChangedCallback(evt =>
            {
                m_FieldChanged?.Invoke(NodeId, field.FieldId, ToStableAssetValue(evt.newValue));
            });
            container.Add(objectField);

            var stableValue = new TextField("资源路径") { isDelayed = true, tooltip = AppendTooltip("序列化保存到剧情数据的 Assets/... 资源路径；也可以填写业务资源 key。", DiagnosticsTooltip(field.Diagnostics)) };
            stableValue.SetValueWithoutNotify(field.Value ?? string.Empty);
            stableValue.RegisterValueChangedCallback(evt => m_FieldChanged?.Invoke(NodeId, field.FieldId, evt.newValue));
            container.Add(stableValue);

            return container;
        }

        private VisualElement DecorateField(VisualElement field, EditorGraphFieldModel model)
        {
            field.AddToClassList("editor-node-graph-node__field");
            field.tooltip = AppendTooltip(
                AppendTooltip(field.tooltip, model.Tooltip),
                DiagnosticsTooltip(model.Diagnostics));
            ApplyDiagnosticClasses(field, HighestSeverity(model.Diagnostics), HasStale(model.Diagnostics), "editor-node-graph-node__field");
            return field;
        }

        private void RegisterOutputPortDrag(VisualElement output, EditorGraphPortRef portRef)
        {
            var dragging = false;
            var dragStarted = false;
            var start = Vector2.zero;
            output.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                m_FocusCanvas?.Invoke();
                m_Selected?.Invoke(NodeId);
                dragging = true;
                dragStarted = false;
                start = ToPanelPosition(output, evt.localMousePosition);
                output.CaptureMouse();
                evt.StopPropagation();
            });
            output.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }

                var currentMousePosition = ToPanelPosition(output, evt.localMousePosition);
                if (!dragStarted && Vector2.Distance(start, currentMousePosition) > 4f)
                {
                    dragStarted = true;
                    output.AddToClassList("editor-node-graph-node__port-dot--dragging");
                }

                if (dragStarted)
                {
                    m_OutputDragMoved?.Invoke(portRef, currentMousePosition);
                }

                evt.StopPropagation();
            });
            output.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }

                dragging = false;
                output.ReleaseMouse();
                output.RemoveFromClassList("editor-node-graph-node__port-dot--dragging");
                m_OutputDragReleased?.Invoke(portRef, ToPanelPosition(output, evt.localMousePosition));
                evt.StopPropagation();
            });
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || IsInteractiveTarget(evt.target as VisualElement))
            {
                return;
            }

            m_FocusCanvas?.Invoke();
            m_Selected?.Invoke(NodeId);
            m_Dragging = true;
            m_LastMousePosition = ToPanelPosition(this, evt.localMousePosition);
            BringToFront();
            this.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!m_Dragging)
            {
                return;
            }

            var zoom = Mathf.Max(0.01f, m_GetZoom?.Invoke() ?? 1f);
            var currentMousePosition = ToPanelPosition(this, evt.localMousePosition);
            var delta = (currentMousePosition - m_LastMousePosition) / zoom;
            m_LastMousePosition = currentMousePosition;
            Position += delta;
            ApplyPosition();
            m_Moved?.Invoke(NodeId, Position);
            m_MoveDeltaApplied?.Invoke(NodeId, delta);
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (!m_Dragging)
            {
                return;
            }

            m_Dragging = false;
            this.ReleaseMouse();
            evt.StopPropagation();
        }

        internal void ApplyPosition()
        {
            style.position = UnityEngine.UIElements.Position.Absolute;
            style.left = Position.x;
            style.top = Position.y;
            style.width = DefaultWidth;
        }

        private Vector2 ResolvePortAnchor(VisualElement port, bool input)
        {
            var world = port.worldBound.center;
            if (world == Vector2.zero || port.panel == null)
            {
                return Position + new Vector2(input ? 0f : DefaultWidth, 56f);
            }

            var local = this.WorldToLocal(world);
            return Position + local;
        }

        private static bool IsInteractiveTarget(VisualElement element)
        {
            while (element != null)
            {
                if (element.ClassListContains("editor-node-graph-node__port-dot") ||
                    element is TextField ||
                    element is FloatField ||
                    element is Toggle ||
                    element is DropdownField)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private static Vector2 ToPanelPosition(VisualElement element, Vector2 localMousePosition)
        {
            return element.LocalToWorld(localMousePosition);
        }

        private static float ParseFloat(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0f;
        }

        private static Type ResolveObjectType(string resourceType)
        {
            if (string.IsNullOrWhiteSpace(resourceType))
            {
                return typeof(UnityEngine.Object);
            }

            var type = ResolveType(resourceType);
            return type != null && typeof(UnityEngine.Object).IsAssignableFrom(type)
                ? type
                : typeof(UnityEngine.Object);
        }

        private static Type ResolveType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            var unityModules = new[]
            {
                "UnityEngine.CoreModule",
                "UnityEngine.VideoModule",
                "UnityEngine.AudioModule",
                "UnityEngine.ImageConversionModule",
                "UnityEngine"
            };
            for (var i = 0; i < unityModules.Length; i++)
            {
                type = Type.GetType($"{typeName}, {unityModules[i]}");
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static UnityEngine.Object LoadAsset(string stableValue, Type objectType)
        {
            if (string.IsNullOrWhiteSpace(stableValue))
            {
                return null;
            }

            string path;
            if (stableValue.StartsWith("guid:", StringComparison.Ordinal))
            {
                var guid = stableValue.Substring("guid:".Length);
                path = AssetDatabase.GUIDToAssetPath(guid);
            }
            else if (stableValue.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                path = stableValue;
            }
            else
            {
                path = AssetDatabase.GUIDToAssetPath(stableValue);
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath(path, objectType ?? typeof(UnityEngine.Object));
        }

        private static string ToStableAssetValue(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Replace('\\', '/');
        }

        private static bool IsTrue(string value)
        {
            return bool.TryParse(value, out var result) && result;
        }

        private static Label CreateDiagnosticBadge(EditorGraphDiagnosticSeverity severity, bool stale)
        {
            if (severity == EditorGraphDiagnosticSeverity.Info && !stale)
            {
                return null;
            }

            var badge = new Label("!");
            badge.AddToClassList("editor-node-graph-node__diagnostic-badge");
            ApplyDiagnosticClasses(badge, severity, stale, "editor-node-graph-node__diagnostic-badge");
            return badge;
        }

        private static EditorGraphDiagnosticSeverity HighestSeverity(EditorGraphNodeModel node)
        {
            var severity = HighestSeverity(node.Diagnostics);
            severity = Max(severity, HighestSeverity(node.InputPorts));
            severity = Max(severity, HighestSeverity(node.OutputPorts));
            severity = Max(severity, HighestSeverity(node.Fields));
            return severity;
        }

        private static EditorGraphDiagnosticSeverity HighestSeverity(IReadOnlyList<EditorGraphPortModel> ports)
        {
            var severity = EditorGraphDiagnosticSeverity.Info;
            for (var i = 0; i < (ports?.Count ?? 0); i++)
            {
                severity = Max(severity, HighestSeverity(ports[i].Diagnostics));
            }

            return severity;
        }

        private static EditorGraphDiagnosticSeverity HighestSeverity(IReadOnlyList<EditorGraphFieldModel> fields)
        {
            var severity = EditorGraphDiagnosticSeverity.Info;
            for (var i = 0; i < (fields?.Count ?? 0); i++)
            {
                severity = Max(severity, HighestSeverity(fields[i].Diagnostics));
            }

            return severity;
        }

        private static EditorGraphDiagnosticSeverity HighestSeverity(IReadOnlyList<EditorGraphDiagnostic> diagnostics)
        {
            var severity = EditorGraphDiagnosticSeverity.Info;
            for (var i = 0; i < (diagnostics?.Count ?? 0); i++)
            {
                severity = Max(severity, diagnostics[i].Severity);
            }

            return severity;
        }

        private static bool HasStale(EditorGraphNodeModel node)
        {
            return HasStale(node.Diagnostics) ||
                   HasStale(node.InputPorts) ||
                   HasStale(node.OutputPorts) ||
                   HasStale(node.Fields);
        }

        private static bool HasStale(IReadOnlyList<EditorGraphPortModel> ports)
        {
            for (var i = 0; i < (ports?.Count ?? 0); i++)
            {
                if (HasStale(ports[i].Diagnostics))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasStale(IReadOnlyList<EditorGraphFieldModel> fields)
        {
            for (var i = 0; i < (fields?.Count ?? 0); i++)
            {
                if (HasStale(fields[i].Diagnostics))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasStale(IReadOnlyList<EditorGraphDiagnostic> diagnostics)
        {
            for (var i = 0; i < (diagnostics?.Count ?? 0); i++)
            {
                if (diagnostics[i].Stale)
                {
                    return true;
                }
            }

            return false;
        }

        private static EditorGraphDiagnosticSeverity Max(EditorGraphDiagnosticSeverity left, EditorGraphDiagnosticSeverity right)
        {
            return left > right ? left : right;
        }

        private static void ApplyDiagnosticClasses(VisualElement element, EditorGraphDiagnosticSeverity severity, bool stale, string block)
        {
            if (element == null)
            {
                return;
            }

            element.EnableInClassList($"{block}--diagnostic-warning", severity == EditorGraphDiagnosticSeverity.Warning);
            element.EnableInClassList($"{block}--diagnostic-error", severity == EditorGraphDiagnosticSeverity.Error);
            element.EnableInClassList($"{block}--diagnostic-stale", stale);
        }

        private static string DiagnosticsTooltip(IReadOnlyList<EditorGraphDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                return null;
            }

            var lines = new List<string>();
            for (var i = 0; i < diagnostics.Count && lines.Count < 3; i++)
            {
                var diagnostic = diagnostics[i];
                lines.Add(string.IsNullOrWhiteSpace(diagnostic.Tooltip) ? diagnostic.Message : diagnostic.Tooltip);
            }

            if (diagnostics.Count > lines.Count)
            {
                lines.Add($"还有 {diagnostics.Count - lines.Count} 个问题，请查看左侧问题列表。");
            }

            return string.Join("\n", lines);
        }

        private static string AppendTooltip(string tooltip, string diagnosticsTooltip)
        {
            if (string.IsNullOrWhiteSpace(diagnosticsTooltip))
            {
                return tooltip;
            }

            return string.IsNullOrWhiteSpace(tooltip) ? diagnosticsTooltip : $"{tooltip}\n{diagnosticsTooltip}";
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

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static bool ShouldShowTypeLabel(string typeText, string titleText, bool entry)
        {
            if (entry)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(typeText))
            {
                return false;
            }

            return string.Equals(typeText.Trim(), (titleText ?? string.Empty).Trim(), StringComparison.Ordinal) is false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameDeveloperKit.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameDeveloperKit.UIEditor
{
    /// <summary>
    /// 定义 UI Document Inspector 类型。
    /// </summary>
    [CustomEditor(typeof(UIDocument))]
    public sealed class UIDocumentInspector : Editor
    {
        private const float RowHeight = 24f;
        private const float RemoveWidth = 26f;
        private const float AddComponentWidth = 24f;
        private const float LocalizationWidth = 160f;
        private const string EmptyDataBindingMessage = "数据绑定待接入 DataUIBinder 风格的数据路径后显示。";

        private SerializedProperty m_FullScreenRoot;
        private SerializedProperty m_LayerOrder;
        private SerializedProperty m_Mappings;
        private SerializedProperty m_LocalizedTexts;
        private UIDocumentLocalizationDrawer m_LocalizationDrawer;

        private bool m_ShowNodeBindings = true;
        private bool m_ShowDataInfo;
        private int m_SelectedMappingIndex = -1;

        private void OnEnable()
        {
            m_FullScreenRoot = serializedObject.FindProperty("fullScreenRoot");
            m_LayerOrder = serializedObject.FindProperty("layerOrder");
            m_Mappings = serializedObject.FindProperty("mappings");
            m_LocalizedTexts = serializedObject.FindProperty("localizedTexts");
            m_LocalizationDrawer = new UIDocumentLocalizationDrawer(m_Mappings, m_LocalizedTexts);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ClampSelectedMappingIndex();
            m_LocalizationDrawer.RemoveStaleBindings();

            DrawDocumentSettings();
            EditorGUILayout.Space(4f);
            DrawNodeBindingSection(CollectBindingRows());
            EditorGUILayout.Space(4f);
            DrawDataInfoSection();
            EditorGUILayout.Space(6f);
            DrawBottomActions();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDocumentSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(m_FullScreenRoot);
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("代码生成", EditorStyles.boldLabel);
            DrawLayerPopup("Layer", m_LayerOrder);
            EditorGUILayout.EndVertical();
        }

        private static void DrawLayerPopup(string label, SerializedProperty layerOrder)
        {
            var layerNames = new[] { "Background (0)", "Main (100)", "Window (200)", "Loading (300)", "Message (400)", "StoryPlayback (500)" };
            var layerOrders = new[] { 0, 100, 200, 300, 400, 500 };
            var selectedIndex = System.Array.IndexOf(layerOrders, layerOrder.intValue);
            if (selectedIndex < 0) selectedIndex = 2;
            var newIndex = EditorGUILayout.Popup(label, selectedIndex, layerNames);
            layerOrder.intValue = layerOrders[newIndex];
        }

        private void DrawNodeBindingSection(List<BindingRow> rows)
        {
            m_ShowNodeBindings = EditorGUILayout.Foldout(m_ShowNodeBindings, "UI节点绑定", true);
            if (m_ShowNodeBindings is false)
            {
                return;
            }

            DrawBindingTableHeader();
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("没有绑定。使用下方 AutoBind 扫描 b_ 节点。", MessageType.Info);
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                DrawBindingRow(i, rows[i]);
            }
        }

        private void DrawBindingTableHeader()
        {
            var rect = GUILayoutUtility.GetRect(0, RowHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.22f, 0.22f, 0.22f));

            var columns = CalculateColumns(rect);
            EditorGUI.LabelField(columns.Value, "Value (组件引用)", EditorStyles.miniLabel);
            EditorGUI.LabelField(columns.Localization, "本地化", EditorStyles.miniLabel);
        }

        private void DrawBindingRow(int rowIndex, BindingRow row)
        {
            var rect = GUILayoutUtility.GetRect(0, RowHeight, GUILayout.ExpandWidth(true));
            var selected = row.MappingIndex == m_SelectedMappingIndex;
            if (UnityEngine.Event.current.type == EventType.Repaint)
            {
                if (selected)
                {
                    EditorGUI.DrawRect(rect, new Color(0.16f, 0.36f, 0.62f, 0.45f));
                }
                else if (rowIndex % 2 == 1)
                {
                    EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.035f));
                }
            }

            var columns = CalculateColumns(rect);
            if (UnityEngine.Event.current.type == EventType.MouseDown &&
                rect.Contains(UnityEngine.Event.current.mousePosition) &&
                columns.Remove.Contains(UnityEngine.Event.current.mousePosition) is false)
            {
                m_SelectedMappingIndex = row.MappingIndex;
                GUI.changed = true;
            }

            DrawValueField(columns.Value, row);
            DrawLocalizationField(columns.Localization, row);
            if (GUI.Button(columns.Remove, "X", EditorStyles.miniButton))
            {
                RemoveBindingRow(row);
            }
        }

        private void DrawValueField(Rect rect, BindingRow row)
        {
            if (IsValidMappingIndex(row.MappingIndex) is false)
            {
                return;
            }

            var target = row.Target;
            if (row.ComponentIndex < 0)
            {
                var mapping = GetMappingProperty(row.MappingIndex);
                var nameProperty = mapping.FindPropertyRelative("Name");
                var targetProperty = mapping.FindPropertyRelative("Target");
                var componentsProperty = mapping.FindPropertyRelative("Components");
                var targetRect = new Rect(rect.x, rect.y, rect.width - AddComponentWidth - 2f, rect.height);
                var addRect = new Rect(targetRect.xMax + 2f, rect.y, AddComponentWidth, rect.height);

                EditorGUI.BeginChangeCheck();
                var nextTarget = EditorGUI.ObjectField(targetRect, target, typeof(GameObject), true) as GameObject;
                if (EditorGUI.EndChangeCheck())
                {
                    targetProperty.objectReferenceValue = nextTarget;
                    FillNameIfEmpty(nameProperty, nextTarget);
                    RemoveComponentsNotOnTarget(componentsProperty, nextTarget);
                }

                DrawAddComponentButton(addRect, row.MappingIndex);
                return;
            }

            using (new EditorGUI.DisabledScope(target == null))
            {
                var componentRect = new Rect(rect.x, rect.y, rect.width - AddComponentWidth - 2f, rect.height);
                var addRect = new Rect(componentRect.xMax + 2f, rect.y, AddComponentWidth, rect.height);
                var selectedComponent = row.Component;
                var nextComponent = EditorGUI.ObjectField(componentRect, selectedComponent, typeof(Component), true) as Component;
                if (nextComponent != selectedComponent)
                {
                    SetComponent(row.MappingIndex, row.ComponentIndex, nextComponent);
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                }

                DrawAddComponentButton(addRect, row.MappingIndex);
            }
        }

        private void DrawAddComponentButton(Rect rect, int mappingIndex)
        {
            using (new EditorGUI.DisabledScope(GetAvailableComponents(mappingIndex).Length == 0))
            {
                if (GUI.Button(rect, "+", EditorStyles.miniButton))
                {
                    ShowAddComponentMenu(rect, mappingIndex);
                }
            }
        }

        private void DrawLocalizationField(Rect rect, BindingRow row)
        {
            if (row.Component == null || UIDocumentLocalizationDrawer.IsLocalizableComponent(row.Component) is false)
            {
                GUI.Label(rect, "-", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            m_LocalizationDrawer.DrawComponentKeyPopup(rect, row.Component, row.MappingName);
        }

        private void DrawDataInfoSection()
        {
            m_ShowDataInfo = EditorGUILayout.Foldout(m_ShowDataInfo, "数据信息", true);
            if (m_ShowDataInfo is false)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox(EmptyDataBindingMessage, MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void DrawBottomActions()
        {
            EditorGUILayout.BeginHorizontal();

            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.28f, 0.42f, 0.36f);
            if (GUILayout.Button("AutoBind", GUILayout.Height(28f)))
            {
                AutoBind();
            }

            GUI.backgroundColor = new Color(0.65f, 0.25f, 0.25f);
            if (GUILayout.Button("清空绑定", GUILayout.Height(28f)))
            {
                ClearAllBindings();
            }

            GUI.backgroundColor = new Color(0.55f, 0.38f, 0.2f);
            if (GUILayout.Button("验证绑定", GUILayout.Height(28f)))
            {
                ShowValidationDialog();
            }

            GUI.backgroundColor = new Color(0.2f, 0.42f, 0.65f);
            using (new EditorGUI.DisabledScope(CanGenerateCode() is false))
            {
                if (GUILayout.Button("生成 UI 代码", GUILayout.Height(28f)))
                {
                    Generate();
                }
            }

            GUI.backgroundColor = previousColor;
            EditorGUILayout.EndHorizontal();
        }

        private BindingColumns CalculateColumns(Rect rect)
        {
            var removeRect = new Rect(rect.xMax - RemoveWidth - 2f, rect.y + 2f, RemoveWidth, rect.height - 4f);
            var localizationRect = new Rect(removeRect.x - LocalizationWidth - 4f, rect.y + 2f, LocalizationWidth, rect.height - 4f);
            var valueRect = new Rect(rect.x + 4f, rect.y + 2f, localizationRect.x - rect.x - 8f, rect.height - 4f);
            return new BindingColumns(valueRect, localizationRect, removeRect);
        }

        private List<BindingRow> CollectBindingRows()
        {
            var result = new List<BindingRow>();
            for (var mappingIndex = 0; mappingIndex < m_Mappings.arraySize; mappingIndex++)
            {
                var mapping = GetMappingProperty(mappingIndex);
                var mappingName = mapping.FindPropertyRelative("Name").stringValue;
                var targetObject = mapping.FindPropertyRelative("Target").objectReferenceValue as GameObject;
                var components = mapping.FindPropertyRelative("Components");
                if (components.arraySize == 0)
                {
                    result.Add(new BindingRow(mappingIndex, -1, mappingName, targetObject, null));
                    continue;
                }

                for (var componentIndex = 0; componentIndex < components.arraySize; componentIndex++)
                {
                    var component = components.GetArrayElementAtIndex(componentIndex).objectReferenceValue as Component;
                    result.Add(new BindingRow(mappingIndex, componentIndex, mappingName, targetObject, component));
                }
            }

            result.Sort(CompareBindingRows);
            return result;
        }

        private void AutoBind()
        {
            var document = (UIDocument)target;
            var firstAddedIndex = -1;
            for (var i = 0; i < m_Mappings.arraySize; i++)
            {
                var mapping = GetMappingProperty(i);
                FillNameIfEmpty(mapping.FindPropertyRelative("Name"), mapping.FindPropertyRelative("Target").objectReferenceValue as GameObject);
            }

            foreach (var child in document.GetComponentsInChildren<Transform>(true))
            {
                var childObject = child.gameObject;
                if (childObject == document.gameObject || childObject.name.StartsWith("b_", StringComparison.Ordinal) is false)
                {
                    continue;
                }

                var index = EnsureMapping(childObject);
                if (firstAddedIndex < 0)
                {
                    firstAddedIndex = index;
                }
            }

            if (firstAddedIndex >= 0)
            {
                m_SelectedMappingIndex = firstAddedIndex;
            }

            GUI.changed = true;
            Repaint();
        }

        private int AddEmptyMapping()
        {
            var index = m_Mappings.arraySize;
            m_Mappings.InsertArrayElementAtIndex(index);
            var mapping = GetMappingProperty(index);
            mapping.FindPropertyRelative("Name").stringValue = string.Empty;
            mapping.FindPropertyRelative("Target").objectReferenceValue = null;
            mapping.FindPropertyRelative("Components").arraySize = 0;
            return index;
        }

        private int EnsureMapping(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return -1;
            }

            var index = GetMappingIndex(targetObject);
            if (index < 0)
            {
                index = AddEmptyMapping();
                var mapping = GetMappingProperty(index);
                mapping.FindPropertyRelative("Target").objectReferenceValue = targetObject;
                AddDefaultComponent(mapping.FindPropertyRelative("Components"), targetObject);
            }
            else
            {
                var mapping = GetMappingProperty(index);
                var components = mapping.FindPropertyRelative("Components");
                if (components.arraySize == 0)
                {
                    AddDefaultComponent(components, targetObject);
                }
            }

            FillNameIfEmpty(GetMappingProperty(index).FindPropertyRelative("Name"), targetObject);
            return index;
        }

        private int GetMappingIndex(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return -1;
            }

            for (var i = 0; i < m_Mappings.arraySize; i++)
            {
                if (GetMappingProperty(i).FindPropertyRelative("Target").objectReferenceValue == targetObject)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemoveBindingRow(BindingRow row)
        {
            if (IsValidMappingIndex(row.MappingIndex) is false)
            {
                return;
            }

            if (row.ComponentIndex < 0)
            {
                RemoveMapping(row.MappingIndex);
                return;
            }

            var components = GetMappingProperty(row.MappingIndex).FindPropertyRelative("Components");
            if (row.Component != null)
            {
                m_LocalizationDrawer.RemoveBindings(row.Component);
            }

            DeleteArrayElement(components, row.ComponentIndex);
            if (components.arraySize == 0)
            {
                RemoveMapping(row.MappingIndex);
            }
        }

        private void RemoveMapping(int mappingIndex)
        {
            if (IsValidMappingIndex(mappingIndex) is false)
            {
                return;
            }

            var components = GetMappingProperty(mappingIndex).FindPropertyRelative("Components");
            for (var i = 0; i < components.arraySize; i++)
            {
                if (components.GetArrayElementAtIndex(i).objectReferenceValue is Component component)
                {
                    m_LocalizationDrawer.RemoveBindings(component);
                }
            }

            DeleteArrayElement(m_Mappings, mappingIndex);
            ClampSelectedMappingIndex();
            m_LocalizationDrawer.RemoveStaleBindings();
        }

        private void SetComponent(int mappingIndex, int componentIndex, Component component)
        {
            if (IsValidMappingIndex(mappingIndex) is false)
            {
                return;
            }

            var mapping = GetMappingProperty(mappingIndex);
            var targetObject = mapping.FindPropertyRelative("Target").objectReferenceValue as GameObject;
            if (component != null && (targetObject == null || component.gameObject != targetObject))
            {
                EditorUtility.DisplayDialog("组件无效", "组件必须属于当前绑定的 Target。", "OK");
                return;
            }

            var components = mapping.FindPropertyRelative("Components");
            if (component == null)
            {
                if (components.GetArrayElementAtIndex(componentIndex).objectReferenceValue is Component oldComponent)
                {
                    m_LocalizationDrawer.RemoveBindings(oldComponent);
                }

                DeleteArrayElement(components, componentIndex);
                return;
            }

            for (var i = 0; i < components.arraySize; i++)
            {
                if (i != componentIndex && components.GetArrayElementAtIndex(i).objectReferenceValue == component)
                {
                    return;
                }
            }

            if (components.GetArrayElementAtIndex(componentIndex).objectReferenceValue is Component previousComponent)
            {
                m_LocalizationDrawer.RemoveBindings(previousComponent);
            }

            components.GetArrayElementAtIndex(componentIndex).objectReferenceValue = component;
        }

        private void ShowAddComponentMenu(Rect anchor, int mappingIndex)
        {
            var availableComponents = GetAvailableComponents(mappingIndex);
            var menu = new GenericMenu();
            if (availableComponents.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有可添加组件"));
            }
            else
            {
                foreach (var item in CreateComponentMenuItems(availableComponents))
                {
                    var component = item.Component;
                    menu.AddItem(new GUIContent(item.Label), false, () => AddComponentToMappingFromMenu(mappingIndex, component));
                }
            }

            menu.DropDown(anchor);
        }

        private Component[] GetAvailableComponents(int mappingIndex)
        {
            if (IsValidMappingIndex(mappingIndex) is false)
            {
                return Array.Empty<Component>();
            }

            var mapping = GetMappingProperty(mappingIndex);
            var targetObject = mapping.FindPropertyRelative("Target").objectReferenceValue as GameObject;
            if (targetObject == null)
            {
                return Array.Empty<Component>();
            }

            var selected = GetSelectedComponents(mappingIndex);
            return targetObject
                .GetComponents<Component>()
                .Where(component => component != null && component is Transform is false && selected.Contains(component) is false)
                .ToArray();
        }

        private List<Component> GetSelectedComponents(int mappingIndex)
        {
            var result = new List<Component>();
            if (IsValidMappingIndex(mappingIndex) is false)
            {
                return result;
            }

            var components = GetMappingProperty(mappingIndex).FindPropertyRelative("Components");
            for (var i = 0; i < components.arraySize; i++)
            {
                if (components.GetArrayElementAtIndex(i).objectReferenceValue is Component component)
                {
                    result.Add(component);
                }
            }

            return result;
        }

        private void AddComponentToMapping(int mappingIndex, Component component)
        {
            if (component == null || IsValidMappingIndex(mappingIndex) is false)
            {
                return;
            }

            var components = GetMappingProperty(mappingIndex).FindPropertyRelative("Components");
            for (var i = 0; i < components.arraySize; i++)
            {
                if (components.GetArrayElementAtIndex(i).objectReferenceValue == component)
                {
                    return;
                }
            }

            components.InsertArrayElementAtIndex(components.arraySize);
            components.GetArrayElementAtIndex(components.arraySize - 1).objectReferenceValue = component;
        }

        private void AddComponentToMappingFromMenu(int mappingIndex, Component component)
        {
            serializedObject.Update();
            Undo.RecordObject(target, "Add UI Binding Component");
            AddComponentToMapping(mappingIndex, component);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            GUI.changed = true;
            Repaint();
        }

        private void RemoveComponentsNotOnTarget(SerializedProperty components, GameObject targetObject)
        {
            for (var i = components.arraySize - 1; i >= 0; i--)
            {
                var component = components.GetArrayElementAtIndex(i).objectReferenceValue as Component;
                if (component == null || targetObject == null || component.gameObject != targetObject)
                {
                    if (component != null)
                    {
                        m_LocalizationDrawer.RemoveBindings(component);
                    }

                    DeleteArrayElement(components, i);
                }
            }
        }

        private static void AddDefaultComponent(SerializedProperty components, GameObject targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            var component = targetObject.GetComponents<Component>().FirstOrDefault(candidate => candidate != null && candidate is Transform is false);
            if (component == null)
            {
                return;
            }

            components.InsertArrayElementAtIndex(components.arraySize);
            components.GetArrayElementAtIndex(components.arraySize - 1).objectReferenceValue = component;
        }

        private static IEnumerable<ComponentMenuItem> CreateComponentMenuItems(Component[] components)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var component in components)
            {
                var name = component.GetType().Name;
                counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
            }

            foreach (var component in components)
            {
                var name = component.GetType().Name;
                seen[name] = seen.TryGetValue(name, out var index) ? index + 1 : 1;
                yield return new ComponentMenuItem(counts[name] <= 1 ? name : name + " " + seen[name], component);
            }
        }

        private bool IsDocumentChild(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return false;
            }

            var document = (UIDocument)target;
            return targetObject != document.gameObject && targetObject.transform.IsChildOf(document.transform);
        }

        private SerializedProperty GetMappingProperty(int mappingIndex)
        {
            return m_Mappings.GetArrayElementAtIndex(mappingIndex);
        }

        private bool IsValidMappingIndex(int mappingIndex)
        {
            return mappingIndex >= 0 && mappingIndex < m_Mappings.arraySize;
        }

        private void ClampSelectedMappingIndex()
        {
            if (m_Mappings.arraySize == 0)
            {
                m_SelectedMappingIndex = -1;
                return;
            }

            if (m_SelectedMappingIndex >= m_Mappings.arraySize)
            {
                m_SelectedMappingIndex = m_Mappings.arraySize - 1;
            }
        }

        private static void FillNameIfEmpty(SerializedProperty name, GameObject targetObject)
        {
            if (targetObject == null || string.IsNullOrWhiteSpace(name.stringValue) is false)
            {
                return;
            }

            name.stringValue = targetObject.name;
        }

        private static int CompareBindingRows(BindingRow lhs, BindingRow rhs)
        {
            var result = string.CompareOrdinal(GetHierarchySortKey(lhs.Target), GetHierarchySortKey(rhs.Target));
            if (result != 0)
            {
                return result;
            }

            result = lhs.MappingIndex.CompareTo(rhs.MappingIndex);
            return result != 0 ? result : lhs.ComponentIndex.CompareTo(rhs.ComponentIndex);
        }

        private static string GetHierarchySortKey(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return "~~~~";
            }

            var parts = new Stack<string>();
            var cursor = targetObject.transform;
            while (cursor != null)
            {
                parts.Push(cursor.GetSiblingIndex().ToString("D6", System.Globalization.CultureInfo.InvariantCulture));
                cursor = cursor.parent;
            }

            return string.Join("/", parts);
        }

        private static void DeleteArrayElement(SerializedProperty array, int index)
        {
            var previousSize = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == previousSize)
            {
                array.DeleteArrayElementAtIndex(index);
            }
        }

        private void CleanupInvalidBindings()
        {
            for (var mappingIndex = m_Mappings.arraySize - 1; mappingIndex >= 0; mappingIndex--)
            {
                var mapping = GetMappingProperty(mappingIndex);
                var targetObject = mapping.FindPropertyRelative("Target").objectReferenceValue as GameObject;
                var components = mapping.FindPropertyRelative("Components");
                RemoveComponentsNotOnTarget(components, targetObject);
                if (targetObject == null && string.IsNullOrWhiteSpace(mapping.FindPropertyRelative("Name").stringValue))
                {
                    DeleteArrayElement(m_Mappings, mappingIndex);
                }
            }

            m_LocalizationDrawer.RemoveStaleBindings();
        }

        private void ClearAllBindings()
        {
            if (EditorUtility.DisplayDialog("清空绑定", "确定清空 UIDocument 的所有绑定和本地化 Key？", "清空", "取消") is false)
            {
                return;
            }

            m_Mappings.arraySize = 0;
            m_LocalizedTexts.arraySize = 0;
            m_SelectedMappingIndex = -1;
        }

        private void ShowValidationDialog()
        {
            CleanupInvalidBindings();
            var summary = CreateBindingSummary();
            if (summary.Issues.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "验证绑定",
                    "没有发现绑定问题。\n绑定: " + summary.BindingCount + " | 组件: " + summary.ComponentCount + " | 本地化: " + summary.LocalizedTextCount,
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog("验证绑定", string.Join("\n", summary.Issues), "OK");
        }

        private BindingSummary CreateBindingSummary()
        {
            var issues = new List<string>();
            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            var componentCount = 0;
            for (var mappingIndex = 0; mappingIndex < m_Mappings.arraySize; mappingIndex++)
            {
                var mapping = GetMappingProperty(mappingIndex);
                var mappingName = mapping.FindPropertyRelative("Name").stringValue;
                var targetObject = mapping.FindPropertyRelative("Target").objectReferenceValue as GameObject;
                var components = mapping.FindPropertyRelative("Components");
                if (string.IsNullOrWhiteSpace(mappingName))
                {
                    issues.Add("Binding #" + mappingIndex + " key 为空。");
                }

                if (targetObject == null)
                {
                    issues.Add("Binding '" + GetIssueName(mappingIndex, mappingName) + "' target 缺失。");
                }
                else if (IsDocumentChild(targetObject) is false)
                {
                    issues.Add("Binding '" + GetIssueName(mappingIndex, mappingName) + "' target 不在 UIDocument 层级下。");
                }

                for (var componentIndex = 0; componentIndex < components.arraySize; componentIndex++)
                {
                    var component = components.GetArrayElementAtIndex(componentIndex).objectReferenceValue as Component;
                    if (component == null)
                    {
                        issues.Add("Binding '" + GetIssueName(mappingIndex, mappingName) + "' 有缺失组件。");
                        continue;
                    }

                    componentCount++;
                    if (targetObject != null && component.gameObject != targetObject)
                    {
                        issues.Add("Binding '" + GetIssueName(mappingIndex, mappingName) + "' 的组件不属于 target。");
                    }

                    if (string.IsNullOrWhiteSpace(mappingName) is false)
                    {
                        var fieldName = UIDocumentGenerator.CreateFieldName(mappingName, component.GetType());
                        if (fieldNames.Add(fieldName) is false)
                        {
                            issues.Add("重复生成字段: " + fieldName + "。");
                        }
                    }
                }
            }

            m_LocalizationDrawer.CollectIssues(issues);
            return new BindingSummary(m_Mappings.arraySize, componentCount, m_LocalizationDrawer.ConfiguredBindingCount, issues);
        }

        private static string GetIssueName(int index, string mappingName)
        {
            return string.IsNullOrWhiteSpace(mappingName) ? "#" + index : mappingName;
        }

        private bool CanGenerateCode()
        {
            return string.IsNullOrWhiteSpace(GetUIPath()) is false &&
                   string.IsNullOrWhiteSpace(GetClassName()) is false;
        }

        private void Generate()
        {
            try
            {
                var uiPath = GetUIPath();
                if (string.IsNullOrWhiteSpace(uiPath))
                {
                    EditorUtility.DisplayDialog("UIDocument Generate Failed", "UIDocument 必须保存为 prefab 资产后才能生成代码。", "OK");
                    return;
                }

                var className = GetClassName();
                if (string.IsNullOrWhiteSpace(className))
                {
                    EditorUtility.DisplayDialog("UIDocument Generate Failed", "Prefab 名称不能生成合法类型名。", "OK");
                    return;
                }

                var outputFolder = SelectOutputFolder();
                if (string.IsNullOrWhiteSpace(outputFolder))
                {
                    return;
                }

                CleanupInvalidBindings();
                serializedObject.ApplyModifiedProperties();
                var document = (UIDocument)target;
                UIDocumentGenerator.Generate(document, className, outputFolder, uiPath, document.Layer);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("UIDocument Generate Failed", exception.Message, "OK");
            }
        }

        private string GetUIPath()
        {
            var document = (UIDocument)target;
            var prefabStage = PrefabStageUtility.GetPrefabStage(document.gameObject);
            if (prefabStage != null && string.IsNullOrWhiteSpace(prefabStage.assetPath) is false)
            {
                return prefabStage.assetPath;
            }

            var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(document.gameObject);
            if (prefabRoot != null)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot);
                var sourcePath = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
                if (string.IsNullOrWhiteSpace(sourcePath) is false)
                {
                    return sourcePath;
                }
            }

            return AssetDatabase.GetAssetPath(document.gameObject);
        }

        private string GetClassName()
        {
            var uiPath = GetUIPath();
            var prefabName = string.IsNullOrWhiteSpace(uiPath)
                ? ((UIDocument)target).gameObject.name
                : System.IO.Path.GetFileNameWithoutExtension(uiPath);
            if (IsIdentifier(prefabName))
            {
                return prefabName;
            }

            return ToPascalIdentifier(prefabName);
        }

        private static bool IsIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || (char.IsLetter(value[0]) || value[0] == '_') is false)
            {
                return false;
            }

            for (var i = 1; i < value.Length; i++)
            {
                if ((char.IsLetterOrDigit(value[i]) || value[i] == '_') is false)
                {
                    return false;
                }
            }

            return true;
        }

        private static string ToPascalIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var upperNext = true;
            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch) is false)
                {
                    upperNext = true;
                    continue;
                }

                sb.Append(upperNext ? char.ToUpperInvariant(ch) : ch);
                upperNext = false;
            }

            if (sb.Length == 0)
            {
                return string.Empty;
            }

            if (char.IsDigit(sb[0]))
            {
                sb.Insert(0, "UI");
            }

            return sb.ToString();
        }

        private static string SelectOutputFolder()
        {
            var selected = EditorUtility.OpenFolderPanel("Select output folder", Application.dataPath, string.Empty);
            if (string.IsNullOrWhiteSpace(selected))
            {
                return string.Empty;
            }

            if (selected.StartsWith(Application.dataPath, StringComparison.Ordinal) is false)
            {
                EditorUtility.DisplayDialog("输出目录无效", "请选择 Assets 目录下的输出文件夹。", "OK");
                return string.Empty;
            }

            return "Assets" + selected.Substring(Application.dataPath.Length);
        }

        private static string CreateBindingDisplayKey(string mappingName, Component component)
        {
            if (component == null)
            {
                return string.IsNullOrWhiteSpace(mappingName) ? "(Empty)" : mappingName;
            }

            if (component.GetType().Name == "Button")
            {
                return string.IsNullOrWhiteSpace(mappingName) ? component.gameObject.name : mappingName;
            }

            return UIDocumentGenerator.CreateFieldName(
                string.IsNullOrWhiteSpace(mappingName) ? component.gameObject.name : mappingName,
                component.GetType());
        }

        private readonly struct BindingColumns
        {
            public BindingColumns(Rect value, Rect localization, Rect remove)
            {
                Value = value;
                Localization = localization;
                Remove = remove;
            }

            public Rect Value { get; }

            public Rect Localization { get; }

            public Rect Remove { get; }
        }

        private readonly struct BindingRow
        {
            public BindingRow(int mappingIndex, int componentIndex, string mappingName, GameObject target, Component component)
            {
                MappingIndex = mappingIndex;
                ComponentIndex = componentIndex;
                MappingName = mappingName;
                Target = target;
                Component = component;
            }

            public int MappingIndex { get; }

            public int ComponentIndex { get; }

            public string MappingName { get; }

            public GameObject Target { get; }

            public Component Component { get; }

            public string DisplayKey => CreateBindingDisplayKey(MappingName, Component);

            public bool IsValid => string.IsNullOrWhiteSpace(MappingName) is false &&
                                   Target != null &&
                                   Component != null &&
                                   Component.gameObject == Target;
        }

        private readonly struct BindingSummary
        {
            public BindingSummary(int bindingCount, int componentCount, int localizedTextCount, List<string> issues)
            {
                BindingCount = bindingCount;
                ComponentCount = componentCount;
                LocalizedTextCount = localizedTextCount;
                Issues = issues;
            }

            public int BindingCount { get; }

            public int ComponentCount { get; }

            public int LocalizedTextCount { get; }

            public List<string> Issues { get; }
        }

        private readonly struct ComponentMenuItem
        {
            public ComponentMenuItem(string label, Component component)
            {
                Label = label;
                Component = component;
            }

            public string Label { get; }

            public Component Component { get; }
        }
    }
}

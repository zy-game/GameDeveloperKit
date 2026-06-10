using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.UI;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GameDeveloperKit.UIEditor
{
    /// <summary>
    /// 定义 UI Document Inspector 类型。
    /// </summary>
    [CustomEditor(typeof(UIDocument))]
    public sealed class UIDocumentInspector : Editor
    {
        /// <summary>
        /// 定义 Component Column Width 常量。
        /// </summary>
        private const float ComponentColumnWidth = 150f;

        /// <summary>
        /// 存储 Full Screen Root。
        /// </summary>
        private SerializedProperty m_FullScreenRoot;
        /// <summary>
        /// 存储 Safe Area Root。
        /// </summary>
        private SerializedProperty m_SafeAreaRoot;
        /// <summary>
        /// 存储 Mappings。
        /// </summary>
        private SerializedProperty m_Mappings;
        /// <summary>
        /// 存储 Binding Tree State。
        /// </summary>
        private TreeViewState m_BindingTreeState;
        /// <summary>
        /// 存储 Binding Tree。
        /// </summary>
        private BindingTreeView m_BindingTree;
        /// <summary>
        /// 存储 Selected Object。
        /// </summary>
        private GameObject m_SelectedObject;

        /// <summary>
        /// 存储 Class Name。
        /// </summary>
        private string m_ClassName = "Example";
        /// <summary>
        /// 存储 Output Folder。
        /// </summary>
        private string m_OutputFolder = "Assets/Scripts/UI";
        /// <summary>
        /// 存储 UI Path。
        /// </summary>
        private string m_UIPath;
        /// <summary>
        /// 存储 Layer。
        /// </summary>
        private UILayer m_Layer = UILayer.Window;

        /// <summary>
        /// Unity OnEnable 回调。
        /// </summary>
        private void OnEnable()
        {
            m_FullScreenRoot = serializedObject.FindProperty("fullScreenRoot");
            m_SafeAreaRoot = serializedObject.FindProperty("safeAreaRoot");
            m_Mappings = serializedObject.FindProperty("mappings");
            var assetPath = AssetDatabase.GetAssetPath(((UIDocument)target).gameObject);
            m_UIPath = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath;

            m_BindingTreeState = new TreeViewState();
            m_BindingTree = new BindingTreeView(m_BindingTreeState);
            m_BindingTree.GameObjectSelectionChanged += gameObject => m_SelectedObject = gameObject;
            m_BindingTree.ComponentDropdownRequested += ShowComponentMenu;
            m_BindingTree.ComponentLabelRequested += GetComponentSummary;
        }

        /// <summary>
        /// Unity OnInspectorGUI 回调。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_FullScreenRoot);
            EditorGUILayout.PropertyField(m_SafeAreaRoot);

            EditorGUILayout.Space(6f);
            DrawSection("Code Generation", DrawGenerator);
            EditorGUILayout.Space(6f);
            DrawSection("Bindings", DrawBindings);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 绘制 Section。
        /// </summary>
        /// <param name="title">title 参数。</param>
        /// <param name="draw">draw 参数。</param>
        private static void DrawSection(string title, Action draw)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            draw();
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 绘制 Bindings。
        /// </summary>
        private void DrawBindings()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(GetSelectedChildObject() == null))
            {
                if (GUILayout.Button("Add Selected", EditorStyles.toolbarButton, GUILayout.Width(96)))
                {
                    EnsureMapping(GetSelectedChildObject());
                }
            }

            if (GUILayout.Button("AutoBind", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                AutoBind();
            }

            using (new EditorGUI.DisabledScope(GetMappingIndex(m_SelectedObject) < 0))
            {
                if (GUILayout.Button("Remove", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    RemoveMapping(m_SelectedObject);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Components", EditorStyles.miniLabel, GUILayout.Width(ComponentColumnWidth));
            EditorGUILayout.EndHorizontal();

            var bindingTargets = GetBindingTargets();
            if (bindingTargets.Count == 0)
            {
                EditorGUILayout.HelpBox("No bindings. Select a child GameObject or run AutoBind for b_ nodes.", MessageType.Info);
                return;
            }

            m_BindingTree.SetTargets(bindingTargets);
            var treeHeight = Mathf.Clamp(24 + bindingTargets.Count * 20, 64, 280);
            var treeRect = GUILayoutUtility.GetRect(0, treeHeight, GUILayout.ExpandWidth(true));
            m_BindingTree.OnGUI(treeRect);
        }

        /// <summary>
        /// 执行 Auto Bind。
        /// </summary>
        private void AutoBind()
        {
            var document = (UIDocument)target;
            GameObject firstAdded = null;
            for (var i = 0; i < m_Mappings.arraySize; i++)
            {
                var mapping = m_Mappings.GetArrayElementAtIndex(i);
                FillNameIfEmpty(mapping.FindPropertyRelative("Name"), mapping.FindPropertyRelative("Target").objectReferenceValue as GameObject);
            }

            foreach (var child in document.GetComponentsInChildren<Transform>(true))
            {
                var childObject = child.gameObject;
                if (childObject == document.gameObject || childObject.name.StartsWith("b_", StringComparison.Ordinal) is false)
                {
                    continue;
                }

                if (GetMappingIndex(childObject) >= 0)
                {
                    continue;
                }

                EnsureMapping(childObject);
                firstAdded ??= childObject;
            }

            if (firstAdded != null)
            {
                m_SelectedObject = firstAdded;
            }

            GUI.changed = true;
            Repaint();
        }

        /// <summary>
        /// 获取 Binding Targets。
        /// </summary>
        /// <returns>执行结果。</returns>
        private List<GameObject> GetBindingTargets()
        {
            var result = new List<GameObject>();
            var seen = new HashSet<GameObject>();
            for (var i = 0; i < m_Mappings.arraySize; i++)
            {
                var mapping = m_Mappings.GetArrayElementAtIndex(i);
                var targetObject = mapping.FindPropertyRelative("Target").objectReferenceValue as GameObject;
                if (targetObject != null && seen.Add(targetObject))
                {
                    result.Add(targetObject);
                }
            }

            result.Sort(CompareHierarchyOrder);
            return result;
        }

        /// <summary>
        /// 确保 Mapping。
        /// </summary>
        /// <param name="targetObject">target Object 参数。</param>
        /// <returns>执行结果。</returns>
        private SerializedProperty EnsureMapping(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return null;
            }

            var index = GetMappingIndex(targetObject);
            if (index < 0)
            {
                index = m_Mappings.arraySize;
                m_Mappings.InsertArrayElementAtIndex(index);
                var newMapping = m_Mappings.GetArrayElementAtIndex(index);
                newMapping.FindPropertyRelative("Name").stringValue = string.Empty;
                newMapping.FindPropertyRelative("Target").objectReferenceValue = targetObject;
                newMapping.FindPropertyRelative("Components").arraySize = 0;
            }

            var mapping = m_Mappings.GetArrayElementAtIndex(index);
            FillNameIfEmpty(mapping.FindPropertyRelative("Name"), targetObject);
            m_SelectedObject = targetObject;
            return mapping;
        }

        /// <summary>
        /// 获取 Mapping Index。
        /// </summary>
        /// <param name="targetObject">target Object 参数。</param>
        /// <returns>执行结果。</returns>
        private int GetMappingIndex(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return -1;
            }

            for (var i = 0; i < m_Mappings.arraySize; i++)
            {
                if (m_Mappings.GetArrayElementAtIndex(i).FindPropertyRelative("Target").objectReferenceValue == targetObject)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 移除 Mapping。
        /// </summary>
        /// <param name="targetObject">target Object 参数。</param>
        private void RemoveMapping(GameObject targetObject)
        {
            var index = GetMappingIndex(targetObject);
            if (index < 0)
            {
                return;
            }

            DeleteArrayElement(m_Mappings, index);
            if (m_SelectedObject == targetObject)
            {
                m_SelectedObject = null;
            }
        }

        /// <summary>
        /// 执行 Show Component Menu。
        /// </summary>
        /// <param name="anchor">anchor 参数。</param>
        /// <param name="targetObject">target Object 参数。</param>
        private void ShowComponentMenu(Rect anchor, GameObject targetObject)
        {
            var availableComponents = targetObject.GetComponents<Component>().Where(component => component != null).ToArray();
            var selectedComponents = GetSelectedComponents(targetObject);
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Nothing"), selectedComponents.Count == 0, () => SetComponents(targetObject, Array.Empty<Component>()));
            if (availableComponents.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("Everything"));
            }
            else
            {
                menu.AddItem(new GUIContent("Everything"), IsEverythingSelected(availableComponents, selectedComponents), () => SetComponents(targetObject, availableComponents));
            }

            menu.AddSeparator(string.Empty);
            foreach (var item in CreateComponentMenuItems(availableComponents))
            {
                var component = item.Component;
                menu.AddItem(new GUIContent(item.Label), selectedComponents.Contains(component), () => ToggleComponent(targetObject, component));
            }

            menu.DropDown(anchor);
        }

        /// <summary>
        /// 获取 Component Summary。
        /// </summary>
        /// <param name="targetObject">target Object 参数。</param>
        /// <returns>执行结果。</returns>
        private string GetComponentSummary(GameObject targetObject)
        {
            var availableComponents = targetObject.GetComponents<Component>().Where(component => component != null).ToArray();
            var selectedComponents = GetSelectedComponents(targetObject);
            if (selectedComponents.Count == 0)
            {
                return "Nothing";
            }

            if (IsEverythingSelected(availableComponents, selectedComponents))
            {
                return "Everything";
            }

            var names = selectedComponents
                .Where(component => component != null)
                .Select(component => component.GetType().Name)
                .Distinct()
                .ToArray();

            if (names.Length <= 2)
            {
                return string.Join(", ", names);
            }

            return names.Length + " Components";
        }

        /// <summary>
        /// 获取 Selected Components。
        /// </summary>
        /// <param name="targetObject">target Object 参数。</param>
        /// <returns>执行结果。</returns>
        private List<Component> GetSelectedComponents(GameObject targetObject)
        {
            var mappingIndex = GetMappingIndex(targetObject);
            if (mappingIndex < 0)
            {
                return new List<Component>();
            }

            var components = m_Mappings.GetArrayElementAtIndex(mappingIndex).FindPropertyRelative("Components");
            var result = new List<Component>();
            for (var i = 0; i < components.arraySize; i++)
            {
                if (components.GetArrayElementAtIndex(i).objectReferenceValue is Component component)
                {
                    result.Add(component);
                }
            }

            return result;
        }

        /// <summary>
        /// 执行 Toggle Component。
        /// </summary>
        /// <param name="targetObject">target Object 参数。</param>
        /// <param name="component">component 参数。</param>
        private void ToggleComponent(GameObject targetObject, Component component)
        {
            serializedObject.Update();
            var mapping = EnsureMapping(targetObject);
            var components = mapping.FindPropertyRelative("Components");
            for (var i = components.arraySize - 1; i >= 0; i--)
            {
                if (components.GetArrayElementAtIndex(i).objectReferenceValue == component)
                {
                    DeleteArrayElement(components, i);
                    serializedObject.ApplyModifiedProperties();
                    GUI.changed = true;
                    Repaint();
                    return;
                }
            }

            AddComponent(components, component);
            serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
            Repaint();
        }

        /// <summary>
        /// 设置 Components。
        /// </summary>
        /// <param name="targetObject">target Object 参数。</param>
        /// <param name="selectedComponents">selected Components 参数。</param>
        private void SetComponents(GameObject targetObject, Component[] selectedComponents)
        {
            serializedObject.Update();
            var mapping = EnsureMapping(targetObject);
            var components = mapping.FindPropertyRelative("Components");
            components.arraySize = 0;
            foreach (var component in selectedComponents)
            {
                AddComponent(components, component);
            }

            serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
            Repaint();
        }

        /// <summary>
        /// 添加 Component。
        /// </summary>
        /// <param name="components">components 参数。</param>
        /// <param name="component">component 参数。</param>
        private static void AddComponent(SerializedProperty components, Component component)
        {
            components.InsertArrayElementAtIndex(components.arraySize);
            components.GetArrayElementAtIndex(components.arraySize - 1).objectReferenceValue = component;
        }

        /// <summary>
        /// 执行 Is Everything Selected。
        /// </summary>
        /// <param name="availableComponents">available Components 参数。</param>
        /// <param name="selectedComponents">selected Components 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        private static bool IsEverythingSelected(Component[] availableComponents, IReadOnlyCollection<Component> selectedComponents)
        {
            return availableComponents.Length > 0 &&
                   selectedComponents.Count == availableComponents.Length &&
                   availableComponents.All(selectedComponents.Contains);
        }

        /// <summary>
        /// 创建 Component Menu Items。
        /// </summary>
        /// <param name="components">components 参数。</param>
        /// <returns>执行结果。</returns>
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
                var label = counts[name] <= 1 ? name : name + " " + seen[name];
                yield return new ComponentMenuItem(label, component);
            }
        }

        /// <summary>
        /// 获取 Selected Child Object。
        /// </summary>
        /// <returns>执行结果。</returns>
        private GameObject GetSelectedChildObject()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                return null;
            }

            var document = (UIDocument)target;
            if (selected == document.gameObject)
            {
                return null;
            }

            return selected.transform.IsChildOf(document.transform) ? selected : null;
        }

        /// <summary>
        /// 执行 Fill Name If Empty。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <param name="targetObject">target Object 参数。</param>
        private static void FillNameIfEmpty(SerializedProperty name, GameObject targetObject)
        {
            if (targetObject == null || string.IsNullOrWhiteSpace(name.stringValue) is false)
            {
                return;
            }

            name.stringValue = targetObject.name;
        }

        /// <summary>
        /// 执行 Compare Hierarchy Order。
        /// </summary>
        /// <param name="lhs">lhs 参数。</param>
        /// <param name="rhs">rhs 参数。</param>
        /// <returns>执行结果。</returns>
        private static int CompareHierarchyOrder(GameObject lhs, GameObject rhs)
        {
            return string.CompareOrdinal(GetHierarchySortKey(lhs.transform), GetHierarchySortKey(rhs.transform));
        }

        /// <summary>
        /// 获取 Hierarchy Sort Key。
        /// </summary>
        /// <param name="transform">transform 参数。</param>
        /// <returns>执行结果。</returns>
        private static string GetHierarchySortKey(Transform transform)
        {
            var parts = new Stack<string>();
            var cursor = transform;
            while (cursor != null)
            {
                parts.Push(cursor.GetSiblingIndex().ToString("D6", System.Globalization.CultureInfo.InvariantCulture));
                cursor = cursor.parent;
            }

            return string.Join("/", parts);
        }

        /// <summary>
        /// 执行 Delete Array Element。
        /// </summary>
        /// <param name="array">array 参数。</param>
        /// <param name="index">index 参数。</param>
        private static void DeleteArrayElement(SerializedProperty array, int index)
        {
            var previousSize = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == previousSize)
            {
                array.DeleteArrayElementAtIndex(index);
            }
        }

        /// <summary>
        /// 绘制 Generator。
        /// </summary>
        private void DrawGenerator()
        {
            m_ClassName = EditorGUILayout.TextField("Class Name", m_ClassName);
            m_OutputFolder = EditorGUILayout.TextField("Output Folder", m_OutputFolder);
            m_UIPath = EditorGUILayout.TextField("UI Path", m_UIPath);
            m_Layer = (UILayer)EditorGUILayout.EnumPopup("Layer", m_Layer);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Folder"))
            {
                var selected = EditorUtility.OpenFolderPanel("Select output folder", Application.dataPath, string.Empty);
                if (string.IsNullOrWhiteSpace(selected) is false && selected.StartsWith(Application.dataPath, StringComparison.Ordinal))
                {
                    m_OutputFolder = "Assets" + selected.Substring(Application.dataPath.Length);
                }
            }

            if (GUILayout.Button("Generate Code"))
            {
                Generate();
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 执行 Generate。
        /// </summary>
        private void Generate()
        {
            try
            {
                UIDocumentGenerator.Generate((UIDocument)target, m_ClassName, m_OutputFolder, m_UIPath, m_Layer);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("UIDocument Generate Failed", exception.Message, "OK");
            }
        }

        /// <summary>
        /// 定义 Component Menu Item 结构。
        /// </summary>
        private readonly struct ComponentMenuItem
        {
            /// <summary>
            /// 初始化 Component Menu Item。
            /// </summary>
            /// <param name="label">label 参数。</param>
            /// <param name="component">component 参数。</param>
            public ComponentMenuItem(string label, Component component)
            {
                Label = label;
                Component = component;
            }

            public string Label { get; }

            public Component Component { get; }
        }

        /// <summary>
        /// 定义 Binding Tree View 类型。
        /// </summary>
        private sealed class BindingTreeView : TreeView
        {
            /// <summary>             /// 存储 Targets。             /// </summary>
            private List<GameObject> m_Targets = new List<GameObject>();

            /// <summary>
            /// 初始化 Binding Tree View。
            /// </summary>
            /// <param name="state">state 参数。</param>
            public BindingTreeView(TreeViewState state) : base(state)
            {
                showBorder = true;
                showAlternatingRowBackgrounds = false;
                rowHeight = 20f;
                Reload();
            }

            /// <summary>
            /// 定义 Game Object Selection Changed 事件。
            /// </summary>
            public event Action<GameObject> GameObjectSelectionChanged;

            /// <summary>
            /// 定义 Component Dropdown Requested 事件。
            /// </summary>
            public event Action<Rect, GameObject> ComponentDropdownRequested;

            /// <summary>
            /// 定义 Component Label Requested 事件。
            /// </summary>
            public event Func<GameObject, string> ComponentLabelRequested;

            /// <summary>
            /// 设置 Targets。
            /// </summary>
            /// <param name="targets">targets 参数。</param>
            public void SetTargets(List<GameObject> targets)
            {
                m_Targets = targets ?? new List<GameObject>();
                Reload();
            }

            /// <summary>
            /// 构建 Root。
            /// </summary>
            /// <returns>执行结果。</returns>
            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
                var itemLookup = new Dictionary<GameObject, BindingTreeItem>();
                foreach (var target in m_Targets)
                {
                    itemLookup[target] = new BindingTreeItem(target.GetInstanceID(), 0, target.name, target);
                }

                foreach (var target in m_Targets)
                {
                    var item = itemLookup[target];
                    var parent = FindNearestBoundParent(target.transform.parent, itemLookup);
                    if (parent == null)
                    {
                        root.AddChild(item);
                    }
                    else
                    {
                        parent.AddChild(item);
                    }
                }

                SetupDepthsFromParentsAndChildren(root);
                return root;
            }

            /// <summary>
            /// 执行 Row GUI。
            /// </summary>
            /// <param name="args">args 参数。</param>
            protected override void RowGUI(RowGUIArgs args)
            {
                var item = (BindingTreeItem)args.item;
                var rowRect = args.rowRect;
                var componentRect = new Rect(rowRect.xMax - ComponentColumnWidth, rowRect.y + 1f, ComponentColumnWidth - 4f, rowRect.height - 2f);
                args.rowRect = new Rect(rowRect.x, rowRect.y, Mathf.Max(20f, rowRect.width - ComponentColumnWidth - 8f), rowRect.height);
                base.RowGUI(args);

                if (GUI.Button(componentRect, ComponentLabelRequested?.Invoke(item.Target) ?? "Nothing", EditorStyles.popup))
                {
                    ComponentDropdownRequested?.Invoke(componentRect, item.Target);
                }
            }

            /// <summary>
            /// 执行 Selection Changed。
            /// </summary>
            /// <param name="selectedIds">selected Ids 参数。</param>
            protected override void SelectionChanged(IList<int> selectedIds)
            {
                if (selectedIds == null || selectedIds.Count == 0)
                {
                    GameObjectSelectionChanged?.Invoke(null);
                    return;
                }

                var selected = FindItem(selectedIds[0], rootItem) as BindingTreeItem;
                GameObjectSelectionChanged?.Invoke(selected?.Target);
            }

            /// <summary>
            /// 查找 Nearest Bound Parent。
            /// </summary>
            /// <param name="transform">transform 参数。</param>
            /// <param name="itemLookup">item Lookup 参数。</param>
            /// <returns>执行结果。</returns>
            private static BindingTreeItem FindNearestBoundParent(Transform transform, Dictionary<GameObject, BindingTreeItem> itemLookup)
            {
                var cursor = transform;
                while (cursor != null)
                {
                    if (itemLookup.TryGetValue(cursor.gameObject, out var item))
                    {
                        return item;
                    }

                    cursor = cursor.parent;
                }

                return null;
            }
        }

        /// <summary>
        /// 定义 Binding Tree Item 类型。
        /// </summary>
        private sealed class BindingTreeItem : TreeViewItem
        {
            /// <summary>
            /// 初始化 Binding Tree Item。
            /// </summary>
            /// <param name="id">id 参数。</param>
            /// <param name="depth">depth 参数。</param>
            /// <param name="displayName">display Name 参数。</param>
            /// <param name="displayName">display Name 参数。</param>
            public BindingTreeItem(int id, int depth, string displayName, GameObject target) : base(id, depth, displayName)
            {
                Target = target;
            }

            public GameObject Target { get; }
        }
    }
}

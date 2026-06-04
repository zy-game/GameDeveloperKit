using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.UI;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GameDeveloperKit.UIEditor
{
    [CustomEditor(typeof(UIDocument))]
    public sealed class UIDocumentInspector : Editor
    {
        private const float ComponentColumnWidth = 150f;

        private SerializedProperty m_FullScreenRoot;
        private SerializedProperty m_SafeAreaRoot;
        private SerializedProperty m_Mappings;
        private TreeViewState m_BindingTreeState;
        private BindingTreeView m_BindingTree;
        private GameObject m_SelectedObject;

        private string m_ClassName = "Example";
        private string m_OutputFolder = "Assets/Scripts/UI";
        private string m_UIPath;
        private UILayer m_Layer = UILayer.Window;

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

        private static void DrawSection(string title, Action draw)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            draw();
            EditorGUI.indentLevel--;
        }

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

        private static void AddComponent(SerializedProperty components, Component component)
        {
            components.InsertArrayElementAtIndex(components.arraySize);
            components.GetArrayElementAtIndex(components.arraySize - 1).objectReferenceValue = component;
        }

        private static bool IsEverythingSelected(Component[] availableComponents, IReadOnlyCollection<Component> selectedComponents)
        {
            return availableComponents.Length > 0 &&
                   selectedComponents.Count == availableComponents.Length &&
                   availableComponents.All(selectedComponents.Contains);
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
                var label = counts[name] <= 1 ? name : name + " " + seen[name];
                yield return new ComponentMenuItem(label, component);
            }
        }

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

        private static void FillNameIfEmpty(SerializedProperty name, GameObject targetObject)
        {
            if (targetObject == null || string.IsNullOrWhiteSpace(name.stringValue) is false)
            {
                return;
            }

            name.stringValue = targetObject.name;
        }

        private static int CompareHierarchyOrder(GameObject lhs, GameObject rhs)
        {
            return string.CompareOrdinal(GetHierarchySortKey(lhs.transform), GetHierarchySortKey(rhs.transform));
        }

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

        private static void DeleteArrayElement(SerializedProperty array, int index)
        {
            var previousSize = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == previousSize)
            {
                array.DeleteArrayElementAtIndex(index);
            }
        }

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

        private sealed class BindingTreeView : TreeView
        {
            private List<GameObject> m_Targets = new List<GameObject>();

            public BindingTreeView(TreeViewState state) : base(state)
            {
                showBorder = true;
                showAlternatingRowBackgrounds = false;
                rowHeight = 20f;
                Reload();
            }

            public event Action<GameObject> GameObjectSelectionChanged;

            public event Action<Rect, GameObject> ComponentDropdownRequested;

            public event Func<GameObject, string> ComponentLabelRequested;

            public void SetTargets(List<GameObject> targets)
            {
                m_Targets = targets ?? new List<GameObject>();
                Reload();
            }

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

        private sealed class BindingTreeItem : TreeViewItem
        {
            public BindingTreeItem(int id, int depth, string displayName, GameObject target) : base(id, depth, displayName)
            {
                Target = target;
            }

            public GameObject Target { get; }
        }
    }
}

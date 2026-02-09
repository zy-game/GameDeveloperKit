using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    /// <summary>
    /// 组件过滤扩展 - 在 GameObject Inspector 中绘制过滤面板
    /// 使用UIToolkit，通过delayCall延迟插入到正确位置
    /// </summary>
    [InitializeOnLoad]
    public static class ComponentFilterPanel
    {
        private const string PANEL_NAME = "component-filter-panel";
        
        private static int _currentFilterIndex = -1;
        private static GameObject _lastSelectedObject;
        private static int _lastComponentCount = 0;
        private static List<ComponentInfo> _componentList = new List<ComponentInfo>();
        private static Dictionary<Component, HideFlags> _originalHideFlags = new Dictionary<Component, HideFlags>();
        private static bool _pendingInsert = false;

        private class ComponentInfo
        {
            public string displayName;
            public Component component;
            public Type componentType;
        }

        private static StyleSheet _styleSheet;

        static ComponentFilterPanel()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.delayCall += TryInsertPanel;
            
            // 加载项目通用USS样式表
            _styleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");
        }

        private static void OnHierarchyChanged()
        {
            ScheduleInsert();
        }

        private static void OnSelectionChanged()
        {
            RestoreAllHideFlags();
            _currentFilterIndex = -1;
            _lastSelectedObject = null;
            ScheduleInsert();
        }

        private static void ScheduleInsert()
        {
            if (_pendingInsert) return;
            _pendingInsert = true;
            EditorApplication.delayCall += TryInsertPanel;
        }

        private static void TryInsertPanel()
        {
            _pendingInsert = false;
            
            var go = Selection.activeGameObject;
            if (go == null) return;
            
            // 更新组件列表
            if (_lastSelectedObject != go)
            {
                RestoreAllHideFlags();
                _lastSelectedObject = go;
                _lastComponentCount = go.GetComponents<Component>().Length;
                _currentFilterIndex = -1;
                AnalyzeComponents(go);
            }
            else
            {
                int currentCount = go.GetComponents<Component>().Length;
                if (currentCount != _lastComponentCount)
                {
                    RestoreAllHideFlags();
                    _lastComponentCount = currentCount;
                    _currentFilterIndex = -1;
                    AnalyzeComponents(go);
                }
            }
            
            // 查找Inspector窗口并插入面板
            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var windows = Resources.FindObjectsOfTypeAll(inspectorType);
            
            foreach (var window in windows)
            {
                var editorWindow = window as EditorWindow;
                if (editorWindow == null) continue;
                
                var root = editorWindow.rootVisualElement;
                if (root == null) continue;
                
                // 移除旧面板
                var oldPanel = root.Q<VisualElement>(PANEL_NAME);
                oldPanel?.RemoveFromHierarchy();
                
                // 尝试多种方式查找编辑器列表
                VisualElement editorsList = root.Q<VisualElement>("unity-inspector-editors-list");
                if (editorsList == null)
                {
                    editorsList = root.Q<VisualElement>(className: "unity-inspector-editors-list");
                }
                if (editorsList == null)
                {
                    // 遍历查找包含EditorElement的容器
                    editorsList = FindEditorsListContainer(root);
                }
                
                if (editorsList == null) continue;
                
                // 找到第二个EditorElement（第一个是GameObject header，第二个是Transform/RectTransform）
                int insertIndex = 0;
                int editorElementCount = 0;
                for (int i = 0; i < editorsList.childCount; i++)
                {
                    var child = editorsList[i];
                    var typeName = child.GetType().Name;
                    if (typeName == "EditorElement")
                    {
                        editorElementCount++;
                        if (editorElementCount == 2) // 第二个EditorElement
                        {
                            insertIndex = i;
                            break;
                        }
                    }
                }
                
                var panel = CreatePanel();
                editorsList.Insert(insertIndex, panel);
            }
        }

        private static VisualElement FindEditorsListContainer(VisualElement root)
        {
            // 递归查找包含EditorElement的容器
            foreach (var child in root.Children())
            {
                if (child.GetType().Name == "EditorElement")
                {
                    return root;
                }
                
                var result = FindEditorsListContainer(child);
                if (result != null) return result;
            }
            return null;
        }

        private static VisualElement CreatePanel()
        {
            var panel = new VisualElement();
            panel.name = PANEL_NAME;
            
            // 应用样式表
            if (_styleSheet != null)
            {
                panel.styleSheets.Add(_styleSheet);
            }
            
            // 使用info-card样式
            panel.AddToClassList("info-card");
            panel.style.marginTop = 4;
            panel.style.marginLeft = 16;
            panel.style.marginRight = 4;

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;

            var title = new Label("Component Filter");
            title.AddToClassList("card-title");
            title.style.marginBottom = 0;
            header.Add(title);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            string statusText = _currentFilterIndex < 0 ? "[All]" : $"[{_componentList[_currentFilterIndex].displayName}]";
            var statusLabel = new Label(statusText);
            statusLabel.style.color = _currentFilterIndex < 0 ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.4f, 0.7f, 1f);
            header.Add(statusLabel);

            panel.Add(header);

            // Buttons
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.flexWrap = Wrap.Wrap;

            // All button
            var allBtn = CreateButton("All", _currentFilterIndex < 0);
            allBtn.clicked += () => SelectFilter(-1);
            buttonsContainer.Add(allBtn);

            // Component buttons
            for (int i = 0; i < _componentList.Count; i++)
            {
                int index = i;
                var btn = CreateButton(_componentList[i].displayName, _currentFilterIndex == i);
                btn.clicked += () => SelectFilter(index);
                buttonsContainer.Add(btn);
            }

            panel.Add(buttonsContainer);
            return panel;
        }

        private static Button CreateButton(string text, bool isSelected)
        {
            var btn = new Button { text = text };
            btn.AddToClassList("btn");
            btn.AddToClassList("btn-sm");
            
            if (isSelected)
            {
                btn.AddToClassList("btn-primary");
            }
            else
            {
                btn.AddToClassList("btn-secondary");
            }
            
            btn.style.marginRight = 4;
            btn.style.marginBottom = 4;
            
            return btn;
        }

        private static void SelectFilter(int index)
        {
            if (_currentFilterIndex == index) return;
            _currentFilterIndex = index;
            ApplyFilter();
            ScheduleInsert(); // 刷新面板
        }

        private static void AnalyzeComponents(GameObject go)
        {
            _componentList.Clear();
            _originalHideFlags.Clear();
            if (go == null) return;

            var components = go.GetComponents<Component>();
            var nameCount = new Dictionary<string, int>();

            foreach (var component in components)
            {
                if (component == null) continue;
                var type = component.GetType();
                _originalHideFlags[component] = component.hideFlags;

                string baseName = type.Name;
                if (!nameCount.ContainsKey(baseName)) nameCount[baseName] = 0;
                nameCount[baseName]++;

                _componentList.Add(new ComponentInfo
                {
                    displayName = nameCount[baseName] > 1 ? $"{baseName} ({nameCount[baseName]})" : baseName,
                    component = component,
                    componentType = type
                });
            }
        }

        private static void ApplyFilter()
        {
            if (_lastSelectedObject == null) return;

            foreach (var info in _componentList)
            {
                if (info.component == null) continue;
                bool shouldShow = _currentFilterIndex < 0 ||
                    _componentList[_currentFilterIndex].component == info.component;

                if (shouldShow && _originalHideFlags.TryGetValue(info.component, out var original))
                    info.component.hideFlags = original;
                else if (!shouldShow)
                    info.component.hideFlags |= HideFlags.HideInInspector;
            }

            EditorUtility.SetDirty(_lastSelectedObject);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void RestoreAllHideFlags()
        {
            foreach (var kvp in _originalHideFlags)
                if (kvp.Key != null) kvp.Key.hideFlags = kvp.Value;
            _originalHideFlags.Clear();
            _componentList.Clear();
        }
    }
}

using System.Collections.Generic;
using GameDeveloperKit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.SceneTools
{
    /// <summary>
    /// Scene视图底部导航栏，使用UIToolkit实现
    /// 显示当前选中物体的层级路径
    /// </summary>
    public class SceneNavigationBar
    {
        private VisualElement _root;
        private VisualElement _pathContainer;
        private Button _lockButton;
        
        private StyleSheet _styleSheet;
        private bool _isInitialized;
        
        // 锁定状态
        private static bool _isLocked;
        private static GameObject _lockedObject;
        
        public static bool IsLocked => _isLocked;
        public static GameObject LockedObject => _lockedObject;
        
        public VisualElement Root => _root;
        
        public void Initialize()
        {
            if (_isInitialized) return;
            
            LoadStyleSheet();
            CreateUI();
            
            Selection.selectionChanged += OnSelectionChanged;
            _isInitialized = true;
            
            RefreshPath();
        }
        
        public void Dispose()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            _root?.RemoveFromHierarchy();
            _isInitialized = false;
        }
        
        private void LoadStyleSheet()
        {
            _styleSheet = EditorAssetLoader.LoadStyleSheet("SceneTools/SceneTools.uss");
        }
        
        private void CreateUI()
        {
            _root = new VisualElement();
            _root.name = "scene-navigation-bar";
            _root.AddToClassList("scene-navigation-bar");
            
            if (_styleSheet != null)
            {
                _root.styleSheets.Add(_styleSheet);
            }
            
            // 路径容器
            _pathContainer = new VisualElement();
            _pathContainer.style.flexDirection = FlexDirection.Row;
            _pathContainer.style.alignItems = Align.Center;
            _pathContainer.style.flexGrow = 1;
            _root.Add(_pathContainer);
            
            // 锁定按钮
            _lockButton = new Button(OnLockButtonClicked);
            _lockButton.AddToClassList("scene-nav-lock-button");
            _lockButton.text = "🔓";
            _lockButton.tooltip = "锁定选择";
            _root.Add(_lockButton);
        }
        
        private void OnSelectionChanged()
        {
            HandleSelectionLock();
            RefreshPath();
        }
        
        private void HandleSelectionLock()
        {
            if (!_isLocked || _lockedObject == null)
                return;
            
            if (Selection.activeGameObject != _lockedObject)
            {
                Selection.activeGameObject = _lockedObject;
            }
        }
        
        private void OnLockButtonClicked()
        {
            _isLocked = !_isLocked;
            
            if (_isLocked)
            {
                _lockedObject = Selection.activeGameObject;
                _lockButton.text = "🔒";
                _lockButton.tooltip = "解锁选择 (当前已锁定)";
                _lockButton.AddToClassList("scene-nav-lock-button--locked");
            }
            else
            {
                _lockedObject = null;
                _lockButton.text = "🔓";
                _lockButton.tooltip = "锁定选择";
                _lockButton.RemoveFromClassList("scene-nav-lock-button--locked");
            }
        }
        
        public void RefreshPath()
        {
            if (_pathContainer == null) return;
            
            _pathContainer.Clear();
            
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var isInPrefabMode = prefabStage != null;
            
            // 更新Prefab模式样式
            _root.EnableInClassList("scene-navigation-bar--prefab-mode", isInPrefabMode);
            
            var selectedGO = Selection.activeGameObject;
            var path = selectedGO != null ? GetHierarchyPath(selectedGO) : new List<PathNode>();
            
            // 添加根节点按钮
            if (isInPrefabMode)
            {
                AddPrefabRootButton(prefabStage);
            }
            else
            {
                AddSceneRootButton();
            }
            
            // 添加路径
            if (path.Count > 0)
            {
                AddSeparator();
                
                for (int i = 0; i < path.Count; i++)
                {
                    var node = path[i];
                    var isLast = i == path.Count - 1;
                    
                    AddPathNodeButton(node, isLast);
                    
                    if (!isLast)
                    {
                        AddSeparator();
                    }
                }
            }
        }
        
        private void AddPrefabRootButton(PrefabStage prefabStage)
        {
            var prefabRoot = prefabStage.prefabContentsRoot;
            var prefabName = prefabRoot != null ? prefabRoot.name : "Prefab";
            var hasChildren = prefabRoot != null && prefabRoot.transform.childCount > 0;
            var displayName = hasChildren ? "⬡ " + prefabName + " ▾" : "⬡ " + prefabName;
            
            var button = new Button(() => ShowPrefabRootMenu(prefabRoot));
            button.AddToClassList("scene-nav-button");
            button.AddToClassList("scene-nav-button--root");
            button.text = displayName;
            _pathContainer.Add(button);
        }
        
        private void AddSceneRootButton()
        {
            var activeScene = SceneManager.GetActiveScene();
            var hasRootObjects = activeScene.rootCount > 0;
            var displayName = hasRootObjects ? activeScene.name + " ▾" : activeScene.name;
            
            var button = new Button(() => ShowSceneRootObjects(activeScene));
            button.AddToClassList("scene-nav-button");
            button.AddToClassList("scene-nav-button--root");
            button.text = displayName;
            _pathContainer.Add(button);
        }
        
        private void AddSeparator()
        {
            var separator = new Label("/");
            separator.AddToClassList("scene-nav-separator");
            _pathContainer.Add(separator);
        }
        
        private void AddPathNodeButton(PathNode node, bool isSelected)
        {
            var hasChildren = node.GameObject.transform.childCount > 0;
            var displayName = hasChildren ? node.Name + " ▾" : node.Name;
            
            var button = new Button();
            button.AddToClassList("scene-nav-button");
            button.text = displayName;
            
            if (isSelected)
            {
                button.AddToClassList("scene-nav-button--selected");
                if (hasChildren)
                {
                    button.clicked += () => ShowChildrenMenu(node.GameObject);
                }
            }
            else
            {
                button.clicked += () => SetSelection(node.GameObject);
                
                // 右键菜单
                button.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button == 1 && hasChildren)
                    {
                        ShowChildrenMenu(node.GameObject);
                        evt.StopPropagation();
                    }
                });
            }
            
            _pathContainer.Add(button);
        }
        
        private void SetSelection(GameObject go)
        {
            if (_isLocked)
            {
                _lockedObject = go;
            }
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
        
        private void ShowPrefabRootMenu(GameObject prefabRoot)
        {
            var menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("选择 " + prefabRoot.name), false, () => SetSelection(prefabRoot));
            
            if (prefabRoot.transform.childCount > 0)
            {
                menu.AddSeparator("");
                AddChildrenToMenu(menu, prefabRoot);
            }
            
            menu.ShowAsContext();
        }
        
        private void ShowSceneRootObjects(Scene scene)
        {
            var menu = new GenericMenu();
            var rootObjects = scene.GetRootGameObjects();
            
            foreach (var root in rootObjects)
            {
                var go = root;
                var hasChildren = go.transform.childCount > 0;
                var displayName = hasChildren ? go.name + "  ▶" : go.name;
                menu.AddItem(new GUIContent(displayName), false, () => SetSelection(go));
            }
            
            if (rootObjects.Length == 0)
                menu.AddDisabledItem(new GUIContent("场景为空"));
            
            menu.ShowAsContext();
        }
        
        private void ShowChildrenMenu(GameObject parent)
        {
            var menu = new GenericMenu();
            AddChildrenToMenu(menu, parent);
            
            if (parent.transform.childCount == 0)
                menu.AddDisabledItem(new GUIContent("无子物体"));
            
            menu.ShowAsContext();
        }
        
        private void AddChildrenToMenu(GenericMenu menu, GameObject parent)
        {
            var transform = parent.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                var hasGrandChildren = child.transform.childCount > 0;
                var displayName = hasGrandChildren ? child.name + "  ▶" : child.name;
                menu.AddItem(new GUIContent(displayName), false, () => SetSelection(child));
            }
        }
        
        private List<PathNode> GetHierarchyPath(GameObject go)
        {
            var path = new List<PathNode>();
            var current = go.transform;
            
            while (current != null)
            {
                path.Insert(0, new PathNode { Name = current.name, GameObject = current.gameObject });
                current = current.parent;
            }
            
            return path;
        }
        
        private struct PathNode
        {
            public string Name;
            public GameObject GameObject;
        }
    }
}

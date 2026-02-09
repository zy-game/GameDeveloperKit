using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.SceneTools
{
    /// <summary>
    /// Scene工具管理器，负责初始化和协调各个子系统
    /// 使用UIToolkit实现工具栏和导航栏
    /// </summary>
    [InitializeOnLoad]
    public static class SceneToolsManager
    {
        private static SceneContextMenu _contextMenu;
        private static SceneToolbar _toolbar;
        private static SceneNavigationBar _navigationBar;
        private static SceneGuidelines _guidelines;
        private static SelectionHistory _selectionHistory;
        
        // 跟踪已初始化UIToolkit的SceneView
        private static Dictionary<SceneView, bool> _initializedViews = new Dictionary<SceneView, bool>();
        
        static SceneToolsManager()
        {
            Initialize();
        }
        
        private static void Initialize()
        {
            _contextMenu = new SceneContextMenu();
            _toolbar = new SceneToolbar();
            _navigationBar = new SceneNavigationBar();
            _guidelines = new SceneGuidelines();
            _selectionHistory = new SelectionHistory();
            
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            
            // 初始化UIToolkit组件
            _toolbar.Initialize();
            _navigationBar.Initialize();
        }
        
        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!SceneToolsSettings.Enabled)
            {
                RemoveUIToolkitFromSceneView(sceneView);
                return;
            }
            
            // 确保UIToolkit元素已添加到SceneView
            EnsureUIToolkitInitialized(sceneView);
            
            // 处理选择历史快捷键
            _selectionHistory.HandleKeyboardEvent(Event.current);
            
            // 处理右键菜单
            _contextMenu.OnSceneGUI(sceneView);
            
            // 绘制辅助线
            _guidelines.OnSceneGUI(sceneView);
            
            // 更新UIToolkit组件状态
            _toolbar.UpdateButtonStates();
        }
        
        private static void EnsureUIToolkitInitialized(SceneView sceneView)
        {
            if (_initializedViews.TryGetValue(sceneView, out bool initialized) && initialized)
                return;
            
            var root = sceneView.rootVisualElement;
            if (root == null) return;
            
            // 添加工具栏
            if (_toolbar.Root != null && _toolbar.Root.parent == null)
            {
                root.Add(_toolbar.Root);
            }
            
            // 添加导航栏
            if (_navigationBar.Root != null && _navigationBar.Root.parent == null)
            {
                root.Add(_navigationBar.Root);
            }
            
            _initializedViews[sceneView] = true;
        }
        
        private static void RemoveUIToolkitFromSceneView(SceneView sceneView)
        {
            if (!_initializedViews.TryGetValue(sceneView, out bool initialized) || !initialized)
                return;
            
            _toolbar.Root?.RemoveFromHierarchy();
            _navigationBar.Root?.RemoveFromHierarchy();
            
            _initializedViews[sceneView] = false;
        }
        
        [MenuItem("GameDeveloperKit/Scene Tools/Enable", false, 100)]
        private static void ToggleEnable()
        {
            SceneToolsSettings.Enabled = !SceneToolsSettings.Enabled;
            SceneView.RepaintAll();
        }
        
        [MenuItem("GameDeveloperKit/Scene Tools/Enable", true)]
        private static bool ToggleEnableValidate()
        {
            Menu.SetChecked("GameDeveloperKit/Scene Tools/Enable", SceneToolsSettings.Enabled);
            return true;
        }
        
        [MenuItem("GameDeveloperKit/Scene Tools/Reset Settings", false, 101)]
        private static void ResetSettings()
        {
            SceneToolsSettings.ResetToDefaults();
            SceneView.RepaintAll();
        }
    }
}

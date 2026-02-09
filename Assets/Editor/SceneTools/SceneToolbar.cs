using GameDeveloperKit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.SceneTools
{
    /// <summary>
    /// Scene视图右侧工具栏，使用UIToolkit实现
    /// 提供辅助线相关控制和对齐工具
    /// </summary>
    public class SceneToolbar
    {
        private VisualElement _root;
        private Button _snapButton;
        private Button _guidelinesButton;
        private Button _alignLeftButton;
        private Button _alignCenterButton;
        private Button _alignRightButton;
        
        private StyleSheet _styleSheet;
        private bool _isInitialized;
        
        public VisualElement Root => _root;
        
        public void Initialize()
        {
            if (_isInitialized) return;
            
            LoadStyleSheet();
            CreateUI();
            UpdateButtonStates();
            
            Selection.selectionChanged += OnSelectionChanged;
            _isInitialized = true;
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
            _root.name = "scene-toolbar";
            _root.AddToClassList("scene-toolbar");
            
            if (_styleSheet != null)
            {
                _root.styleSheets.Add(_styleSheet);
            }
            
            // 吸附按钮
            _snapButton = CreateToolbarButton("Grid.MoveTool", "吸附到辅助线 (Snap)");
            _snapButton.clicked += OnSnapButtonClicked;
            _root.Add(_snapButton);
            
            // 辅助线按钮
            _guidelinesButton = CreateToolbarButton("GridAxisY", "显示辅助线 (Guidelines)");
            _guidelinesButton.clicked += OnGuidelinesButtonClicked;
            _root.Add(_guidelinesButton);
            
            // 分隔线
            var separator = new VisualElement();
            separator.AddToClassList("scene-toolbar-separator");
            _root.Add(separator);
            
            // 左对齐按钮
            _alignLeftButton = CreateToolbarButton("align_horizontally_left", "左对齐 (Align Left)");
            _alignLeftButton.clicked += OnAlignLeftClicked;
            _root.Add(_alignLeftButton);
            
            // 居中对齐按钮
            _alignCenterButton = CreateToolbarButton("align_horizontally_center", "居中对齐 (Align Center)");
            _alignCenterButton.clicked += OnAlignCenterClicked;
            _root.Add(_alignCenterButton);
            
            // 右对齐按钮
            _alignRightButton = CreateToolbarButton("align_horizontally_right", "右对齐 (Align Right)");
            _alignRightButton.clicked += OnAlignRightClicked;
            _root.Add(_alignRightButton);
        }
        
        private Button CreateToolbarButton(string iconName, string tooltip)
        {
            var button = new Button();
            button.AddToClassList("scene-toolbar-button");
            button.tooltip = tooltip;
            
            var iconContent = EditorGUIUtility.IconContent(iconName);
            if (iconContent?.image != null)
            {
                var icon = new Image();
                icon.image = iconContent.image;
                button.Add(icon);
            }
            
            return button;
        }
        
        private void OnSnapButtonClicked()
        {
            SceneToolsSettings.SnapEnabled = !SceneToolsSettings.SnapEnabled;
            UpdateButtonStates();
            SceneView.RepaintAll();
        }
        
        private void OnGuidelinesButtonClicked()
        {
            SceneToolsSettings.GuidelinesVisible = !SceneToolsSettings.GuidelinesVisible;
            UpdateButtonStates();
            SceneView.RepaintAll();
        }
        
        private void OnAlignLeftClicked()
        {
            if (SceneAlignmentHelper.CanAlign())
            {
                SceneAlignmentHelper.Align(SceneAlignmentHelper.AlignmentType.Left);
            }
        }
        
        private void OnAlignCenterClicked()
        {
            if (SceneAlignmentHelper.CanAlign())
            {
                SceneAlignmentHelper.Align(SceneAlignmentHelper.AlignmentType.Center);
            }
        }
        
        private void OnAlignRightClicked()
        {
            if (SceneAlignmentHelper.CanAlign())
            {
                SceneAlignmentHelper.Align(SceneAlignmentHelper.AlignmentType.Right);
            }
        }
        
        private void OnSelectionChanged()
        {
            UpdateButtonStates();
        }
        
        public void UpdateButtonStates()
        {
            if (_snapButton == null) return;
            
            // 更新吸附按钮状态
            _snapButton.EnableInClassList("scene-toolbar-button--active", SceneToolsSettings.SnapEnabled);
            
            // 更新辅助线按钮状态
            _guidelinesButton.EnableInClassList("scene-toolbar-button--active", SceneToolsSettings.GuidelinesVisible);
            
            // 更新对齐按钮状态
            var canAlign = SceneAlignmentHelper.CanAlign();
            
            _alignLeftButton.EnableInClassList("scene-toolbar-button--disabled", !canAlign);
            _alignCenterButton.EnableInClassList("scene-toolbar-button--disabled", !canAlign);
            _alignRightButton.EnableInClassList("scene-toolbar-button--disabled", !canAlign);
            
            _alignLeftButton.SetEnabled(canAlign);
            _alignCenterButton.SetEnabled(canAlign);
            _alignRightButton.SetEnabled(canAlign);
        }
        
        public void UpdatePosition(SceneView sceneView)
        {
            if (_root == null) return;
            
            // UIToolkit使用USS中的position: absolute定位
            // 位置已在USS中通过right和top设置
        }
    }
}

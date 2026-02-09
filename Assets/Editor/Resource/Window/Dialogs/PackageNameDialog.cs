using GameDeveloperKit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// Package 名称输入对话框 (UIToolkit版本)
    /// </summary>
    public class PackageNameDialog : EditorWindow
    {
        private string _packageName = "NewPackage";
        public System.Action<string> OnConfirm;
        
        private VisualElement _root;
        private TextField _packageNameField;
        private Button _confirmButton;
        private Button _cancelButton;

        private void CreateGUI()
        {
            // 加载UXML
            var visualTree = EditorAssetLoader.LoadVisualTree("Resource/Window/Dialogs/PackageNameDialog.uxml");

            if (visualTree == null)
            {
                Debug.LogError("Failed to load PackageNameDialog.uxml");
                return;
            }

            _root = visualTree.CloneTree();
            
            // 设置根元素样式
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(_root);
            _root.style.flexGrow = 1;

            // 加载通用样式
            var commonStyleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");
            if (commonStyleSheet != null)
            {
                _root.styleSheets.Add(commonStyleSheet);
            }

            // 获取UI元素引用
            _packageNameField = _root.Q<TextField>("package-name-field");
            _confirmButton = _root.Q<Button>("confirm-button");
            _cancelButton = _root.Q<Button>("cancel-button");

            // 设置初始值
            _packageNameField.value = _packageName;
            
            // 聚焦到输入框
            _packageNameField.schedule.Execute(() => _packageNameField.Focus());

            // 绑定事件
            _confirmButton.clicked += OnConfirmClicked;
            _cancelButton.clicked += OnCancelClicked;
            
            // 回车键确认
            _packageNameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    OnConfirmClicked();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    OnCancelClicked();
                    evt.StopPropagation();
                }
            });
        }

        private void OnConfirmClicked()
        {
            _packageName = _packageNameField.value;
            if (!string.IsNullOrWhiteSpace(_packageName))
            {
                OnConfirm?.Invoke(_packageName);
                Close();
            }
        }

        private void OnCancelClicked()
        {
            Close();
        }
    }
}

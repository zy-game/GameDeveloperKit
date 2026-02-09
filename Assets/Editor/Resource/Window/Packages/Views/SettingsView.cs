using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 设置视图 - 嵌入在ResourcePackagesWindow中
    /// </summary>
    public class SettingsView
    {
        private VisualElement _container;
        private VisualElement _root;

        // UI元素引用
        private VisualElement _content;
        private VisualElement _labelsContainer;
        private VisualElement _labelsEmpty;
        private TextField _newLabelField;
        private Button _addLabelButton;

        private TextField _manifestPathField;
        private Button _browseManifestButton;

        private TextField _outputPathField;
        private Button _browseOutputButton;
        private VisualElement _buildTargetContainer;
        private VisualElement _compressionContainer;
        private Toggle _generateHashToggle;
        private Toggle _incrementalBuildToggle;
        private TextField _bundleExtensionField;

        /// <summary>
        /// 初始化设置视图
        /// </summary>
        public void Initialize(VisualElement container, VisualElement root)
        {
            _container = container;
            _root = root;

            // 加载UXML
            var visualTree = EditorAssetLoader.LoadVisualTree("Resource/Window/Settings/ResourceBuilderSettingsWindow.uxml");

            if (visualTree == null)
            {
                Debug.LogError("Failed to load ResourceBuilderSettingsWindow.uxml");
                return;
            }

            _content = visualTree.CloneTree();
            _container.Add(_content);

            // 隐藏内部的toolbar（因为外部已有header）
            var toolbar = _content.Q("toolbar");
            if (toolbar != null)
            {
                toolbar.style.display = DisplayStyle.None;
            }

            // 初始化UI引用
            InitializeUIReferences();

            // 绑定事件
            BindEvents();

            // 刷新UI
            RefreshUI();
        }

        private void InitializeUIReferences()
        {
            if (_content == null) return;

            // 标签管理
            _labelsContainer = _content.Q("labels-container");
            _labelsEmpty = _content.Q("labels-empty");
            _newLabelField = _content.Q<TextField>("new-label-field");
            _addLabelButton = _content.Q<Button>("add-label-button");

            // 清单设置
            _manifestPathField = _content.Q<TextField>("manifest-path-field");
            _browseManifestButton = _content.Q<Button>("browse-manifest-button");

            // 构建设置
            _outputPathField = _content.Q<TextField>("output-path-field");
            _browseOutputButton = _content.Q<Button>("browse-output-button");
            _buildTargetContainer = _content.Q("build-target-container");
            _compressionContainer = _content.Q("compression-container");
            _generateHashToggle = _content.Q<Toggle>("generate-hash-toggle");
            _incrementalBuildToggle = _content.Q<Toggle>("incremental-build-toggle");
            _bundleExtensionField = _content.Q<TextField>("bundle-extension-field");
        }

        private void BindEvents()
        {
            var settings = ResourceBuilderSettings.Instance;

            // 添加标签
            if (_addLabelButton != null)
                _addLabelButton.clicked += OnAddLabelClicked;

            if (_newLabelField != null)
            {
                _newLabelField.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    {
                        OnAddLabelClicked();
                        evt.StopPropagation();
                    }
                });
            }

            // 浏览按钮
            if (_browseManifestButton != null)
                _browseManifestButton.clicked += OnBrowseManifestClicked;
            if (_browseOutputButton != null)
                _browseOutputButton.clicked += OnBrowseOutputClicked;

            // 字段变化事件
            if (_manifestPathField != null)
            {
                _manifestPathField.RegisterValueChangedCallback(evt =>
                {
                    settings.manifestSavePath = evt.newValue;
                    settings.Save();
                });
            }

            if (_outputPathField != null)
            {
                _outputPathField.RegisterValueChangedCallback(evt =>
                {
                    settings.outputPath = evt.newValue;
                    settings.Save();
                });
            }

            if (_generateHashToggle != null)
            {
                _generateHashToggle.RegisterValueChangedCallback(evt =>
                {
                    settings.generateHash = evt.newValue;
                    settings.Save();
                });
            }

            if (_incrementalBuildToggle != null)
            {
                _incrementalBuildToggle.RegisterValueChangedCallback(evt =>
                {
                    settings.enableIncrementalBuild = evt.newValue;
                    settings.Save();
                });
            }

            if (_bundleExtensionField != null)
            {
                _bundleExtensionField.RegisterValueChangedCallback(evt =>
                {
                    settings.bundleExtension = evt.newValue;
                    settings.Save();
                });
            }
        }

        /// <summary>
        /// 刷新UI显示
        /// </summary>
        public void Refresh()
        {
            RefreshUI();
        }

        private void RefreshUI()
        {
            var settings = ResourceBuilderSettings.Instance;

            // 刷新标签列表
            RefreshLabelsList();

            // 加载设置值
            if (_manifestPathField != null)
                _manifestPathField.SetValueWithoutNotify(settings.manifestSavePath ?? "");
            if (_outputPathField != null)
                _outputPathField.SetValueWithoutNotify(settings.outputPath ?? "");
            if (_generateHashToggle != null)
                _generateHashToggle.SetValueWithoutNotify(settings.generateHash);
            if (_incrementalBuildToggle != null)
                _incrementalBuildToggle.SetValueWithoutNotify(settings.enableIncrementalBuild);
            if (_bundleExtensionField != null)
                _bundleExtensionField.SetValueWithoutNotify(settings.bundleExtension ?? "");

            // 创建枚举下拉框
            CreateEnumDropdowns();
        }

        private void CreateEnumDropdowns()
        {
            var settings = ResourceBuilderSettings.Instance;

            // Build Target下拉框
            if (_buildTargetContainer != null)
            {
                _buildTargetContainer.Clear();
                var buildTargetDropdown = CustomDropdownMenu.CreateEnumDropdown(
                    "Build Target",
                    settings.buildTarget,
                    newValue =>
                    {
                        settings.buildTarget = (BuildTarget)newValue;
                        settings.Save();
                    },
                    _root
                );
                _buildTargetContainer.Add(buildTargetDropdown);
            }

            // Compression下拉框
            if (_compressionContainer != null)
            {
                _compressionContainer.Clear();
                var compressionDropdown = CustomDropdownMenu.CreateEnumDropdown(
                    "Compression",
                    settings.compression,
                    newValue =>
                    {
                        settings.compression = (BuildAssetBundleOptions)newValue;
                        settings.Save();
                    },
                    _root
                );
                _compressionContainer.Add(compressionDropdown);
            }
        }

        private void RefreshLabelsList()
        {
            if (_labelsContainer == null || _labelsEmpty == null) return;

            var settings = ResourceBuilderSettings.Instance;
            _labelsContainer.Clear();

            if (settings.availableLabels == null || settings.availableLabels.Length == 0)
            {
                _labelsEmpty.style.display = DisplayStyle.Flex;
            }
            else
            {
                _labelsEmpty.style.display = DisplayStyle.None;
                foreach (var label in settings.availableLabels)
                {
                    var labelItem = CreateLabelItem(label);
                    _labelsContainer.Add(labelItem);
                }
            }
        }

        private VisualElement CreateLabelItem(string labelName)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingTop = 6;
            item.style.paddingBottom = 6;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.marginBottom = 4;
            item.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;

            // 图标
            var icon = new Label("•");
            icon.style.fontSize = 20;
            icon.style.color = new Color(0.5f, 0.8f, 1f);
            icon.style.marginRight = 8;
            item.Add(icon);

            // 标签名
            var nameLabel = new Label(labelName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            nameLabel.style.fontSize = 13;
            item.Add(nameLabel);

            // 删除按钮
            var deleteButton = new Button(() => OnDeleteLabelClicked(labelName));
            deleteButton.text = "Del";
            deleteButton.AddToClassList("btn");
            deleteButton.AddToClassList("btn-danger");
            deleteButton.style.minWidth = 50;
            deleteButton.style.height = 22;
            item.Add(deleteButton);

            return item;
        }

        private void OnAddLabelClicked()
        {
            if (_newLabelField == null) return;

            var labelName = _newLabelField.value;
            if (string.IsNullOrWhiteSpace(labelName))
            {
                EditorUtility.DisplayDialog("错误", "标签名称不能为空", "确定");
                return;
            }

            var settings = ResourceBuilderSettings.Instance;
            if (settings.HasLabel(labelName))
            {
                EditorUtility.DisplayDialog("错误", $"标签 '{labelName}' 已存在", "确定");
                return;
            }

            settings.AddLabel(labelName);
            _newLabelField.value = "";
            RefreshLabelsList();
        }

        private void OnDeleteLabelClicked(string labelName)
        {
            if (EditorUtility.DisplayDialog("删除标签", $"确定要删除标签 '{labelName}' 吗？", "删除", "取消"))
            {
                var settings = ResourceBuilderSettings.Instance;
                settings.RemoveLabel(labelName);
                RefreshLabelsList();
            }
        }

        private void OnBrowseManifestClicked()
        {
            var path = EditorUtility.SaveFolderPanel("选择清单保存路径", "", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (_manifestPathField != null)
                    _manifestPathField.value = path;
            }
        }

        private void OnBrowseOutputClicked()
        {
            var path = EditorUtility.SaveFolderPanel("选择输出路径", "", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (_outputPathField != null)
                    _outputPathField.value = path;
            }
        }
    }
}

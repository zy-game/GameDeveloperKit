using System.IO;
using GameDeveloperKit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源构建设置窗口 (UIToolkit版本)
    /// </summary>
    public class ResourceBuilderSettingsWindow : EditorWindow
    {
        // UI元素引用
        private VisualElement _root;
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

        public static void ShowWindow()
        {
            var window = GetWindow<ResourceBuilderSettingsWindow>("构建设置");
            window.position = new Rect(0, 0, 500, 900);
            window.minSize = new Vector2(500, 900);
            window.maxSize = new Vector2(500, 900);
            window.Show();
        }


        private void CreateGUI()
        {
            // 加载UXML
            var visualTree = EditorAssetLoader.LoadVisualTree("Resource/Window/Settings/ResourceBuilderSettingsWindow.uxml");

            if (visualTree == null)
            {
                Debug.LogError("Failed to load ResourceBuilderSettingsWindow.uxml");
                return;
            }

            _root = visualTree.CloneTree();

            // 设置根元素样式
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(_root);
            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Column;

            // 加载通用样式
            var commonStyleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");
            if (commonStyleSheet != null)
            {
                _root.styleSheets.Add(commonStyleSheet);
            }

            // 获取UI元素引用
            InitializeUIReferences();

            // 强制布局
            var contentScroll = _root.Q("content-scroll");
            contentScroll.style.flexGrow = 1;

            // 绑定事件
            BindEvents();

            // 刷新UI
            RefreshUI();
        }

        private void InitializeUIReferences()
        {
            // 标签管理
            _labelsContainer = _root.Q("labels-container");
            _labelsEmpty = _root.Q("labels-empty");
            _newLabelField = _root.Q<TextField>("new-label-field");
            _addLabelButton = _root.Q<Button>("add-label-button");

            // 清单设置
            _manifestPathField = _root.Q<TextField>("manifest-path-field");
            _browseManifestButton = _root.Q<Button>("browse-manifest-button");

            // 构建设置
            _outputPathField = _root.Q<TextField>("output-path-field");
            _browseOutputButton = _root.Q<Button>("browse-output-button");
            _buildTargetContainer = _root.Q("build-target-container");
            _compressionContainer = _root.Q("compression-container");
            _generateHashToggle = _root.Q<Toggle>("generate-hash-toggle");
            _incrementalBuildToggle = _root.Q<Toggle>("incremental-build-toggle");
            _bundleExtensionField = _root.Q<TextField>("bundle-extension-field");
        }

        private void BindEvents()
        {
            var settings = ResourceBuilderSettings.Instance;

            // 添加标签
            _addLabelButton.clicked += OnAddLabelClicked;
            _newLabelField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    OnAddLabelClicked();
                    evt.StopPropagation();
                }
            });

            // 浏览按钮
            _browseManifestButton.clicked += OnBrowseManifestClicked;
            _browseOutputButton.clicked += OnBrowseOutputClicked;

            // 字段变化事件
            _manifestPathField.RegisterValueChangedCallback(evt =>
            {
                settings.manifestSavePath = evt.newValue;
                settings.Save();
            });

            _outputPathField.RegisterValueChangedCallback(evt =>
            {
                settings.outputPath = evt.newValue;
                settings.Save();
            });

            _generateHashToggle.RegisterValueChangedCallback(evt =>
            {
                settings.generateHash = evt.newValue;
                settings.Save();
            });

            _incrementalBuildToggle.RegisterValueChangedCallback(evt =>
            {
                settings.enableIncrementalBuild = evt.newValue;
                settings.Save();
            });

            _bundleExtensionField.RegisterValueChangedCallback(evt =>
            {
                settings.bundleExtension = evt.newValue;
                settings.Save();
            });
        }

        private void RefreshUI()
        {
            var settings = ResourceBuilderSettings.Instance;

            // 刷新标签列表
            RefreshLabelsList();

            // 加载设置值
            _manifestPathField.SetValueWithoutNotify(settings.manifestSavePath ?? "");
            _outputPathField.SetValueWithoutNotify(settings.outputPath ?? "");
            _generateHashToggle.SetValueWithoutNotify(settings.generateHash);
            _incrementalBuildToggle.SetValueWithoutNotify(settings.enableIncrementalBuild);
            _bundleExtensionField.SetValueWithoutNotify(settings.bundleExtension ?? "");

            // 创建枚举下拉框
            CreateEnumDropdowns();
        }

        private void CreateEnumDropdowns()
        {
            var settings = ResourceBuilderSettings.Instance;

            // Build Target下拉框
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

            // Compression下拉框
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

        private void RefreshLabelsList()
        {
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
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;
            item.style.marginBottom = 4;

            // 图标
            var icon = new Label("•");
            icon.style.width = 15;
            icon.style.color = new Color(0.23f, 0.51f, 0.96f); // 蓝色
            item.Add(icon);

            // 标签名
            var nameLabel = new Label(labelName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.color = new Color(0.86f, 0.86f, 0.86f);
            item.Add(nameLabel);

            // 删除按钮
            var deleteButton = new Button(() => OnDeleteLabelClicked(labelName));
            deleteButton.text = "删除";
            deleteButton.AddToClassList("btn");
            deleteButton.AddToClassList("btn-danger");
            deleteButton.style.minWidth = 60;
            item.Add(deleteButton);

            return item;
        }

        private void OnAddLabelClicked()
        {
            var labelName = _newLabelField.value;
            if (!string.IsNullOrWhiteSpace(labelName))
            {
                var settings = ResourceBuilderSettings.Instance;
                settings.AddLabel(labelName);
                _newLabelField.value = "";
                RefreshLabelsList();
            }
        }

        private void OnDeleteLabelClicked(string labelName)
        {
            if (EditorUtility.DisplayDialog("删除标签",
                $"确定要删除标签 '{labelName}' 吗？", "删除", "取消"))
            {
                var settings = ResourceBuilderSettings.Instance;
                settings.RemoveLabel(labelName);
                RefreshLabelsList();
            }
        }

        private void OnBrowseManifestClicked()
        {
            var settings = ResourceBuilderSettings.Instance;
            var path = EditorUtility.OpenFolderPanel("Select Manifest Save Path", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    settings.manifestSavePath = "Assets" + path.Substring(Application.dataPath.Length);
                    _manifestPathField.value = settings.manifestSavePath;
                    settings.Save();
                }
                else
                {
                    EditorUtility.DisplayDialog("Invalid Path", "Please select a folder inside the Assets directory", "OK");
                }
            }
        }

        private void OnBrowseOutputClicked()
        {
            var settings = ResourceBuilderSettings.Instance;
            var path = EditorUtility.OpenFolderPanel("Select Output Path", "", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    settings.outputPath = "Assets" + path.Substring(Application.dataPath.Length);
                }
                else
                {
                    var projectPath = Directory.GetParent(Application.dataPath)?.FullName;
                    if (projectPath != null && path.StartsWith(projectPath))
                    {
                        settings.outputPath = path.Substring(projectPath.Length + 1);
                    }
                    else
                    {
                        settings.outputPath = path;
                    }
                }
                _outputPathField.value = settings.outputPath;
                settings.Save();
            }
        }

        #region IMGUI备份（注释掉，可用于回退）
        /*
        private void OnGUI()
        {
            var settings = ResourceBuilderSettings.Instance;

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // 标签管理区域
            DrawLabelManagement(settings);

            EditorGUILayout.Space(20);

            EditorGUILayout.LabelField("构建设置", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUI.BeginChangeCheck();

            // Manifest Settings
            EditorGUILayout.LabelField("清单设置", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            settings.manifestSavePath = EditorGUILayout.TextField("Manifest Save Path", settings.manifestSavePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Manifest Save Path", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        settings.manifestSavePath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Path", "Please select a folder inside the Assets directory", "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Build Settings
            EditorGUILayout.LabelField("构建设置", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            settings.outputPath = EditorGUILayout.TextField("Output Path", settings.outputPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Output Path", "", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        settings.outputPath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        var projectPath = Directory.GetParent(Application.dataPath)?.FullName;
                        if (projectPath != null && path.StartsWith(projectPath))
                        {
                            settings.outputPath = path.Substring(projectPath.Length + 1);
                        }
                        else
                        {
                            settings.outputPath = path;
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            settings.buildTarget = (BuildTarget)EditorGUILayout.EnumPopup("Build Target", settings.buildTarget);
            settings.compression = (BuildAssetBundleOptions)EditorGUILayout.EnumPopup("Compression", settings.compression);

            EditorGUILayout.Space(5);
            settings.generateHash = EditorGUILayout.Toggle("Generate Hash", settings.generateHash);
            settings.enableIncrementalBuild = EditorGUILayout.Toggle("Enable Incremental Build", settings.enableIncrementalBuild);

            EditorGUILayout.Space(5);
            settings.bundleExtension = EditorGUILayout.TextField("Bundle Extension", settings.bundleExtension);

            if (EditorGUI.EndChangeCheck())
            {
                settings.Save();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLabelManagement(ResourceBuilderSettings settings)
        {
            EditorGUILayout.LabelField("标签管理", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical("box");

            // 显示现有标签
            EditorGUILayout.LabelField("可用标签列表:", EditorStyles.boldLabel);

            if (settings.availableLabels == null || settings.availableLabels.Length == 0)
            {
                EditorGUILayout.HelpBox("暂无可用标签", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < settings.availableLabels.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    // 标签图标和名称
                    GUILayout.Label("•", GUILayout.Width(15));
                    GUILayout.Label(settings.availableLabels[i], GUILayout.ExpandWidth(true));

                    // 删除按钮
                    if (GUILayout.Button("删除", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("删除标签",
                            $"确定要删除标签 '{settings.availableLabels[i]}' 吗？", "删除", "取消"))
                        {
                            settings.RemoveLabel(settings.availableLabels[i]);
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(10);

            // 添加新标签
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("添加新标签:", GUILayout.Width(80));
            _newLabelName = EditorGUILayout.TextField(_newLabelName);

            if (GUILayout.Button("添加", GUILayout.Width(60)))
            {
                if (!string.IsNullOrWhiteSpace(_newLabelName))
                {
                    settings.AddLabel(_newLabelName);
                    _newLabelName = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("标签名称只能包含字母、数字和下划线", MessageType.Info);

            EditorGUILayout.EndVertical();
        }
        */
        #endregion
    }
}

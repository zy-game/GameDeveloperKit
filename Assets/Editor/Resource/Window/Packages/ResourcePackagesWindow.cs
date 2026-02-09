using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using GameDeveloperKit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源信息结构体，用于在编辑器中显示资源详情
    /// </summary>
    public struct AssetDisplayInfo
    {
        public string path;
        public string guid;
        public UnityEngine.Object obj;
        public System.Type type;
        public string address;

        public AssetDisplayInfo(string path, string guid, UnityEngine.Object obj, System.Type type, string address)
        {
            this.path = path;
            this.guid = guid;
            this.obj = obj;
            this.type = type;
            this.address = address;
        }
    }
    /// <summary>
    /// 资源包管理窗口（UIToolkit重构版）
    /// </summary>
    public class ResourcePackagesWindow : EditorWindow
    {
        // 视图模式枚举
        private enum DetailViewMode
        {
            PackageDetail,    // Package详情
            Settings,         // 构建设置
            Report,           // 构建报告
            CreatePackage     // 创建Package
        }

        private ResourcePackagesData _data;
        private ResourceBuilderPrefs _prefs;
        private PackageSettings _selectedPackage;

        // UI元素引用
        private VisualElement _root;
        private VisualElement _packageListContainer;
        private VisualElement _detailContent;
        private VisualElement _emptyState;
        private VisualElement _packageInfoCard;
        private VisualElement _versionCard;
        private VisualElement _groupsSection;
        private VisualElement _groupsContainer;
        
        // 新增：收集器和打包策略卡片
        private VisualElement _collectorCard;
        private VisualElement _collectorContent;
        private VisualElement _packStrategyCard;
        private VisualElement _packStrategyContent;

        // 视图切换相关
        private DetailViewMode _currentViewMode = DetailViewMode.PackageDetail;
        private VisualElement _packageDetailView;
        private VisualElement _settingsView;
        private VisualElement _settingsContentContainer;
        private VisualElement _reportView;
        private VisualElement _reportContentContainer;
        private VisualElement _createPackageView;
        private TextField _newPackageNameField;

        // 嵌入的子视图实例
        private SettingsView _settingsViewInstance;
        private BuildReportView _buildReportViewInstance;
        
        // 新增：收集器和打包策略编辑器视图
        private CollectorEditorView _collectorEditorView;
        private PackStrategyEditorView _packStrategyEditorView;

        // 版本清单缓存
        private GameDeveloperKit.Resource.VersionManifest _cachedVersionManifest;
        private string _cachedManifestPath;
        private DateTime _lastManifestLoadTime;

        // 分割条拖拽
        private VisualElement _splitter;
        private VisualElement _leftPanel;
        private bool _isDraggingSplitter;
        private float _splitterPos = 0.3f;

        [MenuItem("GameDeveloperKit/资源包工具")]
        public static void ShowWindow()
        {
            try
            {
                var window = GetWindow<ResourcePackagesWindow>("资源包管理");
                window.minSize = new Vector2(900, 500);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ResourcePackagesWindow] 打开窗口失败: {ex}");
            }
        }

        private void CreateGUI()
        {
            try
            {
                // 加载数据
                _data = ResourcePackagesData.Instance;
                _prefs = ResourceBuilderPrefs.Instance;
                _splitterPos = _prefs.splitterPosition;

                if (!string.IsNullOrEmpty(_prefs.selectedPackageId))
                {
                    _selectedPackage = _data.FindPackage(_prefs.selectedPackageId);
                }

                // 加载UXML
                var visualTree = EditorAssetLoader.LoadVisualTree("Resource/Window/Packages/ResourcePackagesWindow.uxml");

                if (visualTree == null)
                {
                    Debug.LogError("Failed to load ResourcePackagesWindow.uxml");
                    ShowErrorUI("无法加载 ResourcePackagesWindow.uxml");
                    return;
                }

                _root = visualTree.CloneTree();

                // 确保rootVisualElement填充整个窗口
                rootVisualElement.style.flexGrow = 1;
                rootVisualElement.style.flexDirection = FlexDirection.Column;

                rootVisualElement.Add(_root);

                // 关键修复：强制设置_root的flexGrow（CSS可能不生效）
                _root.style.flexGrow = 1;
                _root.style.flexDirection = FlexDirection.Column;

                // 加载通用编辑器样式
                var commonStyleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");

                if (commonStyleSheet != null)
                {
                    _root.styleSheets.Add(commonStyleSheet);
                }

                // 加载通用菜单样式
                var menuStyleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/CustomDropdownMenu.uss");

                if (menuStyleSheet != null)
                {
                    _root.styleSheets.Add(menuStyleSheet);
                }

                // 获取UI元素引用
                InitializeUIReferences();

                // 强制设置布局样式（确保填充整个窗口）
                var contentArea = _root.Q("content-area");
                if (contentArea != null)
                {
                    contentArea.style.flexGrow = 1;
                    contentArea.style.flexDirection = FlexDirection.Row;
                }

                if (_leftPanel != null)
                {
                    _leftPanel.style.flexDirection = FlexDirection.Column;
                    _leftPanel.style.flexShrink = 0;
                    _leftPanel.style.width = new StyleLength(new Length(_splitterPos * 100, LengthUnit.Percent));
                }

                var rightPanel = _root.Q("right-panel");
                if (rightPanel != null)
                {
                    rightPanel.style.flexGrow = 1;
                    rightPanel.style.flexDirection = FlexDirection.Column;
                }

                var packageScroll = _root.Q<ScrollView>("package-scroll");
                if (packageScroll != null)
                {
                    packageScroll.style.flexGrow = 1;
                    packageScroll.contentContainer.style.flexGrow = 1;
                }

                var detailScroll = _root.Q<ScrollView>("detail-scroll");
                if (detailScroll != null)
                {
                    detailScroll.style.flexGrow = 1;
                    detailScroll.contentContainer.style.flexGrow = 1;
                }

                if (_packageListContainer != null)
                    _packageListContainer.style.flexGrow = 1;
                if (_detailContent != null)
                    _detailContent.style.flexGrow = 1;
                if (_emptyState != null)
                    _emptyState.style.flexGrow = 1;

                // 绑定事件
                BindEvents();

                // 加载版本清单
                LoadVersionManifestCache();

                // 刷新UI
                RefreshPackageList();
                RefreshDetailPanel();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ResourcePackagesWindow] CreateGUI 失败: {ex.Message}\n{ex.StackTrace}");
                ShowErrorUI($"初始化失败: {ex.Message}");
            }
        }
        
        private void ShowErrorUI(string message)
        {
            rootVisualElement.Clear();
            var label = new Label(message);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = 14;
            label.style.marginTop = 20;
            label.style.color = Color.red;
            rootVisualElement.Add(label);
        }

        private void OnDestroy()
        {
            _prefs?.Save();
            _data?.Save();
        }

        private void InitializeUIReferences()
        {
            // 工具栏
            var menuButton = _root.Q<Button>("menu-button");

            // 左侧面板
            _leftPanel = _root.Q<VisualElement>("left-panel");
            _packageListContainer = _root.Q<VisualElement>("package-list-container");
            var addPackageButton = _root.Q<Button>("add-package-button");

            // 分割条
            _splitter = _root.Q<VisualElement>("splitter");

            // 右侧面板 - Package详情视图
            _detailContent = _root.Q<VisualElement>("detail-content");
            _packageDetailView = _root.Q<VisualElement>("package-detail-view");
            _emptyState = _root.Q<VisualElement>("empty-state");
            _packageInfoCard = _root.Q<VisualElement>("package-info-card");
            _versionCard = _root.Q<VisualElement>("version-card");
            _groupsSection = _root.Q<VisualElement>("groups-section");
            _groupsContainer = _root.Q<VisualElement>("groups-container");
            
            // 新增：收集器和打包策略卡片
            _collectorCard = _root.Q<VisualElement>("collector-card");
            _collectorContent = _root.Q<VisualElement>("collector-content");
            _packStrategyCard = _root.Q<VisualElement>("pack-strategy-card");
            _packStrategyContent = _root.Q<VisualElement>("pack-strategy-content");

            // 右侧面板 - 其他视图
            _settingsView = _root.Q<VisualElement>("settings-view");
            _settingsContentContainer = _root.Q<VisualElement>("settings-content-container");
            _reportView = _root.Q<VisualElement>("report-view");
            _reportContentContainer = _root.Q<VisualElement>("report-content-container");
            _createPackageView = _root.Q<VisualElement>("create-package-view");
            _newPackageNameField = _root.Q<TextField>("new-package-name-field");

            // 事件绑定
            if (menuButton != null)
                menuButton.clicked += ShowToolsMenu;

            if (addPackageButton != null)
                addPackageButton.clicked += OnAddPackageClicked;

            // 视图切换按钮事件
            var backFromSettingsButton = _root.Q<Button>("back-from-settings-button");
            if (backFromSettingsButton != null)
                backFromSettingsButton.clicked += () => SwitchToView(DetailViewMode.PackageDetail);

            var backFromReportButton = _root.Q<Button>("back-from-report-button");
            if (backFromReportButton != null)
                backFromReportButton.clicked += () => SwitchToView(DetailViewMode.PackageDetail);

            var backFromCreateButton = _root.Q<Button>("back-from-create-button");
            if (backFromCreateButton != null)
                backFromCreateButton.clicked += () => SwitchToView(DetailViewMode.PackageDetail);

            // 创建Package表单按钮
            var cancelCreateButton = _root.Q<Button>("cancel-create-button");
            if (cancelCreateButton != null)
                cancelCreateButton.clicked += () => SwitchToView(DetailViewMode.PackageDetail);

            var confirmCreateButton = _root.Q<Button>("confirm-create-button");
            if (confirmCreateButton != null)
                confirmCreateButton.clicked += OnConfirmCreatePackage;
        }

        private void BindEvents()
        {
            // 分割条拖拽
            if (_splitter != null)
            {
                _splitter.RegisterCallback<MouseDownEvent>(OnSplitterMouseDown);
                _splitter.RegisterCallback<MouseMoveEvent>(OnSplitterMouseMove);
                _splitter.RegisterCallback<MouseUpEvent>(OnSplitterMouseUp);
            }
        }

        #region Package List

        private void RefreshPackageList()
        {
            if (_packageListContainer == null) return;

            _packageListContainer.Clear();

            if (_data.packages.Count == 0)
            {
                var helpBox = CreateHelpBox("没有资源包。点击 '+ 新建包' 来创建一个。", HelpBoxType.Info);
                _packageListContainer.Add(helpBox);
                return;
            }

            foreach (var package in _data.packages)
            {
                var packageItem = CreatePackageItem(package);
                _packageListContainer.Add(packageItem);
            }
        }

        private VisualElement CreatePackageItem(PackageSettings package)
        {
            var item = new VisualElement();
            item.AddToClassList("package-item");

            if (_selectedPackage == package)
            {
                item.AddToClassList("package-item--selected");
            }

            // 图标
            var icon = new VisualElement();
            icon.AddToClassList("package-icon");
            icon.style.backgroundImage = EditorGUIUtility.IconContent("Project").image as Texture2D;
            item.Add(icon);

            // 包类型标识
            var badge = new Label(package.packageType == PackageType.BasePackage ? "首" : "热");
            badge.AddToClassList("package-type-badge");
            badge.AddToClassList(package.packageType == PackageType.BasePackage
                ? "package-type-badge--base"
                : "package-type-badge--hotfix");
            item.Add(badge);

            // 包名称
            var nameLabel = new Label($"{package.packageName} (v{package.version})");
            nameLabel.AddToClassList("package-name");
            item.Add(nameLabel);

            // 点击事件
            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // 左键
                {
                    SelectPackage(package);
                }
                else if (evt.button == 1) // 右键
                {
                    var menu = new CustomDropdownMenu();
                    menu.AddItem("构建", false, () => BuildPackage(package));
                    menu.AddItem("强制重新构建", false, () => BuildPackage(package, true));
                    menu.AddSeparator();
                    menu.AddItem("复制", false, () => DuplicatePackage(package));
                    menu.AddItem("删除", false, () => DeletePackage(package));
                    menu.ShowAsContext(_root);
                    evt.StopPropagation();
                }
            });

            return item;
        }

        private void SelectPackage(PackageSettings package)
        {
            _selectedPackage = package;
            _prefs.selectedPackageId = package.packageName;
            _prefs.Save();

            RefreshPackageList();
            RefreshDetailPanel();
        }

        #endregion

        #region Detail Panel

        private void RefreshDetailPanel()
        {
            if (_selectedPackage == null)
            {
                _emptyState.style.display = DisplayStyle.Flex;
                _packageInfoCard.style.display = DisplayStyle.None;
                _versionCard.style.display = DisplayStyle.None;
                if (_groupsSection != null) _groupsSection.style.display = DisplayStyle.None;
                if (_collectorCard != null) _collectorCard.style.display = DisplayStyle.None;
                if (_packStrategyCard != null) _packStrategyCard.style.display = DisplayStyle.None;
                return;
            }

            _emptyState.style.display = DisplayStyle.None;
            _packageInfoCard.style.display = DisplayStyle.Flex;
            _versionCard.style.display = DisplayStyle.Flex;
            
            // 隐藏旧的 groups-section，使用新的收集器和打包策略卡片
            if (_groupsSection != null) _groupsSection.style.display = DisplayStyle.None;
            if (_collectorCard != null) _collectorCard.style.display = DisplayStyle.Flex;
            if (_packStrategyCard != null) _packStrategyCard.style.display = DisplayStyle.Flex;
            
            RefreshCollectorEditor();
            RefreshPackStrategyEditor();

            RefreshPackageInfo();
            RefreshVersionManagement();
        }
        
        private void RefreshCollectorEditor()
        {
            if (_collectorContent == null) return;
            
            _collectorEditorView ??= new CollectorEditorView();
            _collectorEditorView.Initialize(_collectorContent, _root, _selectedPackage, () =>
            {
                _data.Save();
            });
        }
        
        private void RefreshPackStrategyEditor()
        {
            if (_packStrategyContent == null) return;
            
            _packStrategyEditorView ??= new PackStrategyEditorView();
            _packStrategyEditorView.Initialize(_packStrategyContent, _root, _selectedPackage, () =>
            {
                _data.Save();
            });
        }

        private void RefreshPackageInfo()
        {
            // 名称
            var nameField = _root.Q<TextField>("package-name-field");
            if (nameField != null)
            {
                nameField.SetValueWithoutNotify(_selectedPackage.packageName);
                nameField.RegisterValueChangedCallback(evt =>
                {
                    _selectedPackage.packageName = evt.newValue;
                    _data.Save();
                    RefreshPackageList();
                });
            }

            // 版本
            var versionField = _root.Q<TextField>("package-version-field");
            if (versionField != null)
            {
                versionField.SetValueWithoutNotify(_selectedPackage.version);
                versionField.RegisterValueChangedCallback(evt =>
                {
                    _selectedPackage.version = evt.newValue;
                    _data.Save();
                });
            }

            // 地址模式 - 使用自定义下拉框
            var addressModeContainer = _root.Q<VisualElement>("address-mode-container");
            if (addressModeContainer != null)
            {
                addressModeContainer.Clear();
                var addressModeField = CustomDropdownMenu.CreateEnumDropdown(
                    "地址模式",
                    _selectedPackage.addressMode,
                    newValue =>
                    {
                        _selectedPackage.addressMode = newValue;
                        _data.Save();
                    },
                    _root
                );
                addressModeContainer.Add(addressModeField);
            }
        }

        private void RefreshPackageTypeHelp()
        {
            var helpBox = _root.Q<VisualElement>("package-type-help");
            if (helpBox == null) return;

            helpBox.Clear();

            var helpText = _selectedPackage.packageType == PackageType.BasePackage
                ? "首包资源会在构建时拷贝到 StreamingAssets 目录，随安装包发布。"
                : "热更资源不会拷贝到 StreamingAssets，需要在运行时从服务器下载。";

            var label = new Label(helpText);
            label.AddToClassList("help-box-text");
            helpBox.Add(label);
        }

        private void RefreshVersionManagement()
        {
            var versionContent = _root.Q<VisualElement>("version-content");
            if (versionContent == null) return;

            versionContent.Clear();

            var manifest = _cachedVersionManifest;

            if (manifest == null)
            {
                var noManifestHelp = CreateHelpBox("版本清单文件不存在，请先构建该资源包", HelpBoxType.Warning);
                versionContent.Add(noManifestHelp);

                var refreshButton = new Button(() =>
                {
                    LoadVersionManifestCache();
                    RefreshVersionManagement();
                });
                refreshButton.text = "刷新";
                refreshButton.AddToClassList("btn");
                refreshButton.AddToClassList("btn-primary");
                refreshButton.style.width = 80;
                versionContent.Add(refreshButton);
                return;
            }

            var packageInfo = manifest.packages?.FirstOrDefault(p => p.name == _selectedPackage.packageName);

            if (packageInfo == null)
            {
                var noPackageHelp = CreateHelpBox("该Package尚未在版本清单中注册，请先构建", HelpBoxType.Warning);
                versionContent.Add(noPackageHelp);
                return;
            }

            if (packageInfo.versions == null || packageInfo.versions.Length == 0)
            {
                var noVersionsHelp = CreateHelpBox("该Package没有可用的历史版本", HelpBoxType.Info);
                versionContent.Add(noVersionsHelp);
                return;
            }

            // 版本选择
            var versions = packageInfo.versions.Select(v => v.version).ToList();
            var currentVersion = packageInfo.currentVersion;

            var versionDropdown = CreateStringDropdown(
                "当前版本:",
                versions,
                currentVersion,
                newVersion =>
                {
                    if (GlobalManifestEditor.SetPackageCurrentVersion(_selectedPackage.packageName, newVersion))
                    {
                        Debug.Log($"[版本管理] Package '{_selectedPackage.packageName}' 版本已切换至: {newVersion}");
                        LoadVersionManifestCache();
                        RefreshVersionManagement();
                    }
                }
            );

            versionContent.Add(versionDropdown);

            // 版本详情
            var currentIndex = versions.IndexOf(currentVersion);
            if (currentIndex >= 0 && currentIndex < packageInfo.versions.Length)
            {
                var versionDetail = packageInfo.versions[currentIndex];

                var detailsLabel = new Label("版本详情");
                detailsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                detailsLabel.style.marginTop = 12;
                detailsLabel.style.marginBottom = 8;
                versionContent.Add(detailsLabel);

                AddDetailField(versionContent, "版本号", versionDetail.version);
                AddDetailField(versionContent, "构建时间", versionDetail.buildTime);
                AddDetailField(versionContent, "大小", $"{versionDetail.size / 1024.0 / 1024.0:F2} MB");
                AddDetailField(versionContent, "Bundle数量", versionDetail.bundleCount.ToString());
                AddDetailField(versionContent, "清单路径", versionDetail.manifestPath);
            }

            // 帮助信息
            var helpBox = CreateHelpBox(
                "运行时会使用版本清单中的 currentVersion 来决定加载哪个版本的资源。\n" +
                "通过此下拉列表可以实现版本回滚或指定特定版本。",
                HelpBoxType.Info);
            versionContent.Add(helpBox);
        }

        private void AddDetailField(VisualElement container, string label, string value)
        {
            var field = new VisualElement();
            field.style.flexDirection = FlexDirection.Row;
            field.style.marginBottom = 4;

            var labelElement = new Label(label + ":");
            labelElement.style.minWidth = 100;
            labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
            field.Add(labelElement);

            var valueElement = new Label(value);
            valueElement.style.color = new Color(0.86f, 0.86f, 0.86f);
            field.Add(valueElement);

            container.Add(field);
        }

        #endregion

        #region Splitter

        private void OnSplitterMouseDown(MouseDownEvent evt)
        {
            _isDraggingSplitter = true;
            _splitter.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnSplitterMouseMove(MouseMoveEvent evt)
        {
            if (_isDraggingSplitter)
            {
                var newWidth = evt.mousePosition.x;
                var windowWidth = rootVisualElement.resolvedStyle.width;
                _splitterPos = Mathf.Clamp(newWidth / windowWidth, 0.2f, 0.8f);
                _leftPanel.style.width = new StyleLength(new Length(_splitterPos * 100, LengthUnit.Percent));
                _prefs.splitterPosition = _splitterPos;
                evt.StopPropagation();
            }
        }

        private void OnSplitterMouseUp(MouseUpEvent evt)
        {
            if (_isDraggingSplitter)
            {
                _isDraggingSplitter = false;
                _splitter.ReleaseMouse();
                evt.StopPropagation();
            }
        }

        #endregion

        #region View Switching

        /// <summary>
        /// 切换视图模式
        /// </summary>
        private void SwitchToView(DetailViewMode mode)
        {
            _currentViewMode = mode;

            // 隐藏所有视图
            if (_packageDetailView != null)
                _packageDetailView.style.display = DisplayStyle.None;
            if (_settingsView != null)
                _settingsView.style.display = DisplayStyle.None;
            if (_reportView != null)
                _reportView.style.display = DisplayStyle.None;
            if (_createPackageView != null)
                _createPackageView.style.display = DisplayStyle.None;

            // 显示目标视图
            switch (mode)
            {
                case DetailViewMode.PackageDetail:
                    if (_packageDetailView != null)
                        _packageDetailView.style.display = DisplayStyle.Flex;
                    break;

                case DetailViewMode.Settings:
                    if (_settingsView != null)
                    {
                        _settingsView.style.display = DisplayStyle.Flex;
                        if (_settingsViewInstance == null)
                        {
                            _settingsViewInstance = new SettingsView();
                            _settingsViewInstance.Initialize(_settingsContentContainer, _root);
                        }
                        else
                        {
                            _settingsViewInstance.Refresh();
                        }
                    }
                    break;

                case DetailViewMode.Report:
                    if (_reportView != null)
                        _reportView.style.display = DisplayStyle.Flex;
                    // 报告内容在ShowReport时设置
                    break;

                case DetailViewMode.CreatePackage:
                    if (_createPackageView != null)
                    {
                        _createPackageView.style.display = DisplayStyle.Flex;
                        if (_newPackageNameField != null)
                        {
                            _newPackageNameField.value = "NewPackage";
                            _newPackageNameField.schedule.Execute(() => _newPackageNameField.Focus());
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 点击"新建包"按钮
        /// </summary>
        private void OnAddPackageClicked()
        {
            SwitchToView(DetailViewMode.CreatePackage);
        }

        /// <summary>
        /// 确认创建Package
        /// </summary>
        private void OnConfirmCreatePackage()
        {
            if (_newPackageNameField == null)
                return;

            var name = _newPackageNameField.value;

            if (string.IsNullOrEmpty(name))
            {
                EditorUtility.DisplayDialog("错误", "资源包名称不能为空", "确定");
                return;
            }

            if (_data.PackageExists(name))
            {
                EditorUtility.DisplayDialog("错误", $"资源包 '{name}' 已存在", "确定");
                return;
            }

            var package = new PackageSettings(name);
            _data.AddPackage(package);

            SelectPackage(package);
            SwitchToView(DetailViewMode.PackageDetail);
        }

        /// <summary>
        /// 显示构建报告（公共方法，从外部调用）
        /// </summary>
        public void ShowReport(AssetBundleBuilder.BuildReport report)
        {
            if (_buildReportViewInstance == null)
            {
                _buildReportViewInstance = new BuildReportView();
                _buildReportViewInstance.Initialize(_reportContentContainer, _root);
            }
            _buildReportViewInstance.SetReport(report);
            SwitchToView(DetailViewMode.Report);
        }

        #endregion

        #region Dialogs and Menus

        private void ShowCreatePackageDialog()
        {
            var window = ScriptableObject.CreateInstance<PackageNameDialog>();
            window.titleContent = new GUIContent("创建资源包");
            window.OnConfirm = (name) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    EditorUtility.DisplayDialog("错误", "资源包名称不能为空", "确定");
                    return;
                }

                if (_data.PackageExists(name))
                {
                    EditorUtility.DisplayDialog("错误", $"资源包 '{name}' 已存在", "确定");
                    return;
                }

                var package = new PackageSettings(name);
                _data.AddPackage(package);

                SelectPackage(package);
            };
            window.ShowModal();
        }

        private void ShowToolsMenu()
        {
            var menu = new CustomDropdownMenu();
            var settings = ResourceBuilderSettings.Instance;

            // 构建
            menu.AddItem("构建当前资源包", false, () =>
            {
                if (_selectedPackage != null)
                    BuildPackage(_selectedPackage);
            });
            menu.AddItem("全部构建", false, BuildAllPackages);
            menu.AddItem("验证资源", false, ValidateSelectedPackage);
            menu.AddSeparator();

            // 设置
            menu.AddItem("打包设置", false, () => SwitchToView(DetailViewMode.Settings));
            menu.AddItem("打开输出文件夹", false, OpenOutputFolder);
            menu.AddSeparator();

            // SBP 工具
            menu.AddItem("清理构建缓存", false, () => SBPCacheManager.ClearCache());
            menu.AddItem("查看缓存信息", false, () => SBPCacheManager.ShowCacheInfo());
            menu.AddSeparator();

            // Build History
            menu.AddItem("打开构建报告", false, OpenBuildReport);
            menu.AddSeparator();

            // 重置
            menu.AddItem("重置所有资源包", false, () =>
            {
                if (EditorUtility.DisplayDialog("重置所有", "这将删除所有资源包。确定吗？", "重置", "取消"))
                {
                    _data.packages.Clear();
                    _data.Save();
                    _selectedPackage = null;
                    RefreshPackageList();
                    RefreshDetailPanel();
                }
            });

            menu.ShowAsContext(_root);
        }

        #endregion

        #region Build Operations

        private void BuildPackage(PackageSettings package, bool forceRebuild = false)
        {
            EditorUtility.DisplayProgressBar("Building Package", $"Building {package.packageName}...", 0);

            try
            {
                var settings = ResourceBuilderSettings.Instance;
                var originalForceRebuild = settings.forceRebuild;
                settings.forceRebuild = forceRebuild;

                var report = AssetBundleBuilder.BuildFromPackageSettings(package);

                settings.forceRebuild = originalForceRebuild;

                EditorUtility.ClearProgressBar();

                if (report.success)
                {
                    LoadVersionManifestCache();

                    EditorUtility.DisplayDialog("Build Successful",
                        $"Package '{package.packageName}' built successfully!\n\n" +
                        $"Bundles: {report.totalBundles}\n" +
                        $"Assets: {report.totalAssets}\n" +
                        $"Size: {report.totalSize / 1024.0 / 1024.0:F2} MB\n" +
                        $"Time: {report.buildTime:F2}s", "OK");

                    // 在嵌入视图中显示报告
                    ShowReport(report);
                    RefreshVersionManagement();
                }
                else
                {
                    EditorUtility.DisplayDialog("Build Failed", report.message, "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Build Exception", ex.Message, "OK");
                Debug.LogError($"Build exception: {ex}");
            }
        }

        private void BuildAllPackages()
        {
            foreach (var package in _data.packages)
            {
                BuildPackage(package);
            }
        }

        private void ValidateSelectedPackage()
        {
            if (_selectedPackage == null)
            {
                EditorUtility.DisplayDialog("验证资源", "请先选择一个资源包", "确定");
                return;
            }
            
            EditorUtility.DisplayProgressBar("验证资源", $"正在验证 {_selectedPackage.packageName}...", 0.5f);
            
            try
            {
                var result = AssetValidator.ValidatePackage(_selectedPackage);
                EditorUtility.ClearProgressBar();
                
                ValidationResultWindow.ShowResult(result, _selectedPackage.packageName);
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("验证失败", $"验证时发生错误:\n{ex.Message}", "确定");
                Debug.LogError($"[Validation] {ex}");
            }
        }

        private void DuplicatePackage(PackageSettings package)
        {
            var json = JsonUtility.ToJson(package);
            var newPackage = JsonUtility.FromJson<PackageSettings>(json);
            newPackage.packageName = $"{package.packageName}_Copy";
            _data.AddPackage(newPackage);
            RefreshPackageList();
        }

        private void DeletePackage(PackageSettings package)
        {
            if (EditorUtility.DisplayDialog("删除资源包",
                $"确定要删除资源包 '{package.packageName}' 吗？", "删除", "取消"))
            {
                _data.RemovePackage(package.packageName);
                if (_selectedPackage == package)
                    _selectedPackage = null;
                RefreshPackageList();
                RefreshDetailPanel();
            }
        }

        private void OpenOutputFolder()
        {
            var settings = ResourceBuilderSettings.Instance;
            var outputPath = Path.GetFullPath(settings.outputPath);
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            Process.Start(outputPath);
        }

        private void OpenBuildReport()
        {
            // 对于历史报告查看，提供两个选项：
            // 1. 在嵌入视图中显示（如果有最近的报告）
            // 2. 打开独立窗口查看完整历史

            if (_selectedPackage != null)
            {
                var settings = ResourceBuilderSettings.Instance;
                var reportPath = Path.Combine(settings.outputPath, _selectedPackage.packageName,
                    $"{_selectedPackage.packageName}_BuildReport.txt");

                if (File.Exists(reportPath))
                {
                    // 尝试从文件加载报告并在嵌入视图中显示
                    var report = LoadReportFromFile(reportPath);
                    if (report != null)
                    {
                        ShowReport(report);
                        return;
                    }

                    // 加载失败，但文件存在，打开独立窗口并传入路径
                    BuildReportWindow.ShowWindow(reportPath);
                    return;
                }

                // 没有报告文件，显示提示
                EditorUtility.DisplayDialog("提示",
                    $"Package '{_selectedPackage.packageName}' 还没有构建报告。\n请先构建该Package。",
                    "确定");
                return;
            }

            // 没有选中Package，打开独立窗口查看所有历史
            BuildReportWindow.ShowWindow();
        }

        /// <summary>
        /// 从报告文件加载BuildReport
        /// </summary>
        private AssetBundleBuilder.BuildReport LoadReportFromFile(string reportPath)
        {
            try
            {
                if (!File.Exists(reportPath))
                    return null;

                var content = File.ReadAllText(reportPath);
                var outputPath = Path.GetDirectoryName(reportPath);

                // 解析报告内容（简化版）
                var report = new AssetBundleBuilder.BuildReport
                {
                    success = content.Contains("✓ Success") || content.Contains("SUCCESS"),
                    message = "",
                    outputPath = outputPath,
                    bundleSizes = new Dictionary<string, long>()
                };

                // 尝试解析基本信息
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("Total Bundles:"))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int count))
                            report.totalBundles = count;
                    }
                    else if (line.Contains("Total Assets:"))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int count))
                            report.totalAssets = count;
                    }
                    else if (line.Contains("Total Size:"))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            var sizeStr = parts[1].Trim().Replace(" MB", "");
                            if (float.TryParse(sizeStr, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float sizeMB))
                                report.totalSize = (long)(sizeMB * 1024 * 1024);
                        }
                    }
                    else if (line.Contains("Build Time:"))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            var timeStr = parts[1].Trim().Replace("s", "");
                            if (float.TryParse(timeStr, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float time))
                                report.buildTime = time;
                        }
                    }
                    // 解析Bundle大小（格式：  - bundleName: 1.23 MB）
                    else if (line.Trim().StartsWith("- ") && line.Contains(":") && line.Contains("MB"))
                    {
                        var parts = line.Trim().Substring(2).Split(':');
                        if (parts.Length == 2)
                        {
                            var bundleName = parts[0].Trim();
                            var sizeStr = parts[1].Trim().Replace("MB", "").Trim();
                            if (float.TryParse(sizeStr, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float sizeMB))
                            {
                                report.bundleSizes[bundleName] = (long)(sizeMB * 1024 * 1024);
                            }
                        }
                    }
                }

                return report;
            }
            catch (Exception ex)
            {
                Debug.LogError($"加载报告文件失败: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Version Manifest

        private void LoadVersionManifestCache()
        {
            var settings = ResourceBuilderSettings.Instance;
            var manifestPath = Path.Combine(settings.outputPath, "manifest.json");

            if (!File.Exists(manifestPath))
            {
                _cachedVersionManifest = null;
                _cachedManifestPath = null;
                return;
            }

            var lastWriteTime = File.GetLastWriteTime(manifestPath);

            if (_cachedManifestPath != manifestPath ||
                _lastManifestLoadTime != lastWriteTime)
            {
                _cachedVersionManifest = GlobalManifestEditor.LoadGlobalManifest();
                _cachedManifestPath = manifestPath;
                _lastManifestLoadTime = lastWriteTime;

                Debug.Log($"[ResourcePackagesWindow] 版本清单缓存已刷新: {manifestPath}");
            }
        }

        #endregion

        #region Custom UI Components

        /// <summary>
        /// HelpBox类型枚举
        /// </summary>
        private enum HelpBoxType
        {
            Info,
            Warning,
            Error,
            Success
        }

        /// <summary>
        /// 创建自定义样式的HelpBox
        /// </summary>
        private VisualElement CreateHelpBox(string message, HelpBoxType type)
        {
            var box = new VisualElement();
            box.AddToClassList("custom-help-box");

            switch (type)
            {
                case HelpBoxType.Info:
                    box.AddToClassList("help-box--info");
                    break;
                case HelpBoxType.Warning:
                    box.AddToClassList("help-box--warning");
                    break;
                case HelpBoxType.Error:
                    box.AddToClassList("help-box--error");
                    break;
                case HelpBoxType.Success:
                    box.AddToClassList("help-box--success");
                    break;
            }

            var label = new Label(message);
            label.AddToClassList("help-box__text");
            box.Add(label);

            return box;
        }

        /// <summary>
        /// 创建字符串列表下拉框（自定义样式）
        /// </summary>
        private VisualElement CreateStringDropdown(string label, List<string> choices, string currentValue, Action<string> onValueChanged)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 8;
            container.AddToClassList("custom-dropdown");

            // Label
            if (!string.IsNullOrEmpty(label))
            {
                var labelElement = new Label(label);
                labelElement.style.minWidth = 100;
                labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
                container.Add(labelElement);
            }

            // 值显示按钮
            var button = new Button();
            button.style.flexGrow = 1;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.AddToClassList("custom-dropdown-button");

            // 更新按钮文本
            string selectedValue = currentValue;
            void UpdateButtonText(string value)
            {
                button.text = value;
                selectedValue = value;
            }
            UpdateButtonText(currentValue);

            // 点击显示菜单
            button.clicked += () =>
            {
                var menu = new CustomDropdownMenu();

                foreach (var choice in choices)
                {
                    var value = choice; // 捕获变量
                    var isChecked = value == selectedValue;

                    menu.AddItem(value, isChecked, () =>
                    {
                        UpdateButtonText(value);
                        onValueChanged?.Invoke(value);
                    });
                }

                menu.ShowAsDropdown(button, _root);
            };

            container.Add(button);
            return container;
        }

        #endregion
    }
}

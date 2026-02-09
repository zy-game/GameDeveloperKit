using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.ArtMapping
{
    /// <summary>
    /// 美术资源映射编辑器窗口
    /// </summary>
    public class ArtResourceMappingWindow : EditorWindow
    {
        private ArtResourceMappingSettings _settings;
        private VisualElement _root;
        private VisualElement _mappingListContainer;
        private TextField _artProjectRootField;
        private Label _svnStatusLabel;
        private bool _isUpdating;

        [MenuItem("GameDeveloperKit/美术资源映射")]
        public static void ShowWindow()
        {
            try
            {
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        var window = GetWindow<ArtResourceMappingWindow>("美术资源映射");
                        if (window != null)
                        {
                            window.minSize = new Vector2(700, 400);
                            window.Show();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[ArtResourceMappingWindow] 打开窗口失败: {ex.Message}");
                        
                        bool reset = EditorUtility.DisplayDialog(
                            "窗口打开失败",
                            "Unity 编辑器窗口系统出现问题。\n\n" +
                            "这是 Unity 编辑器的已知问题，通常是由于布局文件损坏导致。\n\n" +
                            "建议解决方案：\n" +
                            "1. 点击「重置布局」按钮\n" +
                            "2. 重启 Unity 编辑器\n" +
                            "3. 如果问题持续，删除 Library 文件夹\n\n" +
                            "是否现在重置布局？",
                            "重置布局",
                            "取消"
                        );
                        
                        if (reset)
                        {
                            var layoutUtility = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.WindowLayout");
                            if (layoutUtility != null)
                            {
                                var method = layoutUtility.GetMethod("LoadDefaultLayout", 
                                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                                if (method != null)
                                {
                                    method.Invoke(null, null);
                                    Debug.Log("[ArtResourceMappingWindow] 已重置布局，请重新尝试打开窗口");
                                }
                            }
                        }
                    }
                };
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ArtResourceMappingWindow] 打开窗口失败: {ex.Message}");
            }
        }

        private void CreateGUI()
        {
            _settings = ArtResourceMappingSettings.Instance;

            _root = new VisualElement();
            _root.AddToClassList("root");
            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(_root);

            // 加载通用样式
            var commonStyleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");
            if (commonStyleSheet != null)
            {
                _root.styleSheets.Add(commonStyleSheet);
            }

            // 创建工具栏
            CreateToolbar();

            // 创建内容区域
            CreateContentArea();

            // 刷新映射列表
            RefreshMappingList();
        }

        private void CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            var title = new Label("美术资源映射");
            title.AddToClassList("toolbar-title");
            toolbar.Add(title);

            var spacer = new VisualElement();
            spacer.AddToClassList("toolbar-spacer");
            toolbar.Add(spacer);

            // SVN 状态
            _svnStatusLabel = new Label();
            _svnStatusLabel.style.fontSize = 11;
            _svnStatusLabel.style.marginRight = 16;
            UpdateSvnStatus();
            toolbar.Add(_svnStatusLabel);

            // 全部更新按钮
            var updateAllButton = new Button(OnUpdateAllClicked);
            updateAllButton.text = "全部更新 (SVN)";
            updateAllButton.AddToClassList("btn");
            updateAllButton.AddToClassList("btn-primary");
            toolbar.Add(updateAllButton);

            _root.Add(toolbar);
        }

        private void CreateContentArea()
        {
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;

            var content = new VisualElement();
            content.AddToClassList("detail-content");
            content.style.paddingTop = 16;
            content.style.paddingBottom = 16;
            content.style.paddingLeft = 16;
            content.style.paddingRight = 16;

            // 美术工程根目录配置
            CreateArtProjectRootSection(content);

            // 映射列表区域
            CreateMappingListSection(content);

            scrollView.Add(content);
            _root.Add(scrollView);
        }

        private void CreateArtProjectRootSection(VisualElement parent)
        {
            var card = new VisualElement();
            card.AddToClassList("info-card");

            var cardTitle = new Label("美术工程配置");
            cardTitle.AddToClassList("card-title");
            card.Add(cardTitle);

            // 根目录路径
            var pathContainer = new VisualElement();
            pathContainer.AddToClassList("field");
            pathContainer.style.flexDirection = FlexDirection.Row;
            pathContainer.style.alignItems = Align.Center;

            _artProjectRootField = new TextField("根目录");
            _artProjectRootField.value = _settings.artProjectRoot;
            _artProjectRootField.AddToClassList("custom-textfield");
            _artProjectRootField.style.flexGrow = 1;
            _artProjectRootField.RegisterValueChangedCallback(evt =>
            {
                _settings.artProjectRoot = evt.newValue;
                _settings.Save();
            });
            pathContainer.Add(_artProjectRootField);

            var browseButton = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择美术工程根目录", _settings.artProjectRoot, "");
                if (!string.IsNullOrEmpty(path))
                {
                    _settings.artProjectRoot = path;
                    _settings.Save();
                    _artProjectRootField.value = path;
                }
            });
            browseButton.text = "浏览...";
            browseButton.AddToClassList("btn");
            browseButton.AddToClassList("btn-secondary");
            browseButton.style.marginLeft = 8;
            pathContainer.Add(browseButton);

            card.Add(pathContainer);

            // 帮助信息
            var helpBox = CreateHelpBox("设置美术工程的根目录路径，映射规则中的源目录将相对于此路径。", HelpBoxType.Info);
            card.Add(helpBox);

            parent.Add(card);
        }

        private void CreateMappingListSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.AddToClassList("groups-section");

            // 标题栏
            var header = new VisualElement();
            header.AddToClassList("section-header");

            var sectionTitle = new Label("目录映射");
            sectionTitle.AddToClassList("section-title");
            header.Add(sectionTitle);

            var addButton = new Button(OnAddMappingClicked);
            addButton.text = "+ 添加映射";
            addButton.AddToClassList("btn");
            addButton.AddToClassList("btn-success");
            header.Add(addButton);

            section.Add(header);

            // 映射列表容器
            _mappingListContainer = new VisualElement();
            _mappingListContainer.AddToClassList("groups-container");
            section.Add(_mappingListContainer);

            parent.Add(section);
        }

        private void RefreshMappingList()
        {
            _mappingListContainer.Clear();

            if (_settings.mappings.Count == 0)
            {
                var emptyHint = CreateHelpBox("暂无映射规则，点击「添加映射」按钮创建新的目录映射。", HelpBoxType.Info);
                _mappingListContainer.Add(emptyHint);
                return;
            }

            foreach (var mapping in _settings.mappings)
            {
                var mappingCard = CreateMappingCard(mapping);
                _mappingListContainer.Add(mappingCard);
            }
        }

        private VisualElement CreateMappingCard(DirectoryMapping mapping)
        {
            var card = new VisualElement();
            card.AddToClassList("group-card");

            // 头部
            var header = new VisualElement();
            header.AddToClassList("group-header");

            // 启用开关
            var enableToggle = new Toggle();
            enableToggle.value = mapping.enabled;
            enableToggle.style.marginRight = 8;
            enableToggle.RegisterValueChangedCallback(evt =>
            {
                mapping.enabled = evt.newValue;
                _settings.Save();
            });
            header.Add(enableToggle);

            // 状态指示
            var statusLabel = new Label();
            var status = GetMappingStatus(mapping);
            UpdateStatusLabel(statusLabel, status);
            statusLabel.style.marginRight = 8;
            header.Add(statusLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            // 更新按钮
            var updateButton = new Button(() => OnUpdateMappingClicked(mapping));
            updateButton.text = "更新";
            updateButton.AddToClassList("btn");
            updateButton.AddToClassList("btn-primary");
            updateButton.AddToClassList("btn-sm");
            updateButton.style.marginRight = 4;
            header.Add(updateButton);

            // 创建/删除链接按钮
            if (status == SymlinkStatus.Linked)
            {
                var unlinkButton = new Button(() => OnUnlinkClicked(mapping));
                unlinkButton.text = "删除链接";
                unlinkButton.AddToClassList("btn");
                unlinkButton.AddToClassList("btn-danger");
                unlinkButton.AddToClassList("btn-sm");
                unlinkButton.style.marginRight = 4;
                header.Add(unlinkButton);
            }
            else if (status == SymlinkStatus.NotLinked || status == SymlinkStatus.SourceNotFound)
            {
                var linkButton = new Button(() => OnLinkClicked(mapping));
                linkButton.text = "创建链接";
                linkButton.AddToClassList("btn");
                linkButton.AddToClassList("btn-success");
                linkButton.AddToClassList("btn-sm");
                linkButton.style.marginRight = 4;
                header.Add(linkButton);
            }

            // 删除映射按钮
            var deleteButton = new Button(() => OnDeleteMappingClicked(mapping));
            deleteButton.text = "删除";
            deleteButton.AddToClassList("btn");
            deleteButton.AddToClassList("btn-danger");
            deleteButton.AddToClassList("btn-sm");
            header.Add(deleteButton);

            card.Add(header);

            // 配置区域
            var configSection = new VisualElement();
            configSection.AddToClassList("group-config");

            // 源目录
            var sourceContainer = new VisualElement();
            sourceContainer.AddToClassList("field");
            sourceContainer.style.flexDirection = FlexDirection.Row;
            sourceContainer.style.alignItems = Align.Center;
            sourceContainer.style.marginBottom = 8;

            var sourceField = new TextField("源目录");
            sourceField.value = mapping.sourceDirectory;
            sourceField.AddToClassList("custom-textfield");
            sourceField.style.flexGrow = 1;
            sourceField.RegisterValueChangedCallback(evt =>
            {
                mapping.sourceDirectory = evt.newValue;
                _settings.Save();
                RefreshMappingList();
            });
            sourceContainer.Add(sourceField);

            var sourceBrowseButton = new Button(() =>
            {
                var startPath = string.IsNullOrEmpty(_settings.artProjectRoot) ? "" : _settings.artProjectRoot;
                var path = EditorUtility.OpenFolderPanel("选择源目录", startPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    // 转换为相对路径
                    if (!string.IsNullOrEmpty(_settings.artProjectRoot) && path.StartsWith(_settings.artProjectRoot))
                    {
                        path = path.Substring(_settings.artProjectRoot.Length).TrimStart('/', '\\');
                    }
                    mapping.sourceDirectory = path;
                    _settings.Save();
                    RefreshMappingList();
                }
            });
            sourceBrowseButton.text = "...";
            sourceBrowseButton.AddToClassList("btn");
            sourceBrowseButton.AddToClassList("btn-secondary");
            sourceBrowseButton.AddToClassList("btn-sm");
            sourceBrowseButton.style.marginLeft = 4;
            sourceContainer.Add(sourceBrowseButton);

            configSection.Add(sourceContainer);

            // 目标目录
            var targetContainer = new VisualElement();
            targetContainer.AddToClassList("field");
            targetContainer.style.flexDirection = FlexDirection.Row;
            targetContainer.style.alignItems = Align.Center;

            var targetField = new TextField("目标目录");
            targetField.value = mapping.targetDirectory;
            targetField.AddToClassList("custom-textfield");
            targetField.style.flexGrow = 1;
            targetField.RegisterValueChangedCallback(evt =>
            {
                mapping.targetDirectory = evt.newValue;
                _settings.Save();
                RefreshMappingList();
            });
            targetContainer.Add(targetField);

            var targetBrowseButton = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择目标目录", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    // 转换为相对于 Assets 的路径
                    var assetsPath = Path.GetFullPath("Assets");
                    if (path.StartsWith(assetsPath))
                    {
                        path = path.Substring(assetsPath.Length).TrimStart('/', '\\');
                    }
                    else if (path.StartsWith(Application.dataPath))
                    {
                        path = path.Substring(Application.dataPath.Length).TrimStart('/', '\\');
                    }
                    mapping.targetDirectory = path;
                    _settings.Save();
                    RefreshMappingList();
                }
            });
            targetBrowseButton.text = "...";
            targetBrowseButton.AddToClassList("btn");
            targetBrowseButton.AddToClassList("btn-secondary");
            targetBrowseButton.AddToClassList("btn-sm");
            targetBrowseButton.style.marginLeft = 4;
            targetContainer.Add(targetBrowseButton);

            configSection.Add(targetContainer);
            card.Add(configSection);

            return card;
        }

        private SymlinkStatus GetMappingStatus(DirectoryMapping mapping)
        {
            var sourcePath = _settings.GetFullSourcePath(mapping);
            var targetPath = _settings.GetFullTargetPath(mapping);
            return SymlinkUtility.GetSymlinkStatus(sourcePath, targetPath);
        }

        private void UpdateStatusLabel(Label label, SymlinkStatus status)
        {
            switch (status)
            {
                case SymlinkStatus.Linked:
                    label.text = "● 已链接";
                    label.style.color = new Color(0.063f, 0.725f, 0.506f);
                    break;
                case SymlinkStatus.NotLinked:
                    label.text = "○ 未链接";
                    label.style.color = new Color(0.5f, 0.5f, 0.5f);
                    break;
                case SymlinkStatus.SourceNotFound:
                    label.text = "✗ 源不存在";
                    label.style.color = new Color(0.937f, 0.267f, 0.267f);
                    break;
                case SymlinkStatus.TargetExists:
                    label.text = "! 目标已存在";
                    label.style.color = new Color(0.961f, 0.62f, 0.043f);
                    break;
                default:
                    label.text = "? 错误";
                    label.style.color = new Color(0.937f, 0.267f, 0.267f);
                    break;
            }
        }

        private void UpdateSvnStatus()
        {
            if (SvnUtility.IsSvnAvailable())
            {
                var version = SvnUtility.GetSvnVersion();
                _svnStatusLabel.text = $"SVN {version}";
                _svnStatusLabel.style.color = new Color(0.063f, 0.725f, 0.506f);
            }
            else
            {
                _svnStatusLabel.text = "SVN 不可用";
                _svnStatusLabel.style.color = new Color(0.937f, 0.267f, 0.267f);
            }
        }

        #region Event Handlers

        private void OnAddMappingClicked()
        {
            _settings.AddMapping();
            RefreshMappingList();
        }

        private void OnDeleteMappingClicked(DirectoryMapping mapping)
        {
            if (EditorUtility.DisplayDialog("删除映射", "确定要删除此映射规则吗？", "删除", "取消"))
            {
                // 如果存在符号链接，先删除
                var status = GetMappingStatus(mapping);
                if (status == SymlinkStatus.Linked)
                {
                    var targetPath = _settings.GetFullTargetPath(mapping);
                    SymlinkUtility.DeleteSymlink(targetPath);
                }

                _settings.RemoveMapping(mapping.id);
                RefreshMappingList();
                AssetDatabase.Refresh();
            }
        }

        private void OnLinkClicked(DirectoryMapping mapping)
        {
            var sourcePath = _settings.GetFullSourcePath(mapping);
            var targetPath = _settings.GetFullTargetPath(mapping);

            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
            {
                EditorUtility.DisplayDialog("错误", "请先设置源目录和目标目录", "确定");
                return;
            }

            if (SymlinkUtility.CreateSymlink(targetPath, sourcePath))
            {
                EditorUtility.DisplayDialog("成功", "符号链接创建成功", "确定");
                RefreshMappingList();
                AssetDatabase.Refresh();
            }
            else
            {
                EditorUtility.DisplayDialog("失败", "符号链接创建失败，请检查路径和权限", "确定");
            }
        }

        private void OnUnlinkClicked(DirectoryMapping mapping)
        {
            if (EditorUtility.DisplayDialog("删除链接", "确定要删除此符号链接吗？\n（不会影响源目录的内容）", "删除", "取消"))
            {
                var targetPath = _settings.GetFullTargetPath(mapping);
                if (SymlinkUtility.DeleteSymlink(targetPath))
                {
                    RefreshMappingList();
                    AssetDatabase.Refresh();
                }
                else
                {
                    EditorUtility.DisplayDialog("失败", "符号链接删除失败", "确定");
                }
            }
        }

        private async void OnUpdateMappingClicked(DirectoryMapping mapping)
        {
            if (_isUpdating)
            {
                EditorUtility.DisplayDialog("提示", "正在更新中，请稍候...", "确定");
                return;
            }

            var sourcePath = _settings.GetFullSourcePath(mapping);
            if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
            {
                EditorUtility.DisplayDialog("错误", "源目录不存在", "确定");
                return;
            }

            _isUpdating = true;
            EditorUtility.DisplayProgressBar("SVN 更新", $"正在更新: {mapping.sourceDirectory}", 0.5f);

            try
            {
                var result = await SvnUtility.UpdateAsync(sourcePath, progress =>
                {
                    EditorUtility.DisplayProgressBar("SVN 更新", progress, 0.5f);
                });

                EditorUtility.ClearProgressBar();

                if (result.success)
                {
                    EditorUtility.DisplayDialog("更新完成", $"SVN 更新成功\n\n{result.output}", "确定");
                    AssetDatabase.Refresh();
                }
                else
                {
                    EditorUtility.DisplayDialog("更新失败", $"SVN 更新失败\n\n{result.error}", "确定");
                }
            }
            finally
            {
                _isUpdating = false;
                EditorUtility.ClearProgressBar();
            }
        }

        private async void OnUpdateAllClicked()
        {
            if (_isUpdating)
            {
                EditorUtility.DisplayDialog("提示", "正在更新中，请稍候...", "确定");
                return;
            }

            if (string.IsNullOrEmpty(_settings.artProjectRoot) || !Directory.Exists(_settings.artProjectRoot))
            {
                EditorUtility.DisplayDialog("错误", "请先设置有效的美术工程根目录", "确定");
                return;
            }

            _isUpdating = true;
            EditorUtility.DisplayProgressBar("SVN 更新", "正在更新美术工程...", 0.5f);

            try
            {
                var result = await SvnUtility.UpdateAsync(_settings.artProjectRoot, progress =>
                {
                    EditorUtility.DisplayProgressBar("SVN 更新", progress, 0.5f);
                });

                EditorUtility.ClearProgressBar();

                if (result.success)
                {
                    EditorUtility.DisplayDialog("更新完成", $"SVN 更新成功\n\n{result.output}", "确定");
                    AssetDatabase.Refresh();
                }
                else
                {
                    EditorUtility.DisplayDialog("更新失败", $"SVN 更新失败\n\n{result.error}", "确定");
                }
            }
            finally
            {
                _isUpdating = false;
                EditorUtility.ClearProgressBar();
            }
        }

        #endregion

        #region Helper Methods

        private enum HelpBoxType
        {
            Info,
            Warning,
            Error,
            Success
        }

        private VisualElement CreateHelpBox(string message, HelpBoxType type)
        {
            var helpBox = new VisualElement();
            helpBox.AddToClassList("custom-help-box");

            switch (type)
            {
                case HelpBoxType.Info:
                    helpBox.AddToClassList("help-box--info");
                    break;
                case HelpBoxType.Warning:
                    helpBox.AddToClassList("help-box--warning");
                    break;
                case HelpBoxType.Error:
                    helpBox.AddToClassList("help-box--error");
                    break;
                case HelpBoxType.Success:
                    helpBox.AddToClassList("help-box--success");
                    break;
            }

            var label = new Label(message);
            label.AddToClassList("help-box__text");
            helpBox.Add(label);

            return helpBox;
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.ResourcePublisher;
using GameDeveloperKit.TagEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Resource Editor Window 类型。
    /// </summary>
    public sealed class ResourceEditorWindow : EditorWindow
    {
        /// <summary>
        /// 定义 Window Title 常量。
        /// </summary>
        private const string WindowTitle = "资源编辑器";
        /// <summary>
        /// 定义 Uxml Path 常量。
        /// </summary>
        private const string UxmlPath = "Editor/ResourceEditor/UI/ResourceEditorWindow.uxml";

        /// <summary>
        /// 存储 Settings。
        /// </summary>
        private ResourceEditorSettings m_Settings;
        /// <summary>
        /// 存储 Registry。
        /// </summary>
        private ResourceEditorRegistry m_Registry;
        /// <summary>         /// 存储 Issues。         /// </summary>
        private List<ResourceValidationIssue> m_Issues = new List<ResourceValidationIssue>();
        /// <summary>         /// 存储 Previews。         /// </summary>
        private readonly Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>> m_Previews = new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>>();

        /// <summary>
        /// 存储 Package List。
        /// </summary>
        private ListView m_PackageList;
        /// <summary>
        /// 存储 Empty State。
        /// </summary>
        private VisualElement m_EmptyState;
        /// <summary>
        /// 存储 Package Detail。
        /// </summary>
        private ScrollView m_PackageDetail;
        /// <summary>
        /// 存储 Package Name Field。
        /// </summary>
        private TextField m_PackageNameField;
        /// <summary>
        /// 存储 Package Mode Dropdown。
        /// </summary>
        private DropdownField m_PackageModeDropdown;
        /// <summary>
        /// 存储 Build Dropdown。
        /// </summary>
        private DropdownField m_BuildDropdown;
        /// <summary>
        /// 存储 Build Channel Field。
        /// </summary>
        private DropdownField m_BuildChannelField;
        /// <summary>
        /// 存储 Build Version Field。
        /// </summary>
        private TextField m_BuildVersionField;
        /// <summary>
        /// 存储 Build Compression Dropdown。
        /// </summary>
        private DropdownField m_BuildCompressionDropdown;
        /// <summary>
        /// 存储 Bundle Container。
        /// </summary>
        private VisualElement m_BundleContainer;
        /// <summary>
        /// 定义 Builtin Mode Label 常量。
        /// </summary>
        private const string BuiltinModeLabel = "内置";
        /// <summary>
        /// 定义 Hot Update Mode Label 常量。
        /// </summary>
        private const string HotUpdateModeLabel = "热更";
        /// <summary>
        /// 定义 Compression Default Label 常量。
        /// </summary>
        private const string CompressionDefaultLabel = "默认";
        /// <summary>
        /// 定义 Compression Lz4 Label 常量。
        /// </summary>
        private const string CompressionLz4Label = "LZ4";
        /// <summary>
        /// 定义 Compression Uncompressed Label 常量。
        /// </summary>
        private const string CompressionUncompressedLabel = "未压缩";

        /// <summary>
        /// 执行 Open。
        /// </summary>
        [MenuItem("GameDeveloperKit/"+WindowTitle)]
        public static void Open()
        {
            var window = GetWindow<ResourceEditorWindow>();
            window.titleContent = new UnityEngine.GUIContent(WindowTitle);
            window.minSize = new UnityEngine.Vector2(920, 560);
            window.Show();
        }

        /// <summary>
        /// 创建 GUI。
        /// </summary>
        public void CreateGUI()
        {
            m_Settings = ResourceEditorSettings.LoadOrCreate();
            m_Registry = ResourceEditorRegistryCache.Current ?? ResourceEditorRegistryCache.Refresh();

            var visualTree = GameDeveloperKitEditorPaths.LoadPackageAsset<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                rootVisualElement.Add(new Label($"Missing UXML: {GameDeveloperKitEditorPaths.PackageAssetPath(UxmlPath)}"));
                return;
            }

            rootVisualElement.Clear();
            visualTree.CloneTree(rootVisualElement);
            ApplyEditorTheme();
            QueryElements();
            BindToolbar();
            BindPackageList();
            RefreshAll();
        }

        /// <summary>
        /// 执行 Apply Editor Theme。
        /// </summary>
        private void ApplyEditorTheme()
        {
            var root = rootVisualElement.Q<VisualElement>(className: "resource-editor");
            if (root == null)
            {
                return;
            }

            root.EnableInClassList("resource-editor--dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("resource-editor--light", EditorGUIUtility.isProSkin is false);
        }

        /// <summary>
        /// 执行 Query Elements。
        /// </summary>
        private void QueryElements()
        {
            m_PackageList = rootVisualElement.Q<ListView>("package-list");
            m_EmptyState = rootVisualElement.Q<VisualElement>("empty-state");
            m_PackageDetail = rootVisualElement.Q<ScrollView>("package-detail");
            m_PackageNameField = rootVisualElement.Q<TextField>("package-name-field");
            m_PackageNameField.isDelayed = true;
            m_PackageModeDropdown = rootVisualElement.Q<DropdownField>("package-mode-dropdown");
            m_BuildDropdown = rootVisualElement.Q<DropdownField>("build-dropdown");
            m_BuildChannelField = rootVisualElement.Q<DropdownField>("build-channel-field");
            m_BuildVersionField = rootVisualElement.Q<TextField>("build-version-field");
            m_BuildCompressionDropdown = rootVisualElement.Q<DropdownField>("build-compression-dropdown");
            m_BuildVersionField.isDelayed = true;
            m_BundleContainer = rootVisualElement.Q<VisualElement>("bundle-container");
        }

        /// <summary>
        /// 执行 Bind Toolbar。
        /// </summary>
        private void BindToolbar()
        {
            rootVisualElement.Q<Button>("build-all-button").clicked += BuildAllPackages;
            rootVisualElement.Q<Button>("add-package-button").clicked += AddPackage;
            rootVisualElement.Q<Button>("remove-package-button").clicked += RemoveSelectedPackage;
            rootVisualElement.Q<Button>("add-bundle-button").clicked += AddBundle;
            rootVisualElement.Q<Button>("run-check-button").clicked += ShowCheckResultWindow;
            rootVisualElement.Q<Button>("package-build-button").clicked += BuildSelectedPackage;

            BindBuildSettings();

            m_PackageNameField.RegisterValueChangedCallback(evt =>
            {
                var package = GetSelectedPackage();
                if (package == null)
                {
                    return;
                }

                package.Name = evt.newValue;
                SaveSettingsImmediately();
                RefreshPackageList();
            });

            m_PackageModeDropdown.RegisterValueChangedCallback(evt =>
            {
                var package = GetSelectedPackage();
                if (package == null)
                {
                    return;
                }

                package.IsHotUpdate = evt.newValue == HotUpdateModeLabel;
                SaveSettingsImmediately();
                RefreshPackageList();
            });

            m_BuildDropdown.RegisterValueChangedCallback(evt =>
            {
                var package = GetSelectedPackage();
                if (package == null)
                {
                    return;
                }

                package.BuildStrategyId = FindBuildIdByName(evt.newValue);
                SaveSettingsImmediately();
                RefreshPackageList();
            });
        }

        /// <summary>
        /// 执行 Bind Build Settings。
        /// </summary>
        private void BindBuildSettings()
        {
            m_BuildChannelField.RegisterValueChangedCallback(evt =>
            {
                m_Settings.BuildSettings.Channel = evt.newValue;
                SaveSettingsImmediately();
                RefreshBuildFields();
            });

            m_BuildVersionField.RegisterValueChangedCallback(evt =>
            {
                m_Settings.BuildSettings.ManifestVersion = evt.newValue;
                SaveSettingsImmediately();
                RefreshBuildFields();
            });

            m_BuildCompressionDropdown.RegisterValueChangedCallback(evt =>
            {
                m_Settings.BuildSettings.Compression = CompressionFromLabel(evt.newValue);
                SaveSettingsImmediately();
                RefreshBuildFields();
            });
        }

        /// <summary>
        /// 执行 Bind Package List。
        /// </summary>
        private void BindPackageList()
        {
            m_PackageList.itemsSource = m_Settings.Packages;
            m_PackageList.selectionType = SelectionType.Single;
            m_PackageList.fixedItemHeight = 58;
            m_PackageList.makeItem = MakePackageRow;
            m_PackageList.bindItem = BindPackageRow;
            m_PackageList.selectionChanged += _ =>
            {
                var selectedIndex = m_PackageList.selectedIndex;
                if (m_Settings.SelectedPackageIndex != selectedIndex)
                {
                    m_Settings.SelectedPackageIndex = selectedIndex;
                    SaveSettingsImmediately();
                    m_PackageList.RefreshItems();
                }

                RefreshPackageDetail();
            };
        }

        /// <summary>
        /// 执行 Make Package Row。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement MakePackageRow()
        {
            var row = new VisualElement();
            row.AddToClassList("package-row");

            var top = new VisualElement();
            top.AddToClassList("package-row__top");

            var name = new Label { name = "name" };
            name.AddToClassList("package-row__name");
            var badge = new Label { name = "badge" };
            badge.AddToClassList("badge");

            var meta = new Label { name = "meta" };
            meta.AddToClassList("package-row__meta");

            top.Add(name);
            top.Add(badge);
            row.Add(top);
            row.Add(meta);
            row.RegisterCallback<MouseDownEvent>(OnPackageRowMouseDown);
            return row;
        }

        /// <summary>
        /// 执行 Bind Package Row。
        /// </summary>
        /// <param name="element">element 参数。</param>
        /// <param name="index">index 参数。</param>
        private void BindPackageRow(VisualElement element, int index)
        {
            var package = m_Settings.Packages[index];
            var name = element.Q<Label>("name");
            var badge = element.Q<Label>("badge");
            var meta = element.Q<Label>("meta");
            element.userData = index;

            element.EnableInClassList("package-row--selected", index == m_Settings.SelectedPackageIndex);
            name.text = string.IsNullOrWhiteSpace(package.Name) ? "(未命名)" : package.Name;
            badge.text = package.IsHotUpdate ? "热更" : "内置";
            badge.RemoveFromClassList("badge--hot");
            badge.RemoveFromClassList("badge--builtin");
            badge.AddToClassList(package.IsHotUpdate ? "badge--hot" : "badge--builtin");

            var build = m_Registry.GetBuildStrategy(package.BuildStrategyId)?.DisplayName ?? "Missing build";
            meta.text = $"{package.Bundles.Count} bundles · {build}";
        }

        /// <summary>
        /// 处理 Package Row Mouse Down 回调。
        /// </summary>
        /// <param name="evt">evt 参数。</param>
        private void OnPackageRowMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not VisualElement element || element.userData is not int index)
            {
                return;
            }

            if (index < 0 || index >= m_Settings.Packages.Count)
            {
                return;
            }

            m_Settings.SelectedPackageIndex = index;
            SaveSettingsImmediately();
            m_PackageList.SetSelectionWithoutNotify(new[] { index });
            m_PackageList.RefreshItems();
            RefreshPackageDetail();
        }

        /// <summary>
        /// 刷新 All。
        /// </summary>
        private void RefreshAll()
        {
            RefreshDropdowns();
            RefreshPackageList();
            RefreshPackageDetail();
            RefreshPreviewAndIssues();
        }

        /// <summary>
        /// 刷新 Dropdowns。
        /// </summary>
        private void RefreshDropdowns()
        {
            m_PackageModeDropdown.choices = new List<string> { BuiltinModeLabel, HotUpdateModeLabel };
            m_BuildDropdown.choices = m_Registry.BuildStrategies.Select(x => x.DisplayName).ToList();
            m_BuildChannelField.choices = BuildChannelChoices(m_Settings.BuildSettings.Channel);
            m_BuildCompressionDropdown.choices = new List<string> { CompressionDefaultLabel, CompressionLz4Label, CompressionUncompressedLabel };
            RefreshBuildFields();
        }

        /// <summary>
        /// 刷新 Build Fields。
        /// </summary>
        private void RefreshBuildFields()
        {
            var settings = m_Settings.BuildSettings;
            if (m_BuildChannelField.choices.Contains(settings.Channel) is false)
            {
                m_BuildChannelField.choices = BuildChannelChoices(settings.Channel);
            }

            var selectedChannel = string.IsNullOrWhiteSpace(settings.Channel)
                ? m_BuildChannelField.choices.FirstOrDefault()
                : settings.Channel;
            if (string.IsNullOrWhiteSpace(settings.Channel) && string.IsNullOrWhiteSpace(selectedChannel) is false)
            {
                settings.Channel = selectedChannel;
            }

            m_BuildChannelField.SetValueWithoutNotify(selectedChannel);
            SetValueWithoutNotify(m_BuildVersionField, settings.ManifestVersion);
            m_BuildCompressionDropdown.SetValueWithoutNotify(LabelFromCompression(settings.Compression));
        }

        /// <summary>
        /// 刷新 Package List。
        /// </summary>
        private void RefreshPackageList()
        {
            m_PackageList.Rebuild();
            if (m_Settings.SelectedPackageIndex >= 0 && m_Settings.SelectedPackageIndex < m_Settings.Packages.Count)
            {
                m_PackageList.SetSelection(m_Settings.SelectedPackageIndex);
            }
        }

        /// <summary>
        /// 刷新 Package Detail。
        /// </summary>
        private void RefreshPackageDetail()
        {
            var package = GetSelectedPackage();
            var hasPackage = package != null;
            m_EmptyState.style.display = hasPackage ? DisplayStyle.None : DisplayStyle.Flex;
            m_PackageDetail.style.display = hasPackage ? DisplayStyle.Flex : DisplayStyle.None;
            if (package == null)
            {
                return;
            }

            SetValueWithoutNotify(m_PackageNameField, package.Name);
            m_PackageModeDropdown.SetValueWithoutNotify(package.IsHotUpdate ? HotUpdateModeLabel : BuiltinModeLabel);
            m_BuildDropdown.SetValueWithoutNotify(m_Registry.GetBuildStrategy(package.BuildStrategyId)?.DisplayName ?? MissingLabel(package.BuildStrategyId));
            RefreshBundles(package);
        }

        /// <summary>
        /// 执行 Missing Label。
        /// </summary>
        /// <param name="id">id 参数。</param>
        /// <returns>执行结果。</returns>
        private static string MissingLabel(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : $"Missing: {id}";
        }

        /// <summary>
        /// 刷新 Bundles。
        /// </summary>
        /// <param name="package">package 参数。</param>
        private void RefreshBundles(ResourceEditorPackage package)
        {
            m_BundleContainer.Clear();
            foreach (var bundle in package.Bundles)
            {
                m_BundleContainer.Add(CreateBundleElement(package, bundle));
            }
        }

        /// <summary>
        /// 创建 Bundle Element。
        /// </summary>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
        private VisualElement CreateBundleElement(ResourceEditorPackage package, ResourceEditorBundle bundle)
        {
            var previewCount = GetPreview(bundle).Count;
            var collector = GetBundleCollector(package, bundle);
            var card = new Foldout
            {
                text = $"{bundle.Name} · {collector?.DisplayName ?? MissingLabel(bundle.CollectorId)} · {previewCount} assets",
                value = true
            };
            card.AddToClassList("bundle-card");

            var header = new VisualElement();
            header.AddToClassList("bundle-header");
            var title = new Label(bundle.Name);
            title.AddToClassList("bundle-title");
            var remove = new Button(() =>
            {
                package.Bundles.Remove(bundle);
                SaveSettingsImmediately();
                RefreshPreviewAndIssues();
                RefreshPackageDetail();
                RefreshPackageList();
            })
            {
                text = "-"
            };
            remove.AddToClassList("icon-button");
            header.Add(title);
            header.Add(remove);

            var meta = new Label($"Group: {bundle.Group} · Source: {DisplaySourceFolder(bundle.SourceFolder)}");
            meta.AddToClassList("bundle-meta");

            var nameField = CreateDelayedTextField("Bundle");
            nameField.SetValueWithoutNotify(bundle.Name);
            nameField.RegisterValueChangedCallback(evt =>
            {
                bundle.Name = evt.newValue;
                SaveSettingsImmediately();
                RefreshPreviewAndIssues();
                RefreshPackageDetail();
            });

            var groupField = CreateDelayedTextField("Group");
            groupField.SetValueWithoutNotify(bundle.Group);
            groupField.RegisterValueChangedCallback(evt =>
            {
                bundle.Group = evt.newValue;
                SaveSettingsImmediately();
                RefreshPreviewAndIssues();
                RefreshPackageDetail();
            });

            var collectorDropdown = new DropdownField("资源收集器")
            {
                choices = m_Registry.Collectors.Select(x => x.DisplayName).ToList()
            };
            collectorDropdown.SetValueWithoutNotify(collector?.DisplayName ?? MissingLabel(bundle.CollectorId));
            collectorDropdown.RegisterValueChangedCallback(evt =>
            {
                bundle.CollectorId = FindCollectorIdByName(evt.newValue, bundle.CollectorId);
                SaveSettingsImmediately();
                RefreshPreviewAndIssues();
                RefreshPackageList();
            });

            var folderField = new ObjectField("资源目录")
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false
            };
            folderField.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(bundle.SourceFolder));
            folderField.RegisterValueChangedCallback(evt =>
            {
                var path = evt.newValue == null ? string.Empty : AssetDatabase.GetAssetPath(evt.newValue);
                if (string.IsNullOrWhiteSpace(path) is false && AssetDatabase.IsValidFolder(path) is false)
                {
                    folderField.SetValueWithoutNotify(evt.previousValue);
                    EditorUtility.DisplayDialog("目录无效", "请选择 Project 视图中的目录资源。", "确定");
                    return;
                }

                bundle.SourceFolder = path;
                if (string.IsNullOrWhiteSpace(path) is false)
                {
                    bundle.AssetPaths.Clear();
                    bundle.AssetPaths.Add(path);
                }

                SaveSettingsImmediately();
                RefreshPreviewAndIssues();
                RefreshPackageList();
            });

            var browseButton = new Button(() => SelectFolder(folderField, bundle))
            {
                text = "选择"
            };
            browseButton.AddToClassList("small-button");

            var folderRow = new VisualElement();
            folderRow.AddToClassList("folder-row");
            folderRow.Add(folderField);
            folderRow.Add(browseButton);

            var pathsField = CreateDelayedTextField("资源路径", true);
            pathsField.SetValueWithoutNotify(string.Join("\n", bundle.AssetPaths));
            pathsField.RegisterValueChangedCallback(evt =>
            {
                bundle.AssetPaths.Clear();
                bundle.AssetPaths.AddRange(SplitLines(evt.newValue));
                bundle.SourceFolder = bundle.AssetPaths.FirstOrDefault(AssetDatabase.IsValidFolder) ?? bundle.SourceFolder;
                SaveSettingsImmediately();
                RefreshPreviewAndIssues();
                RefreshPackageDetail();
            });

            var showFolderPicker = collector?.Instance is FolderResourceCollector;
            var showAssetPaths = collector?.Instance is ExplicitAssetResourceCollector || collector == null;
            folderRow.style.display = showFolderPicker ? DisplayStyle.Flex : DisplayStyle.None;
            pathsField.style.display = showAssetPaths ? DisplayStyle.Flex : DisplayStyle.None;

            card.Add(header);
            card.Add(meta);
            card.Add(nameField);
            card.Add(groupField);
            card.Add(collectorDropdown);
            card.Add(folderRow);
            card.Add(pathsField);

            var group = new VisualElement();
            group.AddToClassList("group-preview");
            group.Add(new Label(string.IsNullOrWhiteSpace(bundle.Group) ? "Group: (空)" : $"Group: {bundle.Group}"));

            foreach (var preview in GetPreview(bundle))
            {
                var row = new VisualElement();
                row.AddToClassList("preview-row");
                row.Add(new Label(preview.Location) { name = "asset-location" });
                row.Add(new Label(preview.TypeName) { name = "asset-type" });
                row.Add(CreateLabelDropdown(preview));
                group.Add(row);
            }

            card.Add(group);
            return card;
        }

        /// <summary>
        /// 执行 Split Lines。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<string> SplitLines(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => string.IsNullOrWhiteSpace(x) is false);
        }

        /// <summary>
        /// 创建 Label Dropdown。
        /// </summary>
        /// <param name="preview">preview 参数。</param>
        /// <returns>执行结果。</returns>
        private VisualElement CreateLabelDropdown(ResourceGroupPreview preview)
        {
            var button = new Button
            {
                text = FormatLabelDropdownText(preview?.Labels)
            };
            button.AddToClassList("asset-label-dropdown");

            if (preview == null || string.IsNullOrWhiteSpace(preview.AssetPath))
            {
                button.SetEnabled(false);
                return button;
            }

            button.clicked += () => ShowAssetLabelMenu(button, preview);
            return button;
        }

        /// <summary>
        /// 执行 Format Label Dropdown Text。
        /// </summary>
        /// <param name="labels">labels 参数。</param>
        /// <returns>执行结果。</returns>
        private static string FormatLabelDropdownText(IReadOnlyList<string> labels)
        {
            var names = labels?
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();

            if (names.Length == 0)
            {
                return "标签 ▾";
            }

            if (names.Length <= 2)
            {
                return $"{string.Join(", ", names)} ▾";
            }

            return $"{names.Length} 个标签 ▾";
        }

        /// <summary>
        /// 执行 Show Asset Label Menu。
        /// </summary>
        /// <param name="anchor">anchor 参数。</param>
        /// <param name="preview">preview 参数。</param>
        private void ShowAssetLabelMenu(Button anchor, ResourceGroupPreview preview)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(preview.AssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("资源无效", "无法读取该资源的标签。", "确定");
                return;
            }

            var selectedLabels = new HashSet<string>(
                AssetDatabase.GetLabels(asset).Where(x => string.IsNullOrWhiteSpace(x) is false),
                StringComparer.Ordinal);
            var configuredLabels = GetConfiguredAssetTags().ToArray();
            var configuredLabelSet = new HashSet<string>(configuredLabels, StringComparer.Ordinal);
            var menu = new GenericMenu();

            if (configuredLabels.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("标签目录/没有可用资源标签"));
            }
            else
            {
                foreach (var label in configuredLabels)
                {
                    var labelName = label;
                    menu.AddItem(new GUIContent($"标签目录/{labelName}"), selectedLabels.Contains(labelName), () => ToggleAssetLabel(preview.AssetPath, labelName));
                }
            }

            var unregisteredLabels = selectedLabels
                .Where(x => configuredLabelSet.Contains(x) is false)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (unregisteredLabels.Length > 0)
            {
                menu.AddSeparator(string.Empty);
                foreach (var label in unregisteredLabels)
                {
                    var labelName = label;
                    menu.AddItem(new GUIContent($"当前未登记/{labelName}"), true, () => ToggleAssetLabel(preview.AssetPath, labelName));
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("编辑标签..."), false, () => ShowAssetLabelEditor(preview));
            if (selectedLabels.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("清空标签"));
            }
            else
            {
                menu.AddItem(new GUIContent("清空标签"), false, () => SetAssetLabels(preview.AssetPath, Array.Empty<string>()));
            }

            menu.DropDown(anchor.worldBound);
        }

        /// <summary>
        /// 执行 Show Asset Label Editor。
        /// </summary>
        /// <param name="preview">preview 参数。</param>
        private void ShowAssetLabelEditor(ResourceGroupPreview preview)
        {
            if (preview == null || string.IsNullOrWhiteSpace(preview.AssetPath))
            {
                return;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(preview.AssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("资源无效", "无法读取该资源的标签。", "确定");
                return;
            }

            ResourceEditorLabelEditWindow.Open(
                preview.AssetPath,
                AssetDatabase.GetLabels(asset),
                GetConfiguredAssetTags(),
                labels => SetAssetLabels(preview.AssetPath, labels));
        }

        /// <summary>
        /// 获取 Configured Asset Tags。
        /// </summary>
        /// <returns>执行结果。</returns>
        private static IReadOnlyList<string> GetConfiguredAssetTags()
        {
            return ResourceEditorTagCatalogProvider.GetAssetTagKeys();
        }

        /// <summary>
        /// 执行 Toggle Asset Label。
        /// </summary>
        /// <param name="assetPath">asset Path 参数。</param>
        /// <param name="label">label 参数。</param>
        private void ToggleAssetLabel(string assetPath, string label)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            var labels = AssetDatabase.GetLabels(asset)
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .ToList();
            var index = labels.FindIndex(x => string.Equals(x, label, StringComparison.Ordinal));
            if (index >= 0)
            {
                labels.RemoveAt(index);
            }
            else
            {
                labels.Add(label);
            }

            SetAssetLabels(assetPath, labels);
        }

        /// <summary>
        /// 设置 Asset Labels。
        /// </summary>
        /// <param name="assetPath">asset Path 参数。</param>
        /// <param name="labels">labels 参数。</param>
        private void SetAssetLabels(string assetPath, IEnumerable<string> labels)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                return;
            }

            var normalizedLabels = labels?
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();

            AssetDatabase.SetLabels(asset, normalizedLabels);
            AssetDatabase.SaveAssets();
            RefreshPreviewAndIssues();
        }

        /// <summary>
        /// 刷新 Preview And Issues。
        /// </summary>
        private void RefreshPreviewAndIssues()
        {
            m_Previews.Clear();
            m_Issues = new List<ResourceValidationIssue>();

            foreach (var error in m_Registry.Errors)
            {
                m_Issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, "Registry", error));
            }

            foreach (var package in m_Settings.Packages)
            {
                if (package == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(package.BuildStrategyId) is false && m_Registry.GetBuildStrategy(package.BuildStrategyId) == null)
                {
                    m_Issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, "Registry", MissingLabel(package.BuildStrategyId), package));
                }

                foreach (var bundle in package.Bundles)
                {
                    var collectorDescriptor = GetBundleCollector(package, bundle);
                    if (collectorDescriptor == null)
                    {
                        m_Issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, "Registry", MissingLabel(bundle.CollectorId), package, bundle));
                    }

                    var collector = collectorDescriptor?.Instance;
                    var resources = collector?.Collect(package, bundle)?.ToList() ?? new List<ResourceGroupPreview>();
                    m_Previews[bundle] = resources;
                }
            }

            foreach (var package in m_Settings.Packages)
            {
                if (package == null)
                {
                    continue;
                }

                foreach (var bundle in package.Bundles)
                {
                    var resources = GetPreview(bundle);
                    var context = new ResourceCheckContext(m_Settings, package, bundle, resources, m_Previews);
                    foreach (var checker in m_Registry.Checkers)
                    {
                        checker.Instance.Check(context, m_Issues);
                    }
                }
            }

            RefreshPackageDetail();
        }

        /// <summary>
        /// 获取 Preview。
        /// </summary>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
        private IReadOnlyList<ResourceGroupPreview> GetPreview(ResourceEditorBundle bundle)
        {
            return bundle != null && m_Previews.TryGetValue(bundle, out var preview) ? preview : Array.Empty<ResourceGroupPreview>();
        }

        /// <summary>
        /// 执行 Issue Target。
        /// </summary>
        /// <param name="issue">issue 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        internal static string IssueTarget(ResourceValidationIssue issue)
        {
            if (issue.Resource != null)
            {
                return $" · {issue.Resource.Location}";
            }

            if (issue.Bundle != null)
            {
                return $" · {issue.Bundle.Name}";
            }

            if (issue.Package != null)
            {
                return $" · {issue.Package.Name}";
            }

            return string.Empty;
        }

        /// <summary>
        /// 处理 Issue Selection Changed 回调。
        /// </summary>
        /// <param name="selection">selection 参数。</param>
        private void OnIssueSelectionChanged(IEnumerable<object> selection)
        {
            var issue = selection.OfType<ResourceValidationIssue>().FirstOrDefault();
            if (issue == null)
            {
                return;
            }

            if (issue.Package != null)
            {
                var index = m_Settings.Packages.IndexOf(issue.Package);
                if (index >= 0)
                {
                    m_Settings.SelectedPackageIndex = index;
                    m_PackageList.SetSelection(index);
                    RefreshPackageDetail();
                }
            }

            if (issue.Resource == null || string.IsNullOrWhiteSpace(issue.Resource.AssetPath))
            {
                return;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(issue.Resource.AssetPath);
            if (asset == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>
        /// 执行 Show Check Result Window。
        /// </summary>
        private void ShowCheckResultWindow()
        {
            RefreshPreviewAndIssues();
            ResourceEditorCheckResultWindow.Open(m_Issues, SelectIssue);
        }

        /// <summary>
        /// 构建 All Packages。
        /// </summary>
        private void BuildAllPackages()
        {
            BuildResources(ResourceBuildScope.AllPackages);
        }

        /// <summary>
        /// 构建 Selected Package。
        /// </summary>
        private void BuildSelectedPackage()
        {
            BuildResources(ResourceBuildScope.SelectedPackage);
        }

        /// <summary>
        /// 构建 Resources。
        /// </summary>
        /// <param name="scope">scope 参数。</param>
        private void BuildResources(ResourceBuildScope scope)
        {
            SaveSettingsImmediately();
            RefreshPreviewAndIssues();
            if (HasBlockingIssues())
            {
                ShowCheckResultWindow();
                return;
            }

            var workflow = new ResourceBuildWorkflow(m_Settings, m_Registry, () => m_Previews, CreateBuildSettings(scope));
            var result = workflow.Build(out _);
            ResourceBuildPublishResultWindow.OpenBuildResult(result);
        }

        /// <summary>
        /// 创建 Build Settings。
        /// </summary>
        /// <param name="scope">scope 参数。</param>
        /// <returns>执行结果。</returns>
        private ResourceBuildSettings CreateBuildSettings(ResourceBuildScope scope)
        {
            var source = m_Settings.BuildSettings;
            var settings = new ResourceBuildSettings
            {
                OutputRoot = source.OutputRoot,
                Target = source.Target,
                Channel = source.Channel,
                CleanOutput = source.CleanOutput,
                Compression = source.Compression,
                ManifestFileName = source.ManifestFileName,
                ManifestVersion = source.ManifestVersion,
                Scope = scope
            };
            return settings;
        }

        /// <summary>
        /// 查询是否存在 Blocking Issues。
        /// </summary>
        /// <returns>条件满足时返回 true。</returns>
        private bool HasBlockingIssues()
        {
            return m_Issues.Any(issue => issue.Severity == ResourceValidationSeverity.Error);
        }

        /// <summary>
        /// 添加 Package。
        /// </summary>
        private void AddPackage()
        {
            var package = new ResourceEditorPackage
            {
                Name = $"Package{m_Settings.Packages.Count + 1}",
                CollectorId = m_Registry.Collectors.FirstOrDefault()?.Id,
                BuildStrategyId = m_Registry.BuildStrategies.FirstOrDefault()?.Id
            };
            package.EnsureDefaults();
            m_Settings.Packages.Add(package);
            m_Settings.SelectedPackageIndex = m_Settings.Packages.Count - 1;
            SaveSettingsImmediately();
            RefreshAll();
        }

        /// <summary>
        /// 移除 Selected Package。
        /// </summary>
        private void RemoveSelectedPackage()
        {
            var package = GetSelectedPackage();
            if (package == null)
            {
                return;
            }

            m_Settings.Packages.Remove(package);
            m_Settings.SelectedPackageIndex = Math.Min(m_Settings.SelectedPackageIndex, m_Settings.Packages.Count - 1);
            SaveSettingsImmediately();
            RefreshAll();
        }

        /// <summary>
        /// 添加 Bundle。
        /// </summary>
        private void AddBundle()
        {
            var package = GetSelectedPackage();
            if (package == null)
            {
                return;
            }

            var bundle = new ResourceEditorBundle
            {
                Name = $"Bundle{package.Bundles.Count + 1}",
                CollectorId = m_Registry.Collectors.FirstOrDefault()?.Id
            };
            bundle.EnsureDefaults();
            package.Bundles.Add(bundle);
            SaveSettingsImmediately();
            RefreshPreviewAndIssues();
            RefreshPackageList();
        }

        /// <summary>
        /// 获取 Selected Package。
        /// </summary>
        /// <returns>执行结果。</returns>
        private ResourceEditorPackage GetSelectedPackage()
        {
            if (m_Settings.SelectedPackageIndex < 0 || m_Settings.SelectedPackageIndex >= m_Settings.Packages.Count)
            {
                return null;
            }

            return m_Settings.Packages[m_Settings.SelectedPackageIndex];
        }

        /// <summary>
        /// 查找 Collector Id By Name。
        /// </summary>
        /// <param name="displayName">display Name 参数。</param>
        /// <param name="fallbackId">fallback Id 参数。</param>
        /// <returns>执行结果。</returns>
        private string FindCollectorIdByName(string displayName, string fallbackId)
        {
            return m_Registry.Collectors.FirstOrDefault(x => x.DisplayName == displayName)?.Id ?? fallbackId;
        }

        /// <summary>
        /// 查找 Build Id By Name。
        /// </summary>
        /// <param name="displayName">display Name 参数。</param>
        /// <returns>执行结果。</returns>
        private string FindBuildIdByName(string displayName)
        {
            return m_Registry.BuildStrategies.FirstOrDefault(x => x.DisplayName == displayName)?.Id ?? GetSelectedPackage()?.BuildStrategyId;
        }

        /// <summary>
        /// 保存 Settings Immediately。
        /// </summary>
        private void SaveSettingsImmediately()
        {
            m_Settings.SaveSettings();
            hasUnsavedChanges = false;
        }

        /// <summary>
        /// 创建 Delayed Text Field。
        /// </summary>
        /// <param name="label">label 参数。</param>
        /// <param name="multiline">multiline 参数。</param>
        /// <returns>执行结果。</returns>
        private static TextField CreateDelayedTextField(string label, bool multiline = false)
        {
            return new TextField(label)
            {
                isDelayed = true,
                multiline = multiline
            };
        }

        /// <summary>
        /// 设置 Value Without Notify。
        /// </summary>
        /// <param name="field">field 参数。</param>
        /// <param name="value">value 参数。</param>
        private static void SetValueWithoutNotify(TextField field, string value)
        {
            field.SetValueWithoutNotify(value ?? string.Empty);
        }

        /// <summary>
        /// 获取 Bundle Collector。
        /// </summary>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
        private ResourceCollectorDescriptor GetBundleCollector(ResourceEditorPackage package, ResourceEditorBundle bundle)
        {
            return m_Registry.GetCollector(bundle.CollectorId) ?? m_Registry.GetCollector(package.CollectorId);
        }

        /// <summary>
        /// 执行 Select Folder。
        /// </summary>
        /// <param name="folderField">folder Field 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        private void SelectFolder(ObjectField folderField, ResourceEditorBundle bundle)
        {
            var absolute = EditorUtility.OpenFolderPanel("选择资源目录", "Assets", string.Empty);
            if (string.IsNullOrWhiteSpace(absolute))
            {
                return;
            }

            var projectPath = Application.dataPath.Replace('\\', '/');
            absolute = absolute.Replace('\\', '/');
            if (absolute.StartsWith(projectPath) is false)
            {
                EditorUtility.DisplayDialog("目录无效", "请选择当前项目 Assets 目录下的文件夹。", "确定");
                return;
            }

            var assetPath = "Assets" + absolute.Substring(projectPath.Length);
            if (AssetDatabase.IsValidFolder(assetPath) is false)
            {
                EditorUtility.DisplayDialog("目录无效", "请选择 Project 视图中的目录资源。", "确定");
                return;
            }

            bundle.SourceFolder = assetPath;
            bundle.AssetPaths.Clear();
            bundle.AssetPaths.Add(assetPath);
            folderField.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetPath));
            SaveSettingsImmediately();
            RefreshPreviewAndIssues();
            RefreshPackageList();
        }

        /// <summary>
        /// 执行 Display Source Folder。
        /// </summary>
        /// <param name="sourceFolder">source Folder 参数。</param>
        /// <returns>执行结果。</returns>
        private static string DisplaySourceFolder(string sourceFolder)
        {
            return string.IsNullOrWhiteSpace(sourceFolder) ? "(未选择)" : sourceFolder;
        }

        /// <summary>
        /// 执行 Label From Compression。
        /// </summary>
        /// <param name="compression">compression 参数。</param>
        /// <returns>执行结果。</returns>
        private static string LabelFromCompression(ResourceBuildCompression compression)
        {
            switch (compression)
            {
                case ResourceBuildCompression.Uncompressed:
                    return CompressionUncompressedLabel;
                case ResourceBuildCompression.Lz4:
                    return CompressionLz4Label;
                default:
                    return CompressionDefaultLabel;
            }
        }

        /// <summary>
        /// 执行 Compression From Label。
        /// </summary>
        /// <param name="label">label 参数。</param>
        /// <returns>执行结果。</returns>
        private static ResourceBuildCompression CompressionFromLabel(string label)
        {
            switch (label)
            {
                case CompressionUncompressedLabel:
                    return ResourceBuildCompression.Uncompressed;
                case CompressionLz4Label:
                    return ResourceBuildCompression.Lz4;
                default:
                    return ResourceBuildCompression.Default;
            }
        }

        /// <summary>
        /// 构建 Channel Choices。
        /// </summary>
        /// <param name="currentChannel">current Channel 参数。</param>
        /// <returns>执行结果。</returns>
        private static List<string> BuildChannelChoices(string currentChannel)
        {
            var choices = ResourcePublisherSettings.LoadOrCreate().Channels
                .Where(channel => channel != null && string.IsNullOrWhiteSpace(channel.ChannelName) is false)
                .Select(channel => channel.ChannelName.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(channel => channel, StringComparer.Ordinal)
                .ToList();

            if (string.IsNullOrWhiteSpace(currentChannel) is false && choices.Contains(currentChannel) is false)
            {
                choices.Insert(0, currentChannel);
            }

            if (choices.Count == 0)
            {
                choices.Add("dev");
            }

            return choices;
        }

        /// <summary>
        /// 执行 Select Issue。
        /// </summary>
        /// <param name="issue">issue 参数。</param>
        private void SelectIssue(ResourceValidationIssue issue)
        {
            OnIssueSelectionChanged(new[] { issue });
        }

        /// <summary>
        /// 定义 Resource Editor Label Edit Window 类型。
        /// </summary>
        private sealed class ResourceEditorLabelEditWindow : EditorWindow
        {
            /// <summary>             /// 存储 Labels。             /// </summary>
            private readonly List<string> m_Labels = new List<string>();
            /// <summary>             /// 存储 Candidates。             /// </summary>
            private readonly List<string> m_Candidates = new List<string>();
            /// <summary>
            /// 存储 Asset Path。
            /// </summary>
            private string m_AssetPath;
            /// <summary>
            /// 存储 On Apply。
            /// </summary>
            private Action<IEnumerable<string>> m_OnApply;
            /// <summary>
            /// 存储 Add Field。
            /// </summary>
            private TextField m_AddField;
            /// <summary>
            /// 存储 Candidate Container。
            /// </summary>
            private VisualElement m_CandidateContainer;
            /// <summary>
            /// 存储 Label Container。
            /// </summary>
            private VisualElement m_LabelContainer;

            /// <summary>
            /// 执行 Open。
            /// </summary>
            /// <param name="assetPath">asset Path 参数。</param>
            /// <param name="labels">labels 参数。</param>
            /// <param name="candidates">candidates 参数。</param>
            /// <param name="onApply">on Apply 参数。</param>
            public static void Open(string assetPath, IEnumerable<string> labels, IEnumerable<string> candidates, Action<IEnumerable<string>> onApply)
            {
                var window = GetWindow<ResourceEditorLabelEditWindow>(true, "编辑资源标签");
                window.minSize = new Vector2(420, 260);
                window.m_AssetPath = assetPath;
                window.m_OnApply = onApply;
                window.m_Labels.Clear();
                window.m_Labels.AddRange(NormalizeLabels(labels));
                window.m_Candidates.Clear();
                window.m_Candidates.AddRange(NormalizeLabels(candidates));
                window.ShowUtility();
                window.Render();
            }

            /// <summary>
            /// 创建 GUI。
            /// </summary>
            public void CreateGUI()
            {
                Render();
            }

            /// <summary>
            /// 渲染 member。
            /// </summary>
            private void Render()
            {
                if (rootVisualElement == null)
                {
                    return;
                }

                rootVisualElement.Clear();
                rootVisualElement.AddToClassList("label-editor-window");
                rootVisualElement.EnableInClassList("resource-editor--dark", EditorGUIUtility.isProSkin);
                rootVisualElement.EnableInClassList("resource-editor--light", EditorGUIUtility.isProSkin is false);

                var styleSheet = GameDeveloperKitEditorPaths.LoadPackageAsset<StyleSheet>(UxmlPath.Replace(".uxml", ".uss"));
                if (styleSheet != null)
                {
                    rootVisualElement.styleSheets.Add(styleSheet);
                }

                var title = new Label("资源标签");
                title.AddToClassList("label-editor-title");
                rootVisualElement.Add(title);

                var path = new Label(m_AssetPath);
                path.AddToClassList("label-editor-path");
                rootVisualElement.Add(path);

                var addRow = new VisualElement();
                addRow.AddToClassList("label-editor-add-row");
                m_AddField = new TextField("新增标签");
                m_AddField.AddToClassList("label-editor-add-field");
                addRow.Add(m_AddField);
                var addButton = new Button(AddLabelsFromField) { text = "添加" };
                addButton.AddToClassList("small-button");
                addRow.Add(addButton);
                rootVisualElement.Add(addRow);

                m_CandidateContainer = new VisualElement();
                m_CandidateContainer.AddToClassList("label-editor-candidates");
                rootVisualElement.Add(m_CandidateContainer);
                RefreshCandidateRows();

                m_LabelContainer = new VisualElement();
                m_LabelContainer.AddToClassList("label-editor-list");
                rootVisualElement.Add(m_LabelContainer);
                RefreshLabelRows();

                var footer = new VisualElement();
                footer.AddToClassList("label-editor-footer");
                var cancel = new Button(Close) { text = "取消" };
                cancel.AddToClassList("small-button");
                var apply = new Button(Apply) { text = "应用" };
                apply.AddToClassList("toolbar-button");
                apply.AddToClassList("toolbar-button--primary");
                footer.Add(cancel);
                footer.Add(apply);
                rootVisualElement.Add(footer);
            }

            /// <summary>
            /// 刷新 Candidate Rows。
            /// </summary>
            private void RefreshCandidateRows()
            {
                m_CandidateContainer.Clear();
                if (m_Candidates.Count == 0)
                {
                    return;
                }

                var title = new Label("标签目录");
                title.AddToClassList("label-editor-section-title");
                m_CandidateContainer.Add(title);

                var chips = new VisualElement();
                chips.AddToClassList("label-editor-chip-list");
                foreach (var candidate in m_Candidates)
                {
                    var labelName = candidate;
                    var button = new Button(() => AddLabel(labelName))
                    {
                        text = labelName
                    };
                    button.AddToClassList("label-editor-chip");
                    button.EnableInClassList("label-editor-chip--selected", HasLabel(labelName));
                    chips.Add(button);
                }

                m_CandidateContainer.Add(chips);
            }

            /// <summary>
            /// 刷新 Label Rows。
            /// </summary>
            private void RefreshLabelRows()
            {
                m_LabelContainer.Clear();
                if (m_Labels.Count == 0)
                {
                    var empty = new Label("没有标签");
                    empty.AddToClassList("label-editor-empty");
                    m_LabelContainer.Add(empty);
                    return;
                }

                foreach (var label in m_Labels.ToArray())
                {
                    var labelName = label;
                    var row = new VisualElement();
                    row.AddToClassList("label-editor-row");
                    var name = new Label(labelName);
                    name.AddToClassList("label-editor-row-name");
                    var remove = new Button(() =>
                    {
                        m_Labels.Remove(labelName);
                        RefreshCandidateRows();
                        RefreshLabelRows();
                    })
                    {
                        text = "-"
                    };
                    remove.AddToClassList("icon-button");
                    row.Add(name);
                    row.Add(remove);
                    m_LabelContainer.Add(row);
                }
            }

            /// <summary>
            /// 添加 Labels From Field。
            /// </summary>
            private void AddLabelsFromField()
            {
                foreach (var label in SplitLabels(m_AddField.value))
                {
                    AddLabel(label, false);
                }

                m_Labels.Sort(StringComparer.Ordinal);
                m_AddField.SetValueWithoutNotify(string.Empty);
                RefreshCandidateRows();
                RefreshLabelRows();
            }

            /// <summary>
            /// 添加 Label。
            /// </summary>
            /// <param name="label">label 参数。</param>
            private void AddLabel(string label)
            {
                if (AddLabel(label, true))
                {
                    RefreshCandidateRows();
                    RefreshLabelRows();
                }
            }

            /// <summary>
            /// 添加 Label。
            /// </summary>
            /// <param name="label">label 参数。</param>
            /// <param name="sort">sort 参数。</param>
            /// <returns>条件满足时返回 true。</returns>
            private bool AddLabel(string label, bool sort)
            {
                if (string.IsNullOrWhiteSpace(label) || HasLabel(label))
                {
                    return false;
                }

                m_Labels.Add(label.Trim());
                if (sort)
                {
                    m_Labels.Sort(StringComparer.Ordinal);
                }

                return true;
            }

            /// <summary>
            /// 查询是否存在 Label。
            /// </summary>
            /// <param name="label">label 参数。</param>
            /// <returns>条件满足时返回 true。</returns>
            private bool HasLabel(string label)
            {
                return m_Labels.Any(x => string.Equals(x, label, StringComparison.Ordinal));
            }

            /// <summary>
            /// 执行 Apply。
            /// </summary>
            private void Apply()
            {
                m_OnApply?.Invoke(m_Labels);
                Close();
            }

            /// <summary>
            /// 执行 Normalize Labels。
            /// </summary>
            /// <param name="labels">labels 参数。</param>
            /// <returns>执行结果。</returns>
            private static IEnumerable<string> NormalizeLabels(IEnumerable<string> labels)
            {
                return SplitLabels(string.Join("\n", labels ?? Array.Empty<string>()))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal);
            }

            /// <summary>
            /// 执行 Split Labels。
            /// </summary>
            /// <param name="value">value 参数。</param>
            /// <returns>执行结果。</returns>
            private static IEnumerable<string> SplitLabels(string value)
            {
                return (value ?? string.Empty)
                    .Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => string.IsNullOrWhiteSpace(x) is false);
            }
        }
    }
}

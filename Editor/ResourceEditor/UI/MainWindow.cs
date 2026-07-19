using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using GameDeveloperKit.TagEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UIElements;

namespace GameDeveloperKit.ResourceEditor.UI
{
    /// <summary>
    /// 定义 Resource Editor Window 类型。
    /// </summary>
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.ResourceEditor", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "ResourceEditorWindow")]
    public sealed class MainWindow : EditorWindow
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
        private GameDeveloperKit.ResourceEditor.Authoring.Settings m_Settings;
        /// <summary>
        /// 存储 Registry。
        /// </summary>
        private GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry m_Registry;
        private ApplicationService m_Application;
        /// <summary>         /// 存储 Issues。         /// </summary>
        private List<GameDeveloperKit.ResourceEditor.Validation.Issue> m_Issues = new List<GameDeveloperKit.ResourceEditor.Validation.Issue>();
        /// <summary>         /// 存储 Previews。         /// </summary>
        private ApplicationState m_ApplicationState;

        private readonly HashSet<GameDeveloperKit.ResourceEditor.Authoring.Bundle> m_CollapsedBundles = new HashSet<GameDeveloperKit.ResourceEditor.Authoring.Bundle>();

        private readonly HashSet<GameDeveloperKit.ResourceEditor.Authoring.Bundle> m_CollapsedIgnoreLists = new HashSet<GameDeveloperKit.ResourceEditor.Authoring.Bundle>();

        private readonly HashSet<GameDeveloperKit.ResourceEditor.Authoring.Bundle> m_CollapsedResourceLists = new HashSet<GameDeveloperKit.ResourceEditor.Authoring.Bundle>();

        private GameDeveloperKit.ResourceEditor.Authoring.Bundle m_SelectedBundle;

        private VisualElement m_GroupTable;

        private VisualElement m_EmptyState;

        private TextField m_SearchField;

        /// <summary>
        /// 存储 Build Channel Button。
        /// </summary>
        private Button m_BuildChannelButton;
        /// <summary>
        /// 存储 Build Version Field。
        /// </summary>
        private TextField m_BuildVersionField;
        /// <summary>
        /// 存储 Build Compression Dropdown。
        /// </summary>
        private DropdownField m_BuildCompressionDropdown;
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
            var window = GetWindow<MainWindow>();
            window.titleContent = new UnityEngine.GUIContent(WindowTitle);
            window.minSize = new UnityEngine.Vector2(920, 560);
            window.CreateGUI();
            window.Show();
        }

        /// <summary>
        /// 创建 GUI。
        /// </summary>
        public void CreateGUI()
        {
            m_Settings = GameDeveloperKit.ResourceEditor.Authoring.Settings.LoadOrCreate();
            m_Registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistryCache.Current ?? GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistryCache.Refresh();
            m_Application = new ApplicationService(m_Settings, m_Registry);

            var visualTree = GameDeveloperKitEditorPaths.LoadPackageAsset<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                rootVisualElement.Add(new Label($"Missing UXML: {GameDeveloperKitEditorPaths.PackageAssetPath(UxmlPath)}"));
                return;
            }

            rootVisualElement.Clear();
            visualTree.CloneTree(rootVisualElement);
            ApplyEditorTheme();
            ApplyStableInlineLayout();
            QueryElements();
            BindToolbar();
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

        private void ApplyStableInlineLayout()
        {
            var root = rootVisualElement.Q<VisualElement>(className: "resource-editor");
            if (root != null)
            {
                root.style.flexGrow = 1;
            }

            var toolbar = rootVisualElement.Q<VisualElement>(className: "addressables-toolbar");
            if (toolbar != null)
            {
                toolbar.style.flexDirection = FlexDirection.Row;
                toolbar.style.alignItems = Align.Center;
                toolbar.style.minHeight = 30;
                toolbar.style.maxHeight = 30;
            }

            var title = rootVisualElement.Q<Label>(className: "addressables-toolbar__title");
            if (title != null)
            {
                title.style.width = 142;
                title.style.minWidth = 142;
                title.style.maxWidth = 142;
            }

            ApplyToolbarLabelLayout(rootVisualElement.Q<Label>(className: "addressables-toolbar__profile-label"), 48, 0, 6);
            ApplyToolbarFieldLayout(rootVisualElement.Q<TextField>("build-version-field"), 96, 80, 116, false);
            ApplyToolbarFieldLayout(rootVisualElement.Q<DropdownField>("build-compression-dropdown"), 118, 100, 136, false);
            ApplyToolbarLabelLayout(rootVisualElement.Q<Label>(className: "toolbar-version-label"), 48, 24, 5);
            ApplyToolbarLabelLayout(rootVisualElement.Q<Label>(className: "toolbar-compression-label"), 78, 18, 5);

            var compression = rootVisualElement.Q<DropdownField>("build-compression-dropdown");
            if (compression != null)
            {
                compression.style.marginRight = 22;
            }

            var search = rootVisualElement.Q<TextField>("search-field");
            ApplyToolbarFieldLayout(search, 360, 220, 440, false);
            if (search != null)
            {
                search.style.flexGrow = 0;
                search.style.flexShrink = 1;
            }

            foreach (var button in rootVisualElement.Query<Button>(className: "toolbar-menu-button").ToList())
            {
                button.style.minWidth = 62;
                button.style.maxWidth = 86;
                button.style.minHeight = 22;
                button.style.maxHeight = 22;
            }

            foreach (var button in rootVisualElement.Query<Button>(className: "toolbar-channel-button").ToList())
            {
                button.style.width = 185;
                button.style.minWidth = 150;
                button.style.maxWidth = 240;
                button.style.minHeight = 22;
                button.style.maxHeight = 22;
                button.style.marginRight = 24;
            }

            foreach (var button in rootVisualElement.Query<Button>(className: "toolbar-icon-button").ToList())
            {
                button.style.width = 24;
                button.style.minWidth = 24;
                button.style.maxWidth = 24;
                button.style.minHeight = 22;
                button.style.maxHeight = 22;
            }

            foreach (var row in rootVisualElement.Query<VisualElement>(className: "addressables-toolbar__main").ToList())
            {
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
            }

            foreach (var row in rootVisualElement.Query<VisualElement>(className: "addressables-toolbar__settings").ToList())
            {
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
            }

            foreach (var row in rootVisualElement.Query<VisualElement>(className: "addressables-toolbar__actions").ToList())
            {
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
            }

            foreach (var row in rootVisualElement.Query<VisualElement>(className: "addressables-table__header").ToList())
            {
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
            }

            ApplyColumnLayout(rootVisualElement.Q<VisualElement>("group-name-column"), 360, 280, 420, false);
            ApplyColumnLayout(rootVisualElement.Q<VisualElement>("icon-column"), 34, 34, 34, false);
            ApplyColumnLayout(rootVisualElement.Q<VisualElement>("path-column"), null, 300, null, true);
            ApplyColumnLayout(rootVisualElement.Q<VisualElement>("labels-column"), 152, 132, 172, false);
            ApplyColumnLayout(rootVisualElement.Q<VisualElement>("actions-column"), 44, 44, 44, false);
        }

        private static void ApplyToolbarFieldLayout(BaseField<string> field, int? width, int? minWidth, int? maxWidth, bool showLabel)
        {
            if (field == null)
            {
                return;
            }

            if (width.HasValue)
            {
                field.style.width = width.Value;
            }

            if (minWidth.HasValue)
            {
                field.style.minWidth = minWidth.Value;
            }

            if (maxWidth.HasValue)
            {
                field.style.maxWidth = maxWidth.Value;
            }

            field.style.minHeight = 22;
            field.style.maxHeight = 22;
            field.labelElement.style.display = showLabel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void ApplyToolbarLabelLayout(Label label, int width, int marginLeft, int marginRight)
        {
            if (label == null)
            {
                return;
            }

            label.style.width = width;
            label.style.minWidth = width;
            label.style.maxWidth = width;
            label.style.marginLeft = marginLeft;
            label.style.marginRight = marginRight;
        }

        private static void HideToolbarElement(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.style.display = DisplayStyle.None;
            element.style.width = 0;
            element.style.minWidth = 0;
            element.style.maxWidth = 0;
            element.style.minHeight = 0;
            element.style.maxHeight = 0;
            element.style.marginLeft = 0;
            element.style.marginRight = 0;
            element.style.paddingLeft = 0;
            element.style.paddingRight = 0;
        }

        /// <summary>
        /// 执行 Query Elements。
        /// </summary>
        private void QueryElements()
        {
            m_GroupTable = rootVisualElement.Q<VisualElement>("group-table");
            m_EmptyState = rootVisualElement.Q<VisualElement>("empty-state");
            m_SearchField = rootVisualElement.Q<TextField>("search-field");
            m_BuildChannelButton = rootVisualElement.Q<Button>("build-channel-button");
            m_BuildVersionField = rootVisualElement.Q<TextField>("build-version-field");
            m_BuildCompressionDropdown = rootVisualElement.Q<DropdownField>("build-compression-dropdown");
            m_BuildVersionField ??= new TextField();
            m_BuildCompressionDropdown ??= new DropdownField();
            m_BuildVersionField.isDelayed = true;
            m_SearchField.isDelayed = false;
        }

        /// <summary>
        /// 执行 Bind Toolbar。
        /// </summary>
        private void BindToolbar()
        {
            var newMenuButton = rootVisualElement.Q<Button>("new-menu-button");
            var toolsMenuButton = rootVisualElement.Q<Button>("tools-menu-button");
            var buildMenuButton = rootVisualElement.Q<Button>("build-menu-button");
            newMenuButton.clicked += () => ShowNewMenu(newMenuButton);
            toolsMenuButton.clicked += () => ShowToolsMenu(toolsMenuButton);
            buildMenuButton.clicked += () => ShowBuildMenu(buildMenuButton);

            BindBuildSettings();

            m_SearchField.RegisterValueChangedCallback(_ => RefreshGroupTable());
        }

        private void ShowNewMenu(Button anchor)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Package"), false, AddPackage);
            var selectedPackage = GetSelectedPackage() ?? GetLocalPackage();
            if (selectedPackage == null)
            {
                menu.AddDisabledItem(new GUIContent("Group"));
            }
            else
            {
                menu.AddItem(new GUIContent($"Group In Selected Package/{selectedPackage.Name}"), false, AddBundle);
            }

            menu.DropDown(anchor.worldBound);
        }

        private void ShowToolsMenu(Button anchor)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Check"), false, ShowCheckResultWindow);
            menu.AddItem(new GUIContent("Sync Resources to BUILTIN/Resources"), false, SyncBuiltinResources);
            menu.DropDown(anchor.worldBound);
        }

        private void ShowChannelSelectionMenu(Button anchor)
        {
            var channels = GetConfiguredChannelNames();
            var selected = ParseChannelSelection(m_Settings.BuildSettings.Channel, channels);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Everything"), selected.Count == channels.Count && channels.Count > 0, () => SetSelectedChannels(channels));
            menu.AddItem(new GUIContent("Nothing"), selected.Count == 0, () => SetSelectedChannels(Array.Empty<string>()));
            menu.AddSeparator(string.Empty);

            foreach (var channel in channels)
            {
                var channelName = channel;
                menu.AddItem(new GUIContent(channelName), selected.Contains(channelName), () =>
                {
                    var next = new HashSet<string>(selected, StringComparer.Ordinal);
                    if (next.Contains(channelName))
                    {
                        next.Remove(channelName);
                    }
                    else
                    {
                        next.Add(channelName);
                    }

                    SetSelectedChannels(channels.Where(next.Contains));
                });
            }

            menu.DropDown(anchor.worldBound);
        }

        private void SetSelectedChannels(IEnumerable<string> channels)
        {
            var selectedChannels = channels?.ToList() ?? new List<string>();
            m_Settings.BuildSettings.Channel = selectedChannels.Count == 0
                ? GameDeveloperKit.ResourceEditor.Build.Settings.NoChannelSelection
                : SerializeChannelSelection(selectedChannels);
            SaveSettingsImmediately();
            RefreshBuildFields();
        }

        private void ShowBuildMenu(Button anchor)
        {
            var menu = new GenericMenu();
            if (GetSelectedPackage() == null)
            {
                menu.AddDisabledItem(new GUIContent("Build Selected Package"));
            }
            else
            {
                menu.AddItem(new GUIContent("Build Selected Package"), false, BuildSelectedPackage);
            }

            menu.AddItem(new GUIContent("Build All Packages"), false, BuildAllPackages);
            menu.AddItem(new GUIContent("Build Hot Update Packages"), false, BuildHotUpdatePackages);
            menu.DropDown(anchor.worldBound);
        }

        /// <summary>
        /// 执行 Bind Build Settings。
        /// </summary>
        private void BindBuildSettings()
        {
            m_BuildChannelButton.clicked += () => ShowChannelSelectionMenu(m_BuildChannelButton);

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
        /// 刷新 All。
        /// </summary>
        private void RefreshAll()
        {
            RefreshDropdowns();
            RefreshPreviewAndIssues();
        }

        /// <summary>
        /// 刷新 Dropdowns。
        /// </summary>
        private void RefreshDropdowns()
        {
            m_BuildCompressionDropdown.choices = new List<string> { CompressionDefaultLabel, CompressionLz4Label, CompressionUncompressedLabel };
            RefreshBuildFields();
        }

        /// <summary>
        /// 刷新 Build Fields。
        /// </summary>
        private void RefreshBuildFields()
        {
            var settings = m_Settings.BuildSettings;
            settings.Channel = NormalizeChannelSelection(settings.Channel, GetConfiguredChannelNames());
            m_BuildChannelButton.text = FormatChannelSelectionText(settings.Channel);
            SetValueWithoutNotify(m_BuildVersionField, settings.ManifestVersion);
            m_BuildCompressionDropdown.SetValueWithoutNotify(LabelFromCompression(settings.Compression));
        }

        private void RefreshGroupTable()
        {
            if (m_GroupTable == null)
            {
                return;
            }

            EnsureSelectedBundle();
            m_GroupTable.Clear();
            var query = NormalizeSearchQuery();
            var hasVisibleGroup = false;

            foreach (var package in m_Settings.Packages.Where(package => package != null))
            {
                var visibleGroups = package.Bundles
                    .Where(bundle => bundle != null)
                    .Select(bundle => new VisibleGroup(package, bundle, GetVisibleEntries(package, bundle, query), GetExcludedEntries(package, bundle, query)))
                    .Where(group => ShouldShowGroup(group.Package, group.Bundle, group.Entries, group.ExcludedEntries, query))
                    .ToList();
                if (visibleGroups.Count == 0 && ShouldShowPackage(package, query) is false)
                {
                    continue;
                }

                hasVisibleGroup = true;
                m_GroupTable.Add(CreatePackageRow(package, visibleGroups.Count));

                foreach (var group in visibleGroups)
                {
                    m_GroupTable.Add(CreateGroupRow(group.Package, group.Bundle, group.Entries.Count));
                    if (m_CollapsedBundles.Contains(group.Bundle))
                    {
                        continue;
                    }

                    AppendIgnoreListSection(group);
                    AppendResourceListSection(group);
                }
            }

            m_EmptyState.style.display = hasVisibleGroup ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private VisualElement CreatePackageRow(GameDeveloperKit.ResourceEditor.Authoring.Package package, int visibleGroupCount)
        {
            var row = CreateTableRow("package-row");
            row.RegisterCallback<ContextClickEvent>(evt =>
            {
                SelectPackage(package, false);
                ShowPackageContextMenu(package);
                evt.StopPropagation();
            });

            var nameCell = CreateCell("group-name-column", "package-name-cell");
            var spacer = new Label(string.Empty);
            spacer.AddToClassList("package-row-spacer");
            var nameLabel = CreateAddressLabel(package.Name, "package-name-label");
            if (IsFixedLocalPackage(package) is false)
            {
                nameLabel.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button == 0 && evt.clickCount == 2)
                    {
                        BeginInlineRename(nameLabel, package.Name, value =>
                        {
                            package.Name = value;
                            SaveSettingsImmediately();
                            RefreshPreviewAndIssues();
                        });
                        evt.StopPropagation();
                    }
                });
            }
            nameCell.Add(spacer);
            nameCell.Add(nameLabel);

            var iconCell = CreateCell("icon-column", "package-icon-cell");
            var pathCell = CreateCell("path-column", "package-summary-cell");
            var summary = new Label($"{FormatPackageMode(package)} · {visibleGroupCount}/{package.Bundles.Count} groups");
            summary.AddToClassList("package-summary-label");
            pathCell.Add(summary);

            var labelsCell = CreateCell("labels-column", "package-labels-cell");
            var actionsCell = CreateCell("actions-column", "package-actions-cell");
            var menuButton = new Button(() =>
            {
                SelectPackage(package, false);
                ShowPackageContextMenu(package);
            })
            {
                text = "..."
            };
            menuButton.AddToClassList("row-menu-button");
            actionsCell.Add(menuButton);

            row.Add(nameCell);
            row.Add(iconCell);
            row.Add(pathCell);
            row.Add(labelsCell);
            row.Add(actionsCell);
            return row;
        }

        private VisualElement CreateGroupRow(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, int visibleEntryCount)
        {
            var row = CreateTableRow("group-row");
            row.EnableInClassList("group-row--selected", ReferenceEquals(m_SelectedBundle, bundle));
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    SelectBundle(package, bundle, false);
                    RefreshGroupTable();
                }
            });
            row.RegisterCallback<ContextClickEvent>(evt =>
            {
                SelectBundle(package, bundle, false);
                ShowGroupContextMenu(package, bundle);
                evt.StopPropagation();
            });
            RegisterBundleDrag(row, bundle);

            var nameCell = CreateCell("group-name-column", "group-name-cell");
            var indent = new Label(string.Empty);
            indent.AddToClassList("group-indent");
            var toggle = new Button(() => ToggleBundle(bundle))
            {
                text = m_CollapsedBundles.Contains(bundle) ? ">" : "▼"
            };
            toggle.AddToClassList("foldout-button");
            var groupLabel = CreateAddressLabel(DisplayGroupName(bundle), "group-name-label");
            if (CanEditGroupName(package, bundle))
            {
                groupLabel.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button == 0 && evt.clickCount == 2)
                    {
                        BeginInlineRename(groupLabel, DisplayGroupName(bundle), value =>
                        {
                            RenameBundleGroup(package, bundle, value);
                            SaveSettingsImmediately();
                            RefreshPreviewAndIssues();
                        });
                        evt.StopPropagation();
                    }
                });
            }
            nameCell.Add(indent);
            nameCell.Add(toggle);
            nameCell.Add(groupLabel);

            var iconCell = CreateCell("icon-column", "group-icon-cell");
            iconCell.Add(new Label(string.Empty));

            var pathCell = CreateCell("path-column", "group-settings-cell");
            var folderField = new ObjectField
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false,
                tooltip = "每个 Group 最多绑定一个 Project 文件夹"
            };
            folderField.AddToClassList("group-folder-field");
            folderField.SetEnabled(ResourceProviderIds.IsResources(bundle.ProviderId) is false);
            folderField.SetValueWithoutNotify(string.IsNullOrWhiteSpace(bundle.SourceFolder)
                ? null
                : AssetDatabase.LoadAssetAtPath<DefaultAsset>(bundle.SourceFolder));
            folderField.RegisterValueChangedCallback(evt =>
            {
                var folderPath = evt.newValue == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(evt.newValue).Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(folderPath) is false && AssetDatabase.IsValidFolder(folderPath) is false)
                {
                    folderField.SetValueWithoutNotify(evt.previousValue);
                    ShowNotification(new GUIContent("Group 只能绑定一个文件夹，不能选择文件"));
                    return;
                }

                SetGroupFolder(bundle, folderPath);
            });
            var publishLabel = new Label(FormatPackagePublishMode(package, bundle));
            publishLabel.AddToClassList("group-publish-label");
            var entryCount = new Label($"{visibleEntryCount}/{bundle.Entries.Count} entries");
            entryCount.AddToClassList("group-entry-count");
            pathCell.Add(folderField);
            pathCell.Add(publishLabel);
            pathCell.Add(entryCount);

            var labelsCell = CreateCell("labels-column", "group-labels-cell");
            var actionsCell = CreateCell("actions-column", "group-actions-cell");
            var menuButton = new Button(() => ShowGroupContextMenu(package, bundle)) { text = "..." };
            menuButton.AddToClassList("row-menu-button");
            actionsCell.Add(menuButton);

            row.Add(nameCell);
            row.Add(iconCell);
            row.Add(pathCell);
            row.Add(labelsCell);
            row.Add(actionsCell);
            return row;
        }

        private VisualElement CreateEntryRow(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry)
        {
            var row = CreateTableRow("entry-row");
            row.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowEntryContextMenu(bundle, entry);
                evt.StopPropagation();
            });
            RegisterBundleDrag(row, bundle);

            var nameCell = CreateCell("group-name-column", "entry-name-cell");
            var indent = new Label(string.Empty);
            indent.AddToClassList("entry-indent");
            indent.AddToClassList("entry-indent--nested");
            var kindTag = new Label("正常");
            kindTag.AddToClassList("excluded-kind-tag");
            kindTag.AddToClassList("excluded-kind-tag--normal");
            var address = CreateAddressLabel(entry.Location, "entry-address-label");
            address.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2)
                {
                    BeginInlineRename(address, entry.Location, value =>
                    {
                        entry.Location = value;
                        SaveSettingsImmediately();
                        RefreshPreviewAndIssues();
                    });
                    evt.StopPropagation();
                }
            });
            nameCell.Add(indent);
            nameCell.Add(kindTag);
            nameCell.Add(address);

            var iconCell = CreateCell("icon-column", "entry-icon-cell");
            var icon = new Image();
            icon.AddToClassList("asset-icon");
            icon.style.width = 18;
            icon.style.height = 18;
            icon.style.maxWidth = 18;
            icon.style.maxHeight = 18;
            icon.image = AssetDatabase.GetCachedIcon(entry.AssetPath);
            icon.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2)
                {
                    PingEntryAsset(entry);
                    evt.StopPropagation();
                }
            });
            iconCell.Add(icon);

            var pathCell = CreateCell("path-column", "entry-path-cell");
            var pathLabel = new Label(entry.AssetPath);
            pathLabel.AddToClassList("entry-path-label");
            pathLabel.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2)
                {
                    PingEntryAsset(entry);
                    evt.StopPropagation();
                }
            });
            pathCell.Add(pathLabel);

            var labelsCell = CreateCell("labels-column", "entry-labels-cell");
            labelsCell.Add(CreateEntryLabelDropdown(entry));
            var actionsCell = CreateCell("actions-column", "entry-actions-cell");
            var remove = new Button(() => RemoveEntry(bundle, entry)) { text = "-" };
            remove.AddToClassList("row-remove-button");
            actionsCell.Add(remove);

            row.Add(nameCell);
            row.Add(iconCell);
            row.Add(pathCell);
            row.Add(labelsCell);
            row.Add(actionsCell);
            return row;
        }

        private VisualElement CreateEmptyGroupDropRow(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            var row = CreateTableRow("entry-row");
            row.AddToClassList("entry-row--empty");
            RegisterBundleDrag(row, bundle);

            var nameCell = CreateCell("group-name-column", "entry-name-cell");
            var indent = new Label(string.Empty);
            indent.AddToClassList("entry-indent");
            var message = new Label("Drag Project assets or folders here");
            message.AddToClassList("entry-empty-message");
            nameCell.Add(indent);
            nameCell.Add(message);
            row.Add(nameCell);
            row.Add(CreateCell("icon-column", "entry-icon-cell"));
            row.Add(CreateCell("path-column", "entry-path-cell"));
            row.Add(CreateCell("labels-column", "entry-labels-cell"));
            row.Add(CreateCell("actions-column", "entry-actions-cell"));
            return row;
        }

        /// <summary>
        /// 在忽略列表之后追加资源列表区域，展示参与打包的条目。
        /// </summary>
        /// <param name="group">分组视图数据。</param>
        private void AppendResourceListSection(VisibleGroup group)
        {
            m_GroupTable.Add(CreateResourceListHeaderRow(group));

            if (m_CollapsedResourceLists.Contains(group.Bundle))
            {
                return;
            }

            if (group.Entries.Count == 0)
            {
                m_GroupTable.Add(CreateEmptyGroupDropRow(group.Package, group.Bundle));
                return;
            }

            foreach (var entry in group.Entries)
            {
                m_GroupTable.Add(CreateEntryRow(group.Package, group.Bundle, entry));
            }
        }

        private VisualElement CreateResourceListHeaderRow(VisibleGroup group)
        {
            var row = CreateTableRow("entry-row");
            row.AddToClassList("resource-list-header");
            RegisterBundleDrag(row, group.Bundle);

            var nameCell = CreateCell("group-name-column", "entry-name-cell");
            var indent = new Label(string.Empty);
            indent.AddToClassList("entry-indent");
            var toggle = new Button(() => ToggleResourceList(group.Bundle))
            {
                text = m_CollapsedResourceLists.Contains(group.Bundle) ? ">" : "▼"
            };
            toggle.AddToClassList("foldout-button");
            var title = new Label($"资源列表 ({group.Entries.Count})");
            title.AddToClassList("resource-list-title");
            nameCell.Add(indent);
            nameCell.Add(toggle);
            nameCell.Add(title);

            row.Add(nameCell);
            row.Add(CreateCell("icon-column", "entry-icon-cell"));
            row.Add(CreateCell("path-column", "entry-path-cell"));
            row.Add(CreateCell("labels-column", "entry-labels-cell"));
            row.Add(CreateCell("actions-column", "entry-actions-cell"));
            return row;
        }

        /// <summary>
        /// 在分组顶部追加忽略列表区域，展示被排除/标记删除的条目。
        /// </summary>
        /// <param name="group">分组视图数据。</param>
        private void AppendIgnoreListSection(VisibleGroup group)
        {
            if (group.ExcludedEntries.Count == 0)
            {
                return;
            }

            m_GroupTable.Add(CreateIgnoreListHeaderRow(group));

            if (m_CollapsedIgnoreLists.Contains(group.Bundle))
            {
                return;
            }

            foreach (var entry in group.ExcludedEntries)
            {
                m_GroupTable.Add(CreateExcludedEntryRow(group.Package, group.Bundle, entry));
            }
        }

        private VisualElement CreateIgnoreListHeaderRow(VisibleGroup group)
        {
            var row = CreateTableRow("entry-row");
            row.AddToClassList("ignore-list-header");

            var nameCell = CreateCell("group-name-column", "entry-name-cell");
            var indent = new Label(string.Empty);
            indent.AddToClassList("entry-indent");
            var toggle = new Button(() => ToggleIgnoreList(group.Bundle))
            {
                text = m_CollapsedIgnoreLists.Contains(group.Bundle) ? ">" : "▼"
            };
            toggle.AddToClassList("foldout-button");
            var title = new Label($"忽略列表 ({group.ExcludedEntries.Count})");
            title.AddToClassList("ignore-list-title");
            nameCell.Add(indent);
            nameCell.Add(toggle);
            nameCell.Add(title);

            var actionsCell = CreateCell("actions-column", "entry-actions-cell");
            var restoreAll = new Button(() => RestoreAllEntries(group.Bundle)) { text = "全部恢复" };
            restoreAll.AddToClassList("ignore-list-restore-all");
            actionsCell.Add(restoreAll);

            row.Add(nameCell);
            row.Add(CreateCell("icon-column", "entry-icon-cell"));
            row.Add(CreateCell("path-column", "entry-path-cell"));
            row.Add(CreateCell("labels-column", "entry-labels-cell"));
            row.Add(actionsCell);
            return row;
        }

        private VisualElement CreateExcludedEntryRow(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry)
        {
            var row = CreateTableRow("entry-row");
            row.AddToClassList("entry-row--excluded");
            row.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowExcludedEntryContextMenu(bundle, entry);
                evt.StopPropagation();
            });

            var nameCell = CreateCell("group-name-column", "entry-name-cell");
            var indent = new Label(string.Empty);
            indent.AddToClassList("entry-indent");
            indent.AddToClassList("entry-indent--nested");
            var kindTag = new Label(entry.ExcludeKind == GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.Deleted ? "删除" : "排除");
            kindTag.AddToClassList("excluded-kind-tag");
            kindTag.AddToClassList(entry.ExcludeKind == GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.Deleted ? "excluded-kind-tag--deleted" : "excluded-kind-tag--excluded");
            var address = CreateAddressLabel(entry.Location, "entry-address-label");
            nameCell.Add(indent);
            nameCell.Add(kindTag);
            nameCell.Add(address);

            var iconCell = CreateCell("icon-column", "entry-icon-cell");
            var icon = new Image();
            icon.AddToClassList("asset-icon");
            icon.style.width = 18;
            icon.style.height = 18;
            icon.style.maxWidth = 18;
            icon.style.maxHeight = 18;
            icon.image = AssetDatabase.GetCachedIcon(entry.AssetPath);
            icon.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2)
                {
                    PingEntryAsset(entry);
                    evt.StopPropagation();
                }
            });
            iconCell.Add(icon);

            var pathCell = CreateCell("path-column", "entry-path-cell");
            var pathLabel = new Label(entry.AssetPath);
            pathLabel.AddToClassList("entry-path-label");
            pathCell.Add(pathLabel);

            var labelsCell = CreateCell("labels-column", "entry-labels-cell");
            var actionsCell = CreateCell("actions-column", "entry-actions-cell");
            var restore = new Button(() => RestoreEntry(bundle, entry)) { text = "恢复" };
            restore.AddToClassList("row-restore-button");
            actionsCell.Add(restore);

            row.Add(nameCell);
            row.Add(iconCell);
            row.Add(pathCell);
            row.Add(labelsCell);
            row.Add(actionsCell);
            return row;
        }

        private static VisualElement CreateTableRow(string className)
        {
            var row = new VisualElement();
            row.AddToClassList("addressable-row");
            row.AddToClassList(className);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = className == "package-row" ? 26 : className == "group-row" ? 24 : 24;
            row.style.maxHeight = className == "package-row" ? 26 : className == "group-row" ? 24 : 24;
            return row;
        }

        private static VisualElement CreateCell(string name, string className)
        {
            var cell = new VisualElement { name = name };
            cell.AddToClassList("addressable-cell");
            cell.AddToClassList(className);
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;
            cell.style.minHeight = 22;
            cell.style.maxHeight = 26;
            ApplyColumnLayout(cell, name);
            return cell;
        }

        private static Label CreateAddressLabel(string value, string className)
        {
            var label = new Label(value ?? string.Empty);
            label.AddToClassList("address-label");
            label.AddToClassList(className);
            return label;
        }

        private static void BeginInlineRename(VisualElement target, string currentText, Action<string> onRename)
        {
            if (target == null || target.parent == null)
            {
                return;
            }

            var parent = target.parent;
            var index = parent.IndexOf(target);
            target.RemoveFromHierarchy();

            var field = new TextField
            {
                value = currentText ?? string.Empty,
                isDelayed = false
            };
            field.AddToClassList("inline-rename-field");
            field.RegisterCallback<FocusOutEvent>(_ => CommitInlineRename(field, target, parent, index, onRename));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitInlineRename(field, target, parent, index, onRename);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    CancelInlineRename(field, target, parent, index);
                    evt.StopPropagation();
                }
            });

            InsertInlineRenameElement(parent, field, index);
            field.Focus();
            field.SelectAll();
        }

        private static void CommitInlineRename(TextField field, VisualElement original, VisualElement parent, int index, Action<string> onRename)
        {
            var value = field?.value?.Trim() ?? string.Empty;
            field?.RemoveFromHierarchy();
            InsertInlineRenameElement(parent, original, index);
            if (string.IsNullOrWhiteSpace(value) is false)
            {
                onRename?.Invoke(value);
            }
        }

        private static void CancelInlineRename(TextField field, VisualElement original, VisualElement parent, int index)
        {
            field?.RemoveFromHierarchy();
            InsertInlineRenameElement(parent, original, index);
        }

        private static void InsertInlineRenameElement(VisualElement parent, VisualElement element, int index)
        {
            if (parent == null || element == null)
            {
                return;
            }

            if (index < 0 || index > parent.childCount)
            {
                parent.Add(element);
            }
            else
            {
                parent.Insert(index, element);
            }
        }

        private VisualElement CreateEntryLabelDropdown(GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry)
        {
            var button = new Button
            {
                text = FormatLabelDropdownText(entry?.Labels)
            };
            button.AddToClassList("asset-label-dropdown");
            button.style.minHeight = 20;
            button.style.maxHeight = 22;
            button.clicked += () => ShowEntryLabelMenu(button, entry);
            return button;
        }

        private static void ApplyColumnLayout(VisualElement element, string columnName)
        {
            switch (columnName)
            {
                case "group-name-column":
                    ApplyColumnLayout(element, 360, 280, 420, false);
                    break;
                case "icon-column":
                    ApplyColumnLayout(element, 34, 34, 34, false);
                    break;
                case "path-column":
                    ApplyColumnLayout(element, null, 300, null, true);
                    break;
                case "labels-column":
                    ApplyColumnLayout(element, 152, 132, 172, false);
                    break;
                case "actions-column":
                    ApplyColumnLayout(element, 44, 44, 44, false);
                    break;
            }
        }

        private static void ApplyColumnLayout(VisualElement element, int? width, int? minWidth, int? maxWidth, bool grow)
        {
            if (element == null)
            {
                return;
            }

            if (width.HasValue)
            {
                element.style.width = width.Value;
            }

            if (minWidth.HasValue)
            {
                element.style.minWidth = minWidth.Value;
            }

            if (maxWidth.HasValue)
            {
                element.style.maxWidth = maxWidth.Value;
            }

            element.style.flexGrow = grow ? 1 : 0;
            element.style.flexShrink = grow ? 1 : 0;
        }

        private void ShowEntryLabelMenu(Button anchor, GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var selectedLabels = new HashSet<string>(
                entry.Labels.Where(x => string.IsNullOrWhiteSpace(x) is false),
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
                    menu.AddItem(new GUIContent($"标签目录/{labelName}"), selectedLabels.Contains(labelName), () => ToggleEntryLabel(entry, labelName));
                }
            }

            foreach (var label in selectedLabels.Where(label => configuredLabelSet.Contains(label) is false).OrderBy(label => label, StringComparer.Ordinal))
            {
                var labelName = label;
                menu.AddItem(new GUIContent($"当前未登记/{labelName}"), true, () => ToggleEntryLabel(entry, labelName));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("编辑标签..."), false, () => ShowEntryLabelEditor(entry));
            if (selectedLabels.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("清空标签"));
            }
            else
            {
                menu.AddItem(new GUIContent("清空标签"), false, () => SetEntryLabels(entry, Array.Empty<string>()));
            }

            menu.DropDown(anchor.worldBound);
        }

        private void ShowEntryLabelEditor(GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            LabelEditWindow.Open(
                entry.AssetPath,
                entry.Labels,
                GetConfiguredAssetTags(),
                labels => SetEntryLabels(entry, labels));
        }

        private void ToggleEntryLabel(GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry, string label)
        {
            if (entry == null || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            var labels = entry.Labels.Where(x => string.IsNullOrWhiteSpace(x) is false).ToList();
            var index = labels.FindIndex(x => string.Equals(x, label, StringComparison.Ordinal));
            if (index >= 0)
            {
                labels.RemoveAt(index);
            }
            else
            {
                labels.Add(label);
            }

            SetEntryLabels(entry, labels);
        }

        private void SetEntryLabels(GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry, IEnumerable<string> labels)
        {
            if (entry == null)
            {
                return;
            }

            var normalizedLabels = NormalizeEntryLabels(labels).ToArray();
            entry.Labels.Clear();
            entry.Labels.AddRange(normalizedLabels);
            var asset = AssetDatabase.LoadMainAssetAtPath(entry.AssetPath);
            if (asset != null)
            {
                AssetDatabase.SetLabels(asset, normalizedLabels);
                AssetDatabase.SaveAssets();
            }

            SaveSettingsImmediately();
            RefreshPreviewAndIssues();
        }

        private void ShowGroupContextMenu(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Selected Assets"), false, () => AddSelectedAssetsToBundle(bundle));
            menu.AddItem(new GUIContent("New Group In Package"), false, () => AddBundle(package));
            menu.AddSeparator(string.Empty);
            AddBuildStrategyMenuItems(menu, package);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Build Package"), false, () =>
            {
                SelectBundle(package, bundle, false);
                BuildSelectedPackage();
            });
            if (CanRemoveBundle(package, bundle))
            {
                menu.AddItem(new GUIContent("Remove Group"), false, () => RemoveBundle(package, bundle));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Remove Group"));
            }

            if (GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package) || GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsLocalPackage(package))
            {
                menu.AddDisabledItem(new GUIContent("Remove Package"));
            }
            else
            {
                menu.AddItem(new GUIContent("Remove Package"), false, RemoveSelectedPackage);
            }

            menu.ShowAsContext();
        }

        private void ShowPackageContextMenu(GameDeveloperKit.ResourceEditor.Authoring.Package package)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("New Group"), false, () => AddBundle(package));
            menu.AddSeparator(string.Empty);
            AddBuildStrategyMenuItems(menu, package);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Build Package"), false, () =>
            {
                SelectPackage(package, false);
                BuildSelectedPackage();
            });

            if (IsFixedLocalPackage(package))
            {
                menu.AddDisabledItem(new GUIContent("Remove Package"));
            }
            else
            {
                menu.AddItem(new GUIContent("Remove Package"), false, RemoveSelectedPackage);
            }

            menu.ShowAsContext();
        }

        private void ShowEntryContextMenu(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Ping Asset"), false, () => PingEntryAsset(entry));
            menu.AddItem(new GUIContent("Edit Labels..."), false, () => ShowEntryLabelEditor(entry));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("排除出打包"), false, () => SetEntryExcludeKind(bundle, entry, GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.Excluded));
            menu.AddItem(new GUIContent("标记删除"), false, () => SetEntryExcludeKind(bundle, entry, GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.Deleted));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Remove Entry"), false, () => RemoveEntry(bundle, entry));
            menu.ShowAsContext();
        }

        private void ShowExcludedEntryContextMenu(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Ping Asset"), false, () => PingEntryAsset(entry));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("恢复到打包"), false, () => RestoreEntry(bundle, entry));
            if (entry.ExcludeKind == GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.Deleted)
            {
                menu.AddItem(new GUIContent("改为排除"), false, () => SetEntryExcludeKind(bundle, entry, GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.Excluded));
            }
            else
            {
                menu.AddItem(new GUIContent("改为标记删除"), false, () => SetEntryExcludeKind(bundle, entry, GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.Deleted));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Remove Entry"), false, () => RemoveEntry(bundle, entry));
            menu.ShowAsContext();
        }

        private void AddBuildStrategyMenuItems(GenericMenu menu, GameDeveloperKit.ResourceEditor.Authoring.Package package)
        {
            if (package == null || m_Registry.BuildStrategies.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Build Strategy"));
                return;
            }

            foreach (var strategy in m_Registry.BuildStrategies)
            {
                var strategyId = strategy.Id;
                menu.AddItem(new GUIContent($"Build Strategy/{strategy.DisplayName}"), package.BuildStrategyId == strategyId, () =>
                {
                    package.BuildStrategyId = strategyId;
                    SaveSettingsImmediately();
                    RefreshPreviewAndIssues();
                });
            }
        }

        private void RegisterBundleDrag(VisualElement target, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            target.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = ResolveBundleDragMode(
                    bundle,
                    GameDeveloperKit.ResourceEditor.Authoring.EntryTable.ResolveDraggedAssets());
                evt.StopPropagation();
            });
            target.RegisterCallback<DragPerformEvent>(evt =>
            {
                AddDraggedAssetsToBundle(bundle);
                evt.StopPropagation();
            });
        }

        private void AddDraggedAssetsToBundle(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            var paths = GameDeveloperKit.ResourceEditor.Authoring.EntryTable.ResolveDraggedAssets();
            if (paths.Count == 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            if (ResolveBundleDragMode(bundle, paths) == DragAndDropVisualMode.Rejected)
            {
                ShowNotification(new GUIContent("一个 Group 只能绑定一个文件夹，且不能混拖文件和文件夹"));
                return;
            }

            DragAndDrop.AcceptDrag();
            if (paths.Count == 1 && AssetDatabase.IsValidFolder(paths[0]))
            {
                SetGroupFolder(bundle, paths[0]);
                return;
            }

            AddAssetPathsToBundle(bundle, paths);
        }

        private void AddSelectedAssetsToBundle(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            var paths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => string.IsNullOrWhiteSpace(path) is false)
                .ToList();
            AddAssetPathsToBundle(bundle, paths);
        }

        private void AddAssetPathsToBundle(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, IEnumerable<string> paths)
        {
            if (bundle == null)
            {
                return;
            }

            var normalizedPaths = paths?
                .Where(path => string.IsNullOrWhiteSpace(path) is false)
                .Select(path => path.Replace('\\', '/'))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();
            if (normalizedPaths.Any(AssetDatabase.IsValidFolder))
            {
                ShowNotification(new GUIContent("文件夹必须作为 Group 的唯一 Folder，不能展开为显式资源"));
                return;
            }

            if (string.Equals(bundle.CollectorId, GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId, StringComparison.Ordinal))
            {
                ShowNotification(new GUIContent("该 Group 已绑定 Folder；清空 Folder 后才能添加显式资源"));
                return;
            }

            CommitMutation(() =>
            {
                bundle.CollectorId = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ExplicitCollectorId;
                foreach (var path in normalizedPaths)
                {
                    GameDeveloperKit.ResourceEditor.Authoring.EntryTable.AddEntry(bundle, path);
                }
            });
            m_CollapsedBundles.Remove(bundle);
        }

        private static DragAndDropVisualMode ResolveBundleDragMode(
            GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle,
            IReadOnlyList<string> paths)
        {
            if (bundle == null || paths == null || paths.Count == 0)
            {
                return DragAndDropVisualMode.Rejected;
            }

            var folderCount = paths.Count(AssetDatabase.IsValidFolder);
            if (folderCount > 0)
            {
                return paths.Count == 1 && folderCount == 1
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
            }

            return string.Equals(bundle.CollectorId, GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId, StringComparison.Ordinal)
                ? DragAndDropVisualMode.Rejected
                : DragAndDropVisualMode.Copy;
        }

        private void SetGroupFolder(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, string folderPath)
        {
            if (bundle == null)
            {
                return;
            }

            var normalizedPath = string.IsNullOrWhiteSpace(folderPath)
                ? string.Empty
                : folderPath.Replace('\\', '/').Trim();
            var existingOwner = m_Settings.Packages
                .Where(package => package != null)
                .SelectMany(package => package.Bundles.Where(candidate => candidate != null))
                .FirstOrDefault(candidate => ReferenceEquals(candidate, bundle) is false &&
                                             string.Equals(candidate.SourceFolder, normalizedPath, StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(normalizedPath) is false && existingOwner != null)
            {
                ShowNotification(new GUIContent($"该 Folder 已绑定到 Group: {DisplayGroupName(existingOwner)}"));
                return;
            }

            m_CollapsedBundles.Remove(bundle);
            CommitMutation(() =>
            {
                bundle.SourceFolder = normalizedPath;
                bundle.CollectorId = string.IsNullOrWhiteSpace(normalizedPath)
                    ? GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ExplicitCollectorId
                    : GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId;
            });
        }

        private static IEnumerable<string> NormalizeEntryLabels(IEnumerable<string> labels)
        {
            return labels?
                .Where(label => string.IsNullOrWhiteSpace(label) is false)
                .Select(label => label.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(label => label, StringComparer.Ordinal) ?? Enumerable.Empty<string>();
        }

        private void RenameBundleGroup(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, string value)
        {
            if (bundle == null)
            {
                return;
            }

            var normalized = string.IsNullOrWhiteSpace(value) ? "NewGroup" : value.Trim();
            normalized = UniqueGroupName(package, bundle, normalized);
            bundle.Group = normalized;
            bundle.Name = normalized;
        }

        private void RemoveBundle(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            if (package == null || bundle == null || CanRemoveBundle(package, bundle) is false)
            {
                return;
            }

            package.Bundles.Remove(bundle);
            if (ReferenceEquals(m_SelectedBundle, bundle))
            {
                m_SelectedBundle = package.Bundles.FirstOrDefault();
            }

            SaveSettingsImmediately();
            RefreshPreviewAndIssues();
        }

        private void RemoveEntry(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry)
        {
            if (bundle == null || entry == null)
            {
                return;
            }

            bundle.Entries.Remove(entry);
            SaveSettingsImmediately();
            RefreshPreviewAndIssues();
        }

        /// <summary>
        /// 设置条目的剔除方式（排除或标记删除），条目保留在忽略列表中，可恢复。
        /// </summary>
        /// <param name="bundle">所属 bundle。</param>
        /// <param name="entry">目标条目。</param>
        /// <param name="kind">剔除方式。</param>
        private void SetEntryExcludeKind(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry, GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind kind)
        {
            if (bundle == null || entry == null || entry.ExcludeKind == kind)
            {
                return;
            }

            entry.ExcludeKind = kind;
            SaveSettingsImmediately();
            RefreshPreviewAndIssues();
        }

        /// <summary>
        /// 将条目恢复到打包（从忽略列表移出）。
        /// </summary>
        /// <param name="bundle">所属 bundle。</param>
        /// <param name="entry">目标条目。</param>
        private void RestoreEntry(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry)
        {
            SetEntryExcludeKind(bundle, entry, GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.None);
        }

        /// <summary>
        /// 恢复某个 bundle 忽略列表中的全部条目。
        /// </summary>
        /// <param name="bundle">所属 bundle。</param>
        private void RestoreAllEntries(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            if (bundle == null)
            {
                return;
            }

            var changed = false;
            foreach (var entry in bundle.Entries.Where(entry => entry != null && entry.Excluded))
            {
                entry.ExcludeKind = GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.None;
                changed = true;
            }

            if (changed is false)
            {
                return;
            }

            SaveSettingsImmediately();
            RefreshPreviewAndIssues();
        }

        /// <summary>
        /// 折叠/展开某个 bundle 的忽略列表。
        /// </summary>
        /// <param name="bundle">所属 bundle。</param>
        private void ToggleIgnoreList(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            if (bundle == null)
            {
                return;
            }

            if (m_CollapsedIgnoreLists.Remove(bundle) is false)
            {
                m_CollapsedIgnoreLists.Add(bundle);
            }

            RefreshGroupTable();
        }

        /// <summary>
        /// 折叠/展开某个 bundle 的资源列表。
        /// </summary>
        /// <param name="bundle">所属 bundle。</param>
        private void ToggleResourceList(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            if (bundle == null)
            {
                return;
            }

            if (m_CollapsedResourceLists.Remove(bundle) is false)
            {
                m_CollapsedResourceLists.Add(bundle);
            }

            RefreshGroupTable();
        }

        private void ToggleBundle(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            if (bundle == null)
            {
                return;
            }

            if (m_CollapsedBundles.Contains(bundle))
            {
                m_CollapsedBundles.Remove(bundle);
            }
            else
            {
                m_CollapsedBundles.Add(bundle);
            }

            RefreshGroupTable();
        }

        private void PingEntryAsset(GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.AssetPath))
            {
                return;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(entry.AssetPath);
            if (asset == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void SyncBuiltinResources()
        {
            var package = m_Settings.Packages.FirstOrDefault(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage);
            var bundle = package?.Bundles.FirstOrDefault(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsResourcesGroup);
            if (package == null || bundle == null)
            {
                return;
            }

            var resources = new GameDeveloperKit.ResourceEditor.Registry.UnityResourcesCollector().Collect(package, bundle);
            AddAssetPathsToBundle(bundle, resources.Select(resource => resource.AssetPath));
        }

        private static string FormatPackagePublishMode(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            if (GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package) && GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsResourcesGroup(bundle))
            {
                return "BUILTIN Resources";
            }

            if (GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package) || GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsLocalPackage(package) || package?.IsHotUpdate is false)
            {
                return "Local AssetBundle";
            }

            return "Hot Update AssetBundle";
        }

        private static string FormatPackageMode(GameDeveloperKit.ResourceEditor.Authoring.Package package)
        {
            if (GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package))
            {
                return "Builtin";
            }

            if (GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsLocalPackage(package) || package?.IsHotUpdate is false)
            {
                return "Local";
            }

            return "Hot Update";
        }

        private List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> GetVisibleEntries(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, string query)
        {
            return FilterEntriesByQuery(package, bundle, query, entry => entry.Excluded is false);
        }

        private List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> GetExcludedEntries(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, string query)
        {
            return FilterEntriesByQuery(package, bundle, query, entry => entry.Excluded);
        }

        private List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> FilterEntriesByQuery(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, string query, Func<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry, bool> predicate)
        {
            var entries = bundle.Entries
                .Where(entry => entry != null)
                .Where(predicate)
                .OrderBy(entry => entry.Location, StringComparer.Ordinal)
                .ToList();
            if (string.IsNullOrWhiteSpace(query) || MatchesGroup(package, bundle, query))
            {
                return entries;
            }

            return entries
                .Where(entry => MatchesEntry(entry, query))
                .ToList();
        }

        private static bool ShouldShowGroup(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, IReadOnlyList<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> visibleEntries, IReadOnlyList<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> excludedEntries, string query)
        {
            return string.IsNullOrWhiteSpace(query) ||
                   MatchesPackage(package, query) ||
                   MatchesGroup(package, bundle, query) ||
                   visibleEntries.Count > 0 ||
                   excludedEntries.Count > 0;
        }

        private static bool ShouldShowPackage(GameDeveloperKit.ResourceEditor.Authoring.Package package, string query)
        {
            return string.IsNullOrWhiteSpace(query) || MatchesPackage(package, query);
        }

        private static bool MatchesPackage(GameDeveloperKit.ResourceEditor.Authoring.Package package, string query)
        {
            return ContainsQuery(package?.Name, query) ||
                   ContainsQuery(FormatPackageMode(package), query);
        }

        private static bool MatchesGroup(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, string query)
        {
            return ContainsQuery(package?.Name, query) ||
                   ContainsQuery(bundle?.Name, query) ||
                   ContainsQuery(bundle?.Group, query) ||
                   ContainsQuery(bundle?.ProviderId, query);
        }

        private static bool MatchesEntry(GameDeveloperKit.ResourceEditor.Authoring.AssetEntry entry, string query)
        {
            return ContainsQuery(entry?.Location, query) ||
                   ContainsQuery(entry?.AssetPath, query) ||
                   ContainsQuery(entry?.TypeName, query) ||
                   (entry?.Labels != null && entry.Labels.Any(label => ContainsQuery(label, query)));
        }

        private static bool ContainsQuery(string value, string query)
        {
            return string.IsNullOrWhiteSpace(value) is false &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string NormalizeSearchQuery()
        {
            return m_SearchField?.value?.Trim() ?? string.Empty;
        }

        private void EnsureSelectedBundle()
        {
            if (ContainsBundle(m_SelectedBundle))
            {
                return;
            }

            var selectedPackage = GetSelectedPackage();
            m_SelectedBundle = selectedPackage?.Bundles.FirstOrDefault();
            if (m_SelectedBundle != null)
            {
                return;
            }

            foreach (var package in m_Settings.Packages.Where(package => package != null))
            {
                var bundle = package.Bundles.FirstOrDefault();
                if (bundle == null)
                {
                    continue;
                }

                SelectBundle(package, bundle, false);
                return;
            }
        }

        private bool ContainsBundle(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            return bundle != null && m_Settings.Packages.Any(package => package != null && package.Bundles.Contains(bundle));
        }

        private void SelectBundle(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, bool save)
        {
            m_SelectedBundle = bundle;
            var packageIndex = m_Settings.Packages.IndexOf(package);
            if (packageIndex >= 0)
            {
                m_Settings.SelectedPackageIndex = packageIndex;
            }

            if (save)
            {
                SaveSettingsImmediately();
            }
        }

        private void SelectPackage(GameDeveloperKit.ResourceEditor.Authoring.Package package, bool save)
        {
            var packageIndex = m_Settings.Packages.IndexOf(package);
            if (packageIndex < 0)
            {
                return;
            }

            m_Settings.SelectedPackageIndex = packageIndex;
            m_SelectedBundle = package.Bundles.FirstOrDefault();
            if (save)
            {
                SaveSettingsImmediately();
            }
        }

        private static bool CanEditGroupName(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            return GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package) is false ||
                   GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsResourcesGroup(bundle) is false;
        }

        private static bool CanRemoveBundle(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            return GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package) is false ||
                   GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsResourcesGroup(bundle) is false;
        }

        private static bool IsFixedLocalPackage(GameDeveloperKit.ResourceEditor.Authoring.Package package)
        {
            return GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package) || GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsLocalPackage(package);
        }

        private static string DisplayGroupName(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            return string.IsNullOrWhiteSpace(bundle.Group) ? bundle.Name : bundle.Group;
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

            LabelEditWindow.Open(
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
            m_ApplicationState = m_Application.Refresh();
            m_Issues = m_ApplicationState.Issues.ToList();

            RefreshGroupTable();
        }

        /// <summary>
        /// 获取 Preview。
        /// </summary>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
        private IReadOnlyList<ResourceGroupPreview> GetPreview(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            return m_ApplicationState?.GetPreview(bundle) ?? Array.Empty<ResourceGroupPreview>();
        }

        /// <summary>
        /// 执行 Issue Target。
        /// </summary>
        /// <param name="issue">issue 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        internal static string IssueTarget(GameDeveloperKit.ResourceEditor.Validation.Issue issue)
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
            var issue = selection.OfType<GameDeveloperKit.ResourceEditor.Validation.Issue>().FirstOrDefault();
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
                }
            }

            if (issue.Bundle != null)
            {
                m_SelectedBundle = issue.Bundle;
                m_CollapsedBundles.Remove(issue.Bundle);
                RefreshGroupTable();
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
            CheckResultWindow.Open(m_Issues, SelectIssue);
        }

        /// <summary>
        /// 构建 All Packages。
        /// </summary>
        private void BuildAllPackages()
        {
            BuildResources(GameDeveloperKit.ResourceEditor.Build.Scope.AllPackages);
        }

        private void BuildHotUpdatePackages()
        {
            BuildResources(GameDeveloperKit.ResourceEditor.Build.Scope.HotUpdatePackages);
        }

        /// <summary>
        /// 构建 Selected Package。
        /// </summary>
        private void BuildSelectedPackage()
        {
            BuildResources(GameDeveloperKit.ResourceEditor.Build.Scope.SelectedPackage);
        }

        /// <summary>
        /// 构建 Resources。
        /// </summary>
        /// <param name="scope">scope 参数。</param>
        private void BuildResources(GameDeveloperKit.ResourceEditor.Build.Scope scope)
        {
            var result = m_Application.Build(scope);
            BuildResultWindow.OpenBuildResult(result);
        }

        /// <summary>
        /// 添加 Package。
        /// </summary>
        private void AddPackage()
        {
            var package = new GameDeveloperKit.ResourceEditor.Authoring.Package
            {
                Name = $"Package{m_Settings.Packages.Count + 1}",
                IsHotUpdate = true,
                BuildStrategyId = m_Registry.BuildStrategies.FirstOrDefault()?.Id
            };
            package.EnsureDefaults();
            package.Bundles.Clear();
            package.Bundles.Add(CreateBundle(package, "Default"));
            m_Settings.Packages.Add(package);
            m_Settings.SelectedPackageIndex = m_Settings.Packages.Count - 1;
            m_SelectedBundle = package.Bundles.FirstOrDefault();
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

            if (GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package) || GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsLocalPackage(package))
            {
                return;
            }

            m_Settings.Packages.Remove(package);
            m_Settings.SelectedPackageIndex = Math.Min(m_Settings.SelectedPackageIndex, m_Settings.Packages.Count - 1);
            m_SelectedBundle = GetSelectedPackage()?.Bundles.FirstOrDefault();
            SaveSettingsImmediately();
            RefreshAll();
        }

        /// <summary>
        /// 添加 Bundle。
        /// </summary>
        private void AddBundle()
        {
            AddBundle(GetSelectedPackage() ?? GetLocalPackage());
        }

        private void AddBundle(GameDeveloperKit.ResourceEditor.Authoring.Package package)
        {
            if (package == null)
            {
                return;
            }

            var bundle = CreateBundle(package, NextGroupName(package));
            package.Bundles.Add(bundle);
            m_SelectedBundle = bundle;
            m_CollapsedBundles.Remove(bundle);
            SaveSettingsImmediately();
            RefreshPreviewAndIssues();
        }

        private static GameDeveloperKit.ResourceEditor.Authoring.Bundle CreateBundle(GameDeveloperKit.ResourceEditor.Authoring.Package package, string groupName)
        {
            var isBuiltinResources = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package) && package.Bundles.Count == 0;
            var providerId = isBuiltinResources ? ResourceProviderIds.Resources : ResourceProviderIds.AssetBundle;
            var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
            {
                Name = groupName,
                Group = groupName,
                ProviderId = providerId
            };
            bundle.EnsureDefaults();
            bundle.Name = groupName;
            bundle.Group = groupName;
            bundle.ProviderId = providerId;
            return bundle;
        }

        private static string NextGroupName(GameDeveloperKit.ResourceEditor.Authoring.Package package)
        {
            var index = package.Bundles.Count + 1;
            while (true)
            {
                var name = index == 1 ? "Default" : $"Group{index}";
                if (HasGroupName(package, null, name) is false)
                {
                    return name;
                }

                index++;
            }
        }

        private static string UniqueGroupName(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle currentBundle, string requestedName)
        {
            var baseName = string.IsNullOrWhiteSpace(requestedName) ? "NewGroup" : requestedName.Trim();
            if (HasGroupName(package, currentBundle, baseName) is false)
            {
                return baseName;
            }

            var index = 2;
            while (true)
            {
                var candidate = $"{baseName}{index}";
                if (HasGroupName(package, currentBundle, candidate) is false)
                {
                    return candidate;
                }

                index++;
            }
        }

        private static bool HasGroupName(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle currentBundle, string groupName)
        {
            return package?.Bundles != null &&
                   package.Bundles.Any(bundle => bundle != null &&
                                                 ReferenceEquals(bundle, currentBundle) is false &&
                                                 (string.Equals(bundle.Name, groupName, StringComparison.Ordinal) ||
                                                  string.Equals(bundle.Group, groupName, StringComparison.Ordinal)));
        }

        /// <summary>
        /// 获取 Selected Package。
        /// </summary>
        /// <returns>执行结果。</returns>
        private GameDeveloperKit.ResourceEditor.Authoring.Package GetSelectedPackage()
        {
            if (m_Settings.SelectedPackageIndex < 0 || m_Settings.SelectedPackageIndex >= m_Settings.Packages.Count)
            {
                return null;
            }

            return m_Settings.Packages[m_Settings.SelectedPackageIndex];
        }

        private GameDeveloperKit.ResourceEditor.Authoring.Package GetLocalPackage()
        {
            return m_Settings.Packages.FirstOrDefault(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsLocalPackage);
        }

        /// <summary>
        /// 保存 Settings Immediately。
        /// </summary>
        private void SaveSettingsImmediately()
        {
            m_Settings.SaveSettings();
            hasUnsavedChanges = false;
        }

        private void CommitMutation(Action mutation)
        {
            m_ApplicationState = m_Application.MutateAndCommit(mutation);
            m_Issues = m_ApplicationState.Issues.ToList();
            hasUnsavedChanges = false;
            RefreshGroupTable();
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
        /// 执行 Label From Compression。
        /// </summary>
        /// <param name="compression">compression 参数。</param>
        /// <returns>执行结果。</returns>
        private static string LabelFromCompression(GameDeveloperKit.ResourceEditor.Build.Compression compression)
        {
            switch (compression)
            {
                case GameDeveloperKit.ResourceEditor.Build.Compression.Uncompressed:
                    return CompressionUncompressedLabel;
                case GameDeveloperKit.ResourceEditor.Build.Compression.Lz4:
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
        private static GameDeveloperKit.ResourceEditor.Build.Compression CompressionFromLabel(string label)
        {
            switch (label)
            {
                case CompressionUncompressedLabel:
                    return GameDeveloperKit.ResourceEditor.Build.Compression.Uncompressed;
                case CompressionLz4Label:
                    return GameDeveloperKit.ResourceEditor.Build.Compression.Lz4;
                default:
                    return GameDeveloperKit.ResourceEditor.Build.Compression.Default;
            }
        }

        private static List<string> GetConfiguredChannelNames()
        {
            return GameDeveloperKit.ResourceEditor.Build.Utilities.GetConfiguredChannelNames(
                    GameDeveloperKit.ResourceEditor.Authoring.Settings.LoadOrCreate().BuildSettings)
                .ToList();
        }

        private static string NormalizeChannelSelection(string value, IReadOnlyList<string> configuredChannels)
        {
            if (GameDeveloperKit.ResourceEditor.Build.Settings.IsNoChannelSelection(value))
            {
                return GameDeveloperKit.ResourceEditor.Build.Settings.NoChannelSelection;
            }

            var selected = ParseChannelSelection(value, configuredChannels);
            if (selected.Count == 0 && string.IsNullOrWhiteSpace(value))
            {
                selected.Add(ResourceSettings.DEFAULT_CHANNEL_NAME);
            }

            return SerializeChannelSelection(configuredChannels.Where(selected.Contains));
        }

        private static HashSet<string> ParseChannelSelection(string value, IReadOnlyList<string> configuredChannels)
        {
            var configured = new HashSet<string>(configuredChannels ?? Array.Empty<string>(), StringComparer.Ordinal);
            var selected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var channel in (value ?? string.Empty).Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var normalized = channel.Trim();
                if (configured.Contains(normalized))
                {
                    selected.Add(normalized);
                }
            }

            return selected;
        }

        private static string SerializeChannelSelection(IEnumerable<string> channels)
        {
            return string.Join(",", (channels ?? Enumerable.Empty<string>())
                .Where(channel => string.IsNullOrWhiteSpace(channel) is false)
                .Select(channel => channel.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(channel => channel, StringComparer.Ordinal));
        }

        private static string FormatChannelSelectionText(string value)
        {
            var channels = GetConfiguredChannelNames();
            var selected = ParseChannelSelection(value, channels);
            if (selected.Count == 0)
            {
                return "Nothing";
            }

            if (selected.Count == 1)
            {
                return selected.First();
            }

            if (selected.Count == channels.Count)
            {
                return "Everything";
            }

            return $"{selected.Count} Channels";
        }

        /// <summary>
        /// 执行 Select Issue。
        /// </summary>
        /// <param name="issue">issue 参数。</param>
        private void SelectIssue(GameDeveloperKit.ResourceEditor.Validation.Issue issue)
        {
            OnIssueSelectionChanged(new[] { issue });
        }

        /// <summary>
        /// 定义 Resource Editor Label Edit Window 类型。
        /// </summary>
        private sealed class VisibleGroup
        {
            public VisibleGroup(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> entries, List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> excludedEntries)
            {
                Package = package;
                Bundle = bundle;
                Entries = entries ?? new List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry>();
                ExcludedEntries = excludedEntries ?? new List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry>();
            }

            public GameDeveloperKit.ResourceEditor.Authoring.Package Package { get; }

            public GameDeveloperKit.ResourceEditor.Authoring.Bundle Bundle { get; }

            public List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> Entries { get; }

            public List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> ExcludedEntries { get; }
        }

        private sealed class LabelEditWindow : EditorWindow
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
                var window = GetWindow<LabelEditWindow>(true, "编辑资源标签");
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

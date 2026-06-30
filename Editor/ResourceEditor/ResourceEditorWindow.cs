using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
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

        private readonly HashSet<ResourceEditorBundle> m_CollapsedBundles = new HashSet<ResourceEditorBundle>();

        private ResourceEditorBundle m_SelectedBundle;

        private VisualElement m_GroupTable;

        private VisualElement m_EmptyState;

        private TextField m_SearchField;

        private DropdownField m_PlayModeDropdown;

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
            m_GroupTable = rootVisualElement.Q<VisualElement>("group-table");
            m_EmptyState = rootVisualElement.Q<VisualElement>("empty-state");
            m_SearchField = rootVisualElement.Q<TextField>("search-field");
            m_PlayModeDropdown = rootVisualElement.Q<DropdownField>("play-mode-dropdown");
            m_BuildChannelField = rootVisualElement.Q<DropdownField>("build-channel-field");
            m_BuildVersionField = rootVisualElement.Q<TextField>("build-version-field");
            m_BuildCompressionDropdown = rootVisualElement.Q<DropdownField>("build-compression-dropdown");
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
            m_BuildChannelField.choices = BuildChannelChoices(m_Settings.BuildSettings.Channel);
            m_BuildCompressionDropdown.choices = new List<string> { CompressionDefaultLabel, CompressionLz4Label, CompressionUncompressedLabel };
            m_PlayModeDropdown.choices = new List<string> { "Editor Simulator", "Use Existing Build" };
            m_PlayModeDropdown.SetValueWithoutNotify("Editor Simulator");
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
        /// 执行 Missing Label。
        /// </summary>
        /// <param name="id">id 参数。</param>
        /// <returns>执行结果。</returns>
        private static string MissingLabel(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : $"Missing: {id}";
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
                    .Select(bundle => new VisibleGroup(package, bundle, GetVisibleEntries(package, bundle, query)))
                    .Where(group => ShouldShowGroup(group.Package, group.Bundle, group.Entries, query))
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

                    if (group.Entries.Count == 0)
                    {
                        m_GroupTable.Add(CreateEmptyGroupDropRow(group.Package, group.Bundle));
                        continue;
                    }

                    foreach (var entry in group.Entries)
                    {
                        m_GroupTable.Add(CreateEntryRow(group.Package, group.Bundle, entry));
                    }
                }
            }

            m_EmptyState.style.display = hasVisibleGroup ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private VisualElement CreatePackageRow(ResourceEditorPackage package, int visibleGroupCount)
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
            var nameField = CreateInlineTextField(package.Name);
            nameField.AddToClassList("package-name-field");
            nameField.SetEnabled(IsFixedLocalPackage(package) is false);
            nameField.RegisterValueChangedCallback(evt =>
            {
                package.Name = evt.newValue;
                SaveSettingsImmediately();
                RefreshPreviewAndIssues();
            });
            nameCell.Add(spacer);
            nameCell.Add(nameField);

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

        private VisualElement CreateGroupRow(ResourceEditorPackage package, ResourceEditorBundle bundle, int visibleEntryCount)
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
                text = m_CollapsedBundles.Contains(bundle) ? ">" : "v"
            };
            toggle.AddToClassList("foldout-button");
            var groupField = CreateInlineTextField(DisplayGroupName(bundle));
            groupField.AddToClassList("group-inline-field");
            groupField.SetEnabled(CanEditGroupName(package, bundle));
            groupField.RegisterValueChangedCallback(evt =>
            {
                RenameBundleGroup(package, bundle, evt.newValue);
                SaveSettingsImmediately();
                RefreshPreviewAndIssues();
            });
            nameCell.Add(indent);
            nameCell.Add(toggle);
            nameCell.Add(groupField);

            var iconCell = CreateCell("icon-column", "group-icon-cell");
            iconCell.Add(new Label(string.Empty));

            var pathCell = CreateCell("path-column", "group-settings-cell");
            var publishLabel = new Label(FormatPackagePublishMode(package, bundle));
            publishLabel.AddToClassList("group-publish-label");
            var entryCount = new Label($"{visibleEntryCount}/{bundle.Entries.Count} entries");
            entryCount.AddToClassList("group-entry-count");
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

        private VisualElement CreateEntryRow(ResourceEditorPackage package, ResourceEditorBundle bundle, ResourceEditorAssetEntry entry)
        {
            var row = CreateTableRow("entry-row");
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2)
                {
                    PingEntryAsset(entry);
                }
            });
            row.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowEntryContextMenu(bundle, entry);
                evt.StopPropagation();
            });
            RegisterBundleDrag(row, bundle);

            var nameCell = CreateCell("group-name-column", "entry-name-cell");
            var indent = new Label(string.Empty);
            indent.AddToClassList("entry-indent");
            var address = CreateInlineTextField(entry.Location);
            address.AddToClassList("entry-address-field");
            address.RegisterValueChangedCallback(evt =>
            {
                entry.Location = evt.newValue;
                SaveSettingsImmediately();
                RefreshPreviewAndIssues();
            });
            nameCell.Add(indent);
            nameCell.Add(address);

            var iconCell = CreateCell("icon-column", "entry-icon-cell");
            var icon = new Image();
            icon.AddToClassList("asset-icon");
            icon.image = AssetDatabase.GetCachedIcon(entry.AssetPath);
            iconCell.Add(icon);

            var pathCell = CreateCell("path-column", "entry-path-cell");
            var pathLabel = new Label(entry.AssetPath);
            pathLabel.AddToClassList("entry-path-label");
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

        private VisualElement CreateEmptyGroupDropRow(ResourceEditorPackage package, ResourceEditorBundle bundle)
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

        private static VisualElement CreateTableRow(string className)
        {
            var row = new VisualElement();
            row.AddToClassList("addressable-row");
            row.AddToClassList(className);
            return row;
        }

        private static VisualElement CreateCell(string name, string className)
        {
            var cell = new VisualElement { name = name };
            cell.AddToClassList("addressable-cell");
            cell.AddToClassList(className);
            return cell;
        }

        private static TextField CreateInlineTextField(string value)
        {
            var field = new TextField
            {
                isDelayed = true
            };
            field.AddToClassList("inline-text-field");
            field.SetValueWithoutNotify(value ?? string.Empty);
            return field;
        }

        private VisualElement CreateEntryLabelDropdown(ResourceEditorAssetEntry entry)
        {
            var button = new Button
            {
                text = FormatLabelDropdownText(entry?.Labels)
            };
            button.AddToClassList("asset-label-dropdown");
            button.clicked += () => ShowEntryLabelMenu(button, entry);
            return button;
        }

        private void ShowEntryLabelMenu(Button anchor, ResourceEditorAssetEntry entry)
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

        private void ShowEntryLabelEditor(ResourceEditorAssetEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            ResourceEditorLabelEditWindow.Open(
                entry.AssetPath,
                entry.Labels,
                GetConfiguredAssetTags(),
                labels => SetEntryLabels(entry, labels));
        }

        private void ToggleEntryLabel(ResourceEditorAssetEntry entry, string label)
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

        private void SetEntryLabels(ResourceEditorAssetEntry entry, IEnumerable<string> labels)
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

        private void ShowGroupContextMenu(ResourceEditorPackage package, ResourceEditorBundle bundle)
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

            if (ResourceEditorBuiltinConstants.IsBuiltinPackage(package) || ResourceEditorBuiltinConstants.IsLocalPackage(package))
            {
                menu.AddDisabledItem(new GUIContent("Remove Package"));
            }
            else
            {
                menu.AddItem(new GUIContent("Remove Package"), false, RemoveSelectedPackage);
            }

            menu.ShowAsContext();
        }

        private void ShowPackageContextMenu(ResourceEditorPackage package)
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

        private void ShowEntryContextMenu(ResourceEditorBundle bundle, ResourceEditorAssetEntry entry)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Ping Asset"), false, () => PingEntryAsset(entry));
            menu.AddItem(new GUIContent("Edit Labels..."), false, () => ShowEntryLabelEditor(entry));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Remove Entry"), false, () => RemoveEntry(bundle, entry));
            menu.ShowAsContext();
        }

        private void AddBuildStrategyMenuItems(GenericMenu menu, ResourceEditorPackage package)
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

        private void RegisterBundleDrag(VisualElement target, ResourceEditorBundle bundle)
        {
            target.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = ResourceEditorEntryTable.ResolveDraggedAssets().Count == 0
                    ? DragAndDropVisualMode.Rejected
                    : DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            });
            target.RegisterCallback<DragPerformEvent>(evt =>
            {
                AddDraggedAssetsToBundle(bundle);
                evt.StopPropagation();
            });
        }

        private void AddDraggedAssetsToBundle(ResourceEditorBundle bundle)
        {
            var paths = ResourceEditorEntryTable.ResolveDraggedAssets();
            if (paths.Count == 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            DragAndDrop.AcceptDrag();
            AddAssetPathsToBundle(bundle, paths);
        }

        private void AddSelectedAssetsToBundle(ResourceEditorBundle bundle)
        {
            var paths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => string.IsNullOrWhiteSpace(path) is false)
                .ToList();
            AddAssetPathsToBundle(bundle, paths);
        }

        private void AddAssetPathsToBundle(ResourceEditorBundle bundle, IEnumerable<string> paths)
        {
            if (bundle == null)
            {
                return;
            }

            var changed = false;
            foreach (var path in ResourceEditorEntryTable.ExpandAssetPaths(paths))
            {
                changed |= ResourceEditorEntryTable.AddEntry(bundle, path);
            }

            if (changed)
            {
                m_CollapsedBundles.Remove(bundle);
                SaveSettingsImmediately();
                RefreshPreviewAndIssues();
            }
        }

        private static IEnumerable<string> NormalizeEntryLabels(IEnumerable<string> labels)
        {
            return labels?
                .Where(label => string.IsNullOrWhiteSpace(label) is false)
                .Select(label => label.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(label => label, StringComparer.Ordinal) ?? Enumerable.Empty<string>();
        }

        private void RenameBundleGroup(ResourceEditorPackage package, ResourceEditorBundle bundle, string value)
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

        private void RemoveBundle(ResourceEditorPackage package, ResourceEditorBundle bundle)
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

        private void RemoveEntry(ResourceEditorBundle bundle, ResourceEditorAssetEntry entry)
        {
            if (bundle == null || entry == null)
            {
                return;
            }

            bundle.Entries.Remove(entry);
            SaveSettingsImmediately();
            RefreshPreviewAndIssues();
        }

        private void ToggleBundle(ResourceEditorBundle bundle)
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

        private void PingEntryAsset(ResourceEditorAssetEntry entry)
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
            var package = m_Settings.Packages.FirstOrDefault(ResourceEditorBuiltinConstants.IsBuiltinPackage);
            var bundle = package?.Bundles.FirstOrDefault(ResourceEditorBuiltinConstants.IsResourcesGroup);
            if (package == null || bundle == null)
            {
                return;
            }

            var resources = new UnityResourcesCollector().Collect(package, bundle);
            AddAssetPathsToBundle(bundle, resources.Select(resource => resource.AssetPath));
        }

        private static string FormatPackagePublishMode(ResourceEditorPackage package, ResourceEditorBundle bundle)
        {
            if (ResourceEditorBuiltinConstants.IsBuiltinPackage(package) && ResourceEditorBuiltinConstants.IsResourcesGroup(bundle))
            {
                return "BUILTIN Resources";
            }

            if (ResourceEditorBuiltinConstants.IsBuiltinPackage(package) || ResourceEditorBuiltinConstants.IsLocalPackage(package) || package?.IsHotUpdate is false)
            {
                return "Local AssetBundle";
            }

            return "Hot Update AssetBundle";
        }

        private static string FormatPackageMode(ResourceEditorPackage package)
        {
            if (ResourceEditorBuiltinConstants.IsBuiltinPackage(package))
            {
                return "Builtin";
            }

            if (ResourceEditorBuiltinConstants.IsLocalPackage(package) || package?.IsHotUpdate is false)
            {
                return "Local";
            }

            return "Hot Update";
        }

        private List<ResourceEditorAssetEntry> GetVisibleEntries(ResourceEditorPackage package, ResourceEditorBundle bundle, string query)
        {
            var entries = bundle.Entries
                .Where(entry => entry != null)
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

        private static bool ShouldShowGroup(ResourceEditorPackage package, ResourceEditorBundle bundle, IReadOnlyList<ResourceEditorAssetEntry> visibleEntries, string query)
        {
            return string.IsNullOrWhiteSpace(query) ||
                   MatchesPackage(package, query) ||
                   MatchesGroup(package, bundle, query) ||
                   visibleEntries.Count > 0;
        }

        private static bool ShouldShowPackage(ResourceEditorPackage package, string query)
        {
            return string.IsNullOrWhiteSpace(query) || MatchesPackage(package, query);
        }

        private static bool MatchesPackage(ResourceEditorPackage package, string query)
        {
            return ContainsQuery(package?.Name, query) ||
                   ContainsQuery(FormatPackageMode(package), query);
        }

        private static bool MatchesGroup(ResourceEditorPackage package, ResourceEditorBundle bundle, string query)
        {
            return ContainsQuery(package?.Name, query) ||
                   ContainsQuery(bundle?.Name, query) ||
                   ContainsQuery(bundle?.Group, query) ||
                   ContainsQuery(bundle?.ProviderId, query);
        }

        private static bool MatchesEntry(ResourceEditorAssetEntry entry, string query)
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

        private bool ContainsBundle(ResourceEditorBundle bundle)
        {
            return bundle != null && m_Settings.Packages.Any(package => package != null && package.Bundles.Contains(bundle));
        }

        private void SelectBundle(ResourceEditorPackage package, ResourceEditorBundle bundle, bool save)
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

        private void SelectPackage(ResourceEditorPackage package, bool save)
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

        private static bool CanEditGroupName(ResourceEditorPackage package, ResourceEditorBundle bundle)
        {
            return ResourceEditorBuiltinConstants.IsBuiltinPackage(package) is false ||
                   ResourceEditorBuiltinConstants.IsResourcesGroup(bundle) is false;
        }

        private static bool CanRemoveBundle(ResourceEditorPackage package, ResourceEditorBundle bundle)
        {
            return ResourceEditorBuiltinConstants.IsBuiltinPackage(package) is false ||
                   ResourceEditorBuiltinConstants.IsResourcesGroup(bundle) is false;
        }

        private static bool IsFixedLocalPackage(ResourceEditorPackage package)
        {
            return ResourceEditorBuiltinConstants.IsBuiltinPackage(package) || ResourceEditorBuiltinConstants.IsLocalPackage(package);
        }

        private static string DisplayGroupName(ResourceEditorBundle bundle)
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
                    m_Previews[bundle] = ResourceEditorEntryPreviewBuilder.Build(bundle);
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

            RefreshGroupTable();
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
            ResourceEditorCheckResultWindow.Open(m_Issues, SelectIssue);
        }

        /// <summary>
        /// 构建 All Packages。
        /// </summary>
        private void BuildAllPackages()
        {
            BuildResources(ResourceBuildScope.AllPackages);
        }

        private void BuildHotUpdatePackages()
        {
            BuildResources(ResourceBuildScope.HotUpdatePackages);
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

            if (ResourceEditorBuiltinConstants.IsBuiltinPackage(package) || ResourceEditorBuiltinConstants.IsLocalPackage(package))
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

        private void AddBundle(ResourceEditorPackage package)
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

        private static ResourceEditorBundle CreateBundle(ResourceEditorPackage package, string groupName)
        {
            var isBuiltinResources = ResourceEditorBuiltinConstants.IsBuiltinPackage(package) && package.Bundles.Count == 0;
            var providerId = isBuiltinResources ? ResourceProviderIds.Resources : ResourceProviderIds.AssetBundle;
            var bundle = new ResourceEditorBundle
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

        private static string NextGroupName(ResourceEditorPackage package)
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

        private static string UniqueGroupName(ResourceEditorPackage package, ResourceEditorBundle currentBundle, string requestedName)
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

        private static bool HasGroupName(ResourceEditorPackage package, ResourceEditorBundle currentBundle, string groupName)
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
        private ResourceEditorPackage GetSelectedPackage()
        {
            if (m_Settings.SelectedPackageIndex < 0 || m_Settings.SelectedPackageIndex >= m_Settings.Packages.Count)
            {
                return null;
            }

            return m_Settings.Packages[m_Settings.SelectedPackageIndex];
        }

        private ResourceEditorPackage GetLocalPackage()
        {
            return m_Settings.Packages.FirstOrDefault(ResourceEditorBuiltinConstants.IsLocalPackage);
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
        private sealed class VisibleGroup
        {
            public VisibleGroup(ResourceEditorPackage package, ResourceEditorBundle bundle, List<ResourceEditorAssetEntry> entries)
            {
                Package = package;
                Bundle = bundle;
                Entries = entries ?? new List<ResourceEditorAssetEntry>();
            }

            public ResourceEditorPackage Package { get; }

            public ResourceEditorBundle Bundle { get; }

            public List<ResourceEditorAssetEntry> Entries { get; }
        }

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

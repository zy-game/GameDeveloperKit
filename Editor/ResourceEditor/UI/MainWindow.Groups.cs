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
    public sealed partial class MainWindow
    {
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

                    m_GroupTable.Add(CreateGroupRuleRow(group.Bundle));
                    AppendIgnoreListSection(group);
                    AppendResourceListSection(group);
                }
            }

            m_EmptyState.style.display = hasVisibleGroup ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void CollapseAllGroups()
        {
            m_CollapsedBundles.Clear();
            foreach (var package in m_Settings.Packages.Where(package => package != null))
            {
                foreach (var bundle in package.Bundles.Where(bundle => bundle != null))
                {
                    m_CollapsedBundles.Add(bundle);
                }
            }
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

                if (SetGroupFolder(bundle, folderPath) is false)
                {
                    folderField.SetValueWithoutNotify(evt.previousValue);
                }
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

        private VisualElement CreateGroupRuleRow(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            var row = CreateTableRow("entry-row");
            row.AddToClassList("group-rule-row");

            var collectorField = CreateRuleDropdown(
                "Collector",
                bundle.CollectorId,
                m_Registry.Collectors.Select(descriptor => new KeyValuePair<string, string>(descriptor.Id, descriptor.DisplayName)));
            collectorField.name = "group-collector-dropdown";
            collectorField.SetEnabled(ResourceProviderIds.IsResources(bundle.ProviderId) is false);
            collectorField.RegisterValueChangedCallback(evt =>
            {
                var collectorId = ResolveRuleId(collectorField, evt.newValue);
                if (string.IsNullOrWhiteSpace(collectorId) || string.Equals(collectorId, bundle.CollectorId, StringComparison.Ordinal))
                {
                    return;
                }

                if (string.Equals(collectorId, GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId, StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(bundle.SourceFolder))
                {
                    ShowNotification(new GUIContent("请先为 Group 选择唯一 Folder"));
                    RefreshGroupTable();
                    return;
                }

                CommitMutation(() => bundle.CollectorId = collectorId);
            });
            var filterRuleField = CreateRuleDropdown(
                "Filter",
                bundle.FilterRuleId,
                m_Registry.FilterRules.Select(descriptor => new KeyValuePair<string, string>(descriptor.Id, descriptor.DisplayName)));
            filterRuleField.name = "group-filter-rule-dropdown";
            filterRuleField.RegisterValueChangedCallback(evt =>
            {
                var ruleId = ResolveRuleId(filterRuleField, evt.newValue);
                if (string.IsNullOrWhiteSpace(ruleId) || string.Equals(ruleId, bundle.FilterRuleId, StringComparison.Ordinal))
                {
                    return;
                }

                CommitMutation(() => bundle.FilterRuleId = ruleId);
            });
            var packRuleField = CreateRuleDropdown(
                "Pack",
                bundle.PackRuleId,
                m_Registry.PackRules.Select(descriptor => new KeyValuePair<string, string>(descriptor.Id, descriptor.DisplayName)));
            packRuleField.name = "group-pack-rule-dropdown";
            packRuleField.RegisterValueChangedCallback(evt =>
            {
                var ruleId = ResolveRuleId(packRuleField, evt.newValue);
                if (string.IsNullOrWhiteSpace(ruleId) || string.Equals(ruleId, bundle.PackRuleId, StringComparison.Ordinal))
                {
                    return;
                }

                CommitMutation(() => bundle.PackRuleId = ruleId);
            });

            var rulesCell = new VisualElement();
            rulesCell.AddToClassList("addressable-cell");
            rulesCell.AddToClassList("group-rule-cell");
            var indent = new Label(string.Empty);
            indent.AddToClassList("entry-indent");
            rulesCell.Add(indent);
            rulesCell.Add(collectorField);
            rulesCell.Add(filterRuleField);
            rulesCell.Add(packRuleField);

            row.Add(rulesCell);
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
            var address = CreateAddressLabel(
                GameDeveloperKit.ResourceEditor.Registry.ExplicitAssetCollector.ResolveLocation(bundle.ProviderId, entry.AssetPath),
                "entry-address-label");
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
            var address = CreateAddressLabel(
                GameDeveloperKit.ResourceEditor.Registry.ExplicitAssetCollector.ResolveLocation(bundle.ProviderId, entry.AssetPath),
                "entry-address-label");
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
            row.style.minHeight = className == "package-row" ? 26 : 24;
            row.style.maxHeight = className == "package-row" ? 26 : 24;
            return row;
        }

        private static DropdownField CreateRuleDropdown(
            string label,
            string currentId,
            IEnumerable<KeyValuePair<string, string>> descriptors)
        {
            var options = descriptors?.ToList() ?? new List<KeyValuePair<string, string>>();
            var choices = options
                .Select(option => $"{option.Value} ({option.Key})")
                .ToList();
            var displayToId = choices
                .Select((display, index) => new KeyValuePair<string, string>(display, options[index].Key))
                .ToDictionary(option => option.Key, option => option.Value, StringComparer.Ordinal);
            var currentDisplay = displayToId.FirstOrDefault(option => string.Equals(option.Value, currentId, StringComparison.Ordinal)).Key;
            var missing = string.IsNullOrWhiteSpace(currentDisplay);
            if (missing)
            {
                currentDisplay = $"Missing ({currentId})";
                displayToId.Add(currentDisplay, currentId);
                choices.Insert(0, currentDisplay);
            }

            var field = new DropdownField(label, choices, choices.IndexOf(currentDisplay));
            field.userData = displayToId;
            field.AddToClassList("group-rule-dropdown");
            field.EnableInClassList("group-rule-dropdown--missing", missing);
            field.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            return field;
        }

        private static string ResolveRuleId(DropdownField field, string displayName)
        {
            return field?.userData is IReadOnlyDictionary<string, string> displayToId &&
                   displayToId.TryGetValue(displayName, out var id)
                ? id
                : null;
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

        private bool SetGroupFolder(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle, string folderPath)
        {
            if (bundle == null)
            {
                return false;
            }

            var normalizedPath = GameDeveloperKit.ResourceEditor.Authoring.FolderOwnership.Normalize(folderPath);
            if (GameDeveloperKit.ResourceEditor.Authoring.FolderOwnership.TryFindConflict(
                    m_Settings,
                    bundle,
                    normalizedPath,
                    out var existingOwner))
            {
                ShowNotification(new GUIContent(
                    $"Folder 与 Group {DisplayGroupName(existingOwner)} 的目录重叠: {normalizedPath} <-> {existingOwner.SourceFolder}"));
                return false;
            }

            m_CollapsedBundles.Remove(bundle);
            CommitMutation(() =>
            {
                bundle.SourceFolder = normalizedPath;
                bundle.CollectorId = string.IsNullOrWhiteSpace(normalizedPath)
                    ? GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ExplicitCollectorId
                    : GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId;
            });
            return true;
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
                .OrderBy(entry => entry.AssetPath, StringComparer.Ordinal)
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
            return ContainsQuery(entry?.AssetPath, query) ||
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
    }
}

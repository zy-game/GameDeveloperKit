using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Config;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.TagEditor
{
    public sealed class TagEditorWindow : EditorWindow
    {
        private const string WindowTitle = "Tag Editor";
        private const string StylePath = "Assets/GameDeveloperKit/Editor/TagEditor/UI/TagEditorWindow.uss";

        private TagCatalogAsset m_Catalog;
        private readonly List<TagCatalogValidationIssue> m_Issues = new List<TagCatalogValidationIssue>();
        private TagGroupDefinition m_SelectedGroup;
        private TagDefinition m_SelectedTag;
        private string m_SearchText = string.Empty;
        private string m_StatusText = string.Empty;

        private ListView m_GroupList;
        private ListView m_TagList;
        private TextField m_SearchField;
        private TextField m_GroupKeyField;
        private TextField m_GroupDisplayNameField;
        private Toggle m_GroupFixedToggle;
        private TextField m_TagKeyField;
        private TextField m_TagDisplayNameField;
        private TextField m_TagDescriptionField;
        private Label m_StatusLabel;
        private VisualElement m_IssueContainer;
        private Button m_RemoveGroupButton;
        private Button m_RemoveTagButton;

        [MenuItem("GameDeveloperKit/Tag Editor")]
        public static void Open()
        {
            var window = GetWindow<TagEditorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(840, 520);
            window.Show();
        }

        public void CreateGUI()
        {
            m_Catalog = TagCatalogEditorStore.LoadOrCreate();
            m_Catalog.EnsureDefaults();
            if (m_SelectedGroup == null)
            {
                m_SelectedGroup = m_Catalog.Groups.FirstOrDefault(x => x != null && x.Key == TagCatalogAsset.AssetTagsGroupKey)
                    ?? m_Catalog.Groups.FirstOrDefault();
            }

            BuildLayout();
            RefreshAll();
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var root = new VisualElement();
            root.AddToClassList("tag-editor");
            ApplyEditorTheme(root);
            rootVisualElement.Add(root);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("tag-editor__toolbar");
            toolbar.Add(new Label("标签编辑器") { name = "window-title" });

            var toolbarActions = new VisualElement();
            toolbarActions.AddToClassList("tag-editor__toolbar-actions");
            var refreshButton = new Button(RefreshSources) { text = "刷新来源" };
            var saveButton = new Button(Save) { text = "保存" };
            saveButton.AddToClassList("button--primary");
            toolbarActions.Add(refreshButton);
            toolbarActions.Add(saveButton);
            toolbar.Add(toolbarActions);
            root.Add(toolbar);

            var body = new VisualElement();
            body.AddToClassList("tag-editor__body");
            root.Add(body);

            body.Add(CreateGroupPane());
            body.Add(CreateTagPane());
            body.Add(CreateDetailPane());
        }

        private VisualElement CreateGroupPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("tag-editor__pane");
            pane.AddToClassList("tag-editor__pane--groups");

            var header = new VisualElement();
            header.AddToClassList("pane-header");
            header.Add(new Label("Groups"));
            header.Add(new Button(AddGroup) { text = "+" });
            m_RemoveGroupButton = new Button(RemoveSelectedGroup) { text = "-" };
            header.Add(m_RemoveGroupButton);
            pane.Add(header);

            m_GroupList = new ListView
            {
                selectionType = SelectionType.Single,
                fixedItemHeight = 34,
                makeItem = MakeListRow,
                bindItem = (element, index) =>
                {
                    var groups = GetFilteredGroups();
                    var group = groups[index];
                    SetListRowText(element, group.DisplayName);
                    element.EnableInClassList("list-row--fixed", group.Fixed);
                    element.EnableInClassList("list-row--selected", ReferenceEquals(group, m_SelectedGroup));
                }
            };
            m_GroupList.selectionChanged += selection =>
            {
                var selectedGroup = selection.OfType<TagGroupDefinition>().FirstOrDefault();
                if (ReferenceEquals(selectedGroup, m_SelectedGroup))
                {
                    return;
                }

                m_SelectedGroup = selectedGroup;
                m_SelectedTag = null;
                RefreshAll();
            };
            pane.Add(m_GroupList);
            return pane;
        }

        private VisualElement CreateTagPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("tag-editor__pane");
            pane.AddToClassList("tag-editor__pane--tags");

            var header = new VisualElement();
            header.AddToClassList("pane-header");
            header.Add(new Label("Tags"));
            header.Add(new Button(AddTag) { text = "+" });
            m_RemoveTagButton = new Button(RemoveSelectedTag) { text = "-" };
            header.Add(m_RemoveTagButton);
            pane.Add(header);

            m_SearchField = new TextField("搜索");
            m_SearchField.RegisterValueChangedCallback(evt =>
            {
                m_SearchText = evt.newValue ?? string.Empty;
                RefreshTagList();
            });
            pane.Add(m_SearchField);

            m_TagList = new ListView
            {
                selectionType = SelectionType.Single,
                fixedItemHeight = 36,
                makeItem = MakeListRow,
                bindItem = (element, index) =>
                {
                    var tags = GetFilteredTags();
                    var tag = tags[index];
                    SetListRowText(element, tag.DisplayName);
                    element.EnableInClassList("list-row--fixed", false);
                    element.EnableInClassList("list-row--selected", ReferenceEquals(tag, m_SelectedTag));
                }
            };
            m_TagList.selectionChanged += selection =>
            {
                m_SelectedTag = selection.OfType<TagDefinition>().FirstOrDefault();
                m_TagList.RefreshItems();
                RefreshDetail();
            };
            pane.Add(m_TagList);
            return pane;
        }

        private VisualElement CreateDetailPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("tag-editor__pane");
            pane.AddToClassList("tag-editor__pane--detail");

            pane.Add(new Label("Group") { name = "detail-title" });
            m_GroupKeyField = CreateDelayedTextField("Key");
            m_GroupDisplayNameField = CreateDelayedTextField("Display Name");
            m_GroupFixedToggle = new Toggle("Fixed");
            m_GroupKeyField.RegisterValueChangedCallback(evt =>
            {
                if (m_SelectedGroup == null || m_SelectedGroup.Fixed)
                {
                    return;
                }

                m_SelectedGroup.Key = evt.newValue;
                RefreshAfterEdit();
            });
            m_GroupDisplayNameField.RegisterValueChangedCallback(evt =>
            {
                if (m_SelectedGroup == null)
                {
                    return;
                }

                m_SelectedGroup.DisplayName = evt.newValue;
                RefreshAfterEdit();
            });
            pane.Add(m_GroupKeyField);
            pane.Add(m_GroupDisplayNameField);
            pane.Add(m_GroupFixedToggle);

            pane.Add(new Label("Tag") { name = "detail-title" });
            m_TagKeyField = CreateDelayedTextField("Key");
            m_TagDisplayNameField = CreateDelayedTextField("Display Name");
            m_TagDescriptionField = CreateDelayedTextField("Description", true);
            m_TagKeyField.RegisterValueChangedCallback(evt =>
            {
                if (!CanEditSelectedTag())
                {
                    return;
                }

                m_SelectedTag.Key = evt.newValue;
                RefreshAfterEdit();
            });
            m_TagDisplayNameField.RegisterValueChangedCallback(evt =>
            {
                if (!CanEditSelectedTag())
                {
                    return;
                }

                m_SelectedTag.DisplayName = evt.newValue;
                RefreshAfterEdit();
            });
            m_TagDescriptionField.RegisterValueChangedCallback(evt =>
            {
                if (!CanEditSelectedTag())
                {
                    return;
                }

                m_SelectedTag.Description = evt.newValue;
                RefreshAfterEdit();
            });
            pane.Add(m_TagKeyField);
            pane.Add(m_TagDisplayNameField);
            pane.Add(m_TagDescriptionField);

            m_StatusLabel = new Label();
            m_StatusLabel.AddToClassList("status-label");
            pane.Add(m_StatusLabel);

            m_IssueContainer = new VisualElement();
            m_IssueContainer.AddToClassList("issue-list");
            pane.Add(m_IssueContainer);
            return pane;
        }

        private static void ApplyEditorTheme(VisualElement root)
        {
            root.EnableInClassList("tag-editor--dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("tag-editor--light", EditorGUIUtility.isProSkin is false);
        }

        private void RefreshAll()
        {
            RefreshIssues();
            RefreshGroupList();
            RefreshTagList();
            RefreshDetail();
        }

        private void RefreshIssues()
        {
            m_Issues.Clear();
            m_Issues.AddRange(TagCatalogValidator.Validate(m_Catalog));
        }

        private void RefreshGroupList()
        {
            var groups = GetFilteredGroups();
            m_GroupList.itemsSource = groups;
            m_GroupList.Rebuild();
            var selectedIndex = -1;
            if (m_SelectedGroup != null)
            {
                selectedIndex = groups.IndexOf(m_SelectedGroup);
            }

            SyncListSelection(m_GroupList, selectedIndex);
        }

        private void RefreshTagList()
        {
            var tags = GetFilteredTags();
            m_TagList.itemsSource = tags;
            m_TagList.Rebuild();
            var selectedIndex = -1;
            if (m_SelectedTag != null)
            {
                selectedIndex = tags.IndexOf(m_SelectedTag);
            }

            SyncListSelection(m_TagList, selectedIndex);
        }

        private static void SyncListSelection(ListView listView, int selectedIndex)
        {
            listView.SetSelectionWithoutNotify(selectedIndex >= 0 ? new[] { selectedIndex } : Array.Empty<int>());
            listView.RefreshItems();
        }

        private void RefreshDetail()
        {
            RefreshGroupDetail();
            RefreshTagDetail();
            RefreshIssueList();
            m_RemoveGroupButton?.SetEnabled(m_SelectedGroup != null && m_SelectedGroup.Fixed is false);
            m_RemoveTagButton?.SetEnabled(m_SelectedTag != null);
            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = m_StatusText;
            }
        }

        private void RefreshGroupDetail()
        {
            SetField(m_GroupKeyField, m_SelectedGroup?.Key);
            SetField(m_GroupDisplayNameField, m_SelectedGroup?.DisplayName);
            m_GroupFixedToggle?.SetValueWithoutNotify(m_SelectedGroup?.Fixed ?? false);
            m_GroupKeyField?.SetEnabled(m_SelectedGroup != null && m_SelectedGroup.Fixed is false);
            m_GroupDisplayNameField?.SetEnabled(m_SelectedGroup != null);
            m_GroupFixedToggle?.SetEnabled(false);
        }

        private void RefreshTagDetail()
        {
            SetField(m_TagKeyField, m_SelectedTag?.Key);
            SetField(m_TagDisplayNameField, m_SelectedTag?.DisplayName);
            SetField(m_TagDescriptionField, m_SelectedTag?.Description);

            var tagEditable = CanEditSelectedTag();
            m_TagKeyField?.SetEnabled(tagEditable);
            m_TagDisplayNameField?.SetEnabled(tagEditable);
            m_TagDescriptionField?.SetEnabled(tagEditable);
        }

        private void RefreshIssueList()
        {
            if (m_IssueContainer == null)
            {
                return;
            }

            m_IssueContainer.Clear();
            foreach (var issue in m_Issues)
            {
                var label = new Label($"{issue.Severity}: {issue.Message}");
                label.AddToClassList(issue.Severity == TagCatalogValidationSeverity.Error ? "issue--error" : "issue--warning");
                m_IssueContainer.Add(label);
            }
        }

        private List<TagGroupDefinition> GetFilteredGroups()
        {
            return m_Catalog?.Groups.Where(x => x != null).ToList() ?? new List<TagGroupDefinition>();
        }

        private List<TagDefinition> GetFilteredTags()
        {
            if (m_SelectedGroup == null)
            {
                return new List<TagDefinition>();
            }

            var query = m_SearchText ?? string.Empty;
            return m_SelectedGroup.Tags
                .Where(x => x != null)
                .Where(x => string.IsNullOrWhiteSpace(query)
                    || (x.Key?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                    || (x.DisplayName?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                .ToList();
        }

        private void RefreshSources()
        {
            var assetLabels = TagCatalogImportService.RefreshAssetLabels(m_Catalog);
            var unityTags = TagCatalogImportService.RefreshUnityTags(m_Catalog, out var unityError);
            m_StatusText = string.IsNullOrWhiteSpace(unityError)
                ? $"已刷新来源：Asset Labels +{assetLabels}，Unity Tags +{unityTags}。"
                : $"已刷新 Asset Labels +{assetLabels}；Unity Tags 读取失败：{unityError}";
            RefreshAll();
        }

        private void Save()
        {
            RefreshIssues();
            if (m_Issues.Any(x => x.Severity == TagCatalogValidationSeverity.Error))
            {
                RefreshIssueList();
                EditorUtility.DisplayDialog("标签目录未保存", "当前存在 Error 级标签目录问题，请修复后再保存。", "确定");
                return;
            }

            TagCatalogEditorStore.Save(m_Catalog);
            m_StatusText = "标签目录已保存。";
            RefreshAll();
        }

        private static VisualElement MakeListRow()
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");

            var marker = new VisualElement();
            marker.AddToClassList("list-row__marker");
            row.Add(marker);

            var label = new Label { name = "list-row-label" };
            label.AddToClassList("list-row__text");
            row.Add(label);

            return row;
        }

        private static TextField CreateDelayedTextField(string label, bool multiline = false)
        {
            return new TextField(label)
            {
                isDelayed = true,
                multiline = multiline
            };
        }

        private static void SetListRowText(VisualElement row, string text)
        {
            var label = row.Q<Label>("list-row-label");
            if (label != null)
            {
                label.text = text;
            }
        }

        private void AddGroup()
        {
            var key = MakeUniqueGroupKey("custom-group");
            var group = new TagGroupDefinition
            {
                Key = key,
                DisplayName = "Custom Group",
                Fixed = false
            };
            m_Catalog.Groups.Add(group);
            m_SelectedGroup = group;
            m_SelectedTag = null;
            m_StatusText = $"已新增标签组：{key}";
            RefreshAll();
        }

        private void RemoveSelectedGroup()
        {
            if (m_SelectedGroup == null)
            {
                return;
            }

            if (m_SelectedGroup.Fixed)
            {
                EditorUtility.DisplayDialog("固定组不可删除", "固定标签组只能刷新来源，不能删除。", "确定");
                return;
            }

            m_Catalog.Groups.Remove(m_SelectedGroup);
            m_SelectedGroup = m_Catalog.Groups.FirstOrDefault();
            m_SelectedTag = null;
            m_StatusText = "已删除标签组。";
            RefreshAll();
        }

        private void AddTag()
        {
            if (m_SelectedGroup == null)
            {
                return;
            }

            var key = MakeUniqueTagKey(m_SelectedGroup, "new-tag");
            var tag = new TagDefinition
            {
                Key = key,
                DisplayName = "New Tag"
            };
            m_SelectedGroup.Tags.Add(tag);
            m_SelectedTag = tag;
            m_StatusText = $"已新增标签：{key}";
            RefreshAll();
        }

        private void RemoveSelectedTag()
        {
            if (m_SelectedGroup == null || m_SelectedTag == null)
            {
                return;
            }

            m_SelectedGroup.Tags.Remove(m_SelectedTag);
            m_SelectedTag = null;
            m_StatusText = "已删除标签。";
            RefreshAll();
        }

        private static void SetField(TextField field, string value)
        {
            field?.SetValueWithoutNotify(value ?? string.Empty);
        }

        private void RefreshAfterEdit()
        {
            m_StatusText = string.Empty;
            RefreshIssues();
            RefreshGroupList();
            RefreshTagList();
            RefreshDetail();
        }

        private bool CanEditSelectedTag()
        {
            return m_SelectedTag != null;
        }

        private string MakeUniqueGroupKey(string baseKey)
        {
            var keys = new HashSet<string>(m_Catalog.Groups.Where(x => x != null).Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
            return MakeUniqueKey(baseKey, keys);
        }

        private static string MakeUniqueTagKey(TagGroupDefinition group, string baseKey)
        {
            var keys = new HashSet<string>(group.Tags.Where(x => x != null).Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
            return MakeUniqueKey(baseKey, keys);
        }

        private static string MakeUniqueKey(string baseKey, HashSet<string> existingKeys)
        {
            var key = baseKey;
            var index = 1;
            while (existingKeys.Contains(key))
            {
                index++;
                key = $"{baseKey}-{index}";
            }

            return key;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace GameDeveloperKit.ResourceEditor
{
    public sealed class ResourceEditorWindow : EditorWindow
    {
        private const string WindowTitle = "Resource Editor";
        private const string UxmlPath = "Assets/GameDeveloperKit/Editor/ResourceEditor/UI/ResourceEditorWindow.uxml";

        private ResourceEditorSettings m_Settings;
        private ResourceEditorRegistry m_Registry;
        private List<ResourceValidationIssue> m_Issues = new List<ResourceValidationIssue>();
        private readonly Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>> m_Previews = new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>>();

        private ListView m_PackageList;
        private VisualElement m_EmptyState;
        private ScrollView m_PackageDetail;
        private TextField m_PackageNameField;
        private TextField m_PackageVersionField;
        private Toggle m_HotUpdateToggle;
        private DropdownField m_CollectorDropdown;
        private DropdownField m_BuildDropdown;
        private VisualElement m_BundleContainer;
        private ListView m_IssueList;
        private Label m_DirtyBadge;

        private bool m_IsDirty;

        [MenuItem("GameDeveloperKit/Resource Editor")]
        public static void Open()
        {
            var window = GetWindow<ResourceEditorWindow>();
            window.titleContent = new UnityEngine.GUIContent(WindowTitle);
            window.minSize = new UnityEngine.Vector2(920, 560);
            window.Show();
        }

        public void CreateGUI()
        {
            m_Settings = ResourceEditorSettings.LoadOrCreate();
            m_Registry = ResourceEditorRegistryCache.Current ?? ResourceEditorRegistryCache.Refresh();

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                rootVisualElement.Add(new Label($"Missing UXML: {UxmlPath}"));
                return;
            }

            rootVisualElement.Clear();
            visualTree.CloneTree(rootVisualElement);
            QueryElements();
            BindToolbar();
            BindPackageList();
            RefreshAll();
        }

        private void QueryElements()
        {
            m_PackageList = rootVisualElement.Q<ListView>("package-list");
            m_EmptyState = rootVisualElement.Q<VisualElement>("empty-state");
            m_PackageDetail = rootVisualElement.Q<ScrollView>("package-detail");
            m_PackageNameField = rootVisualElement.Q<TextField>("package-name-field");
            m_PackageVersionField = rootVisualElement.Q<TextField>("package-version-field");
            m_HotUpdateToggle = rootVisualElement.Q<Toggle>("hot-update-toggle");
            m_CollectorDropdown = rootVisualElement.Q<DropdownField>("collector-dropdown");
            m_BuildDropdown = rootVisualElement.Q<DropdownField>("build-dropdown");
            m_BundleContainer = rootVisualElement.Q<VisualElement>("bundle-container");
            m_IssueList = rootVisualElement.Q<ListView>("issue-list");
            m_DirtyBadge = rootVisualElement.Q<Label>("dirty-badge");
        }

        private void BindToolbar()
        {
            rootVisualElement.Q<Button>("refresh-button").clicked += RefreshRegistryAndPreview;
            rootVisualElement.Q<Button>("save-button").clicked += () => Save();
            rootVisualElement.Q<Button>("add-package-button").clicked += AddPackage;
            rootVisualElement.Q<Button>("remove-package-button").clicked += RemoveSelectedPackage;
            rootVisualElement.Q<Button>("add-bundle-button").clicked += AddBundle;
            rootVisualElement.Q<Button>("run-check-button").clicked += RefreshPreviewAndIssues;
            m_IssueList.selectionChanged += OnIssueSelectionChanged;

            m_PackageNameField.RegisterValueChangedCallback(evt =>
            {
                var package = GetSelectedPackage();
                if (package == null)
                {
                    return;
                }

                package.Name = evt.newValue;
                MarkDirty();
                RefreshPackageList();
            });

            m_PackageVersionField.RegisterValueChangedCallback(evt =>
            {
                var package = GetSelectedPackage();
                if (package == null)
                {
                    return;
                }

                package.Version = evt.newValue;
                MarkDirty();
                RefreshPackageList();
            });

            m_HotUpdateToggle.RegisterValueChangedCallback(evt =>
            {
                var package = GetSelectedPackage();
                if (package == null)
                {
                    return;
                }

                package.IsHotUpdate = evt.newValue;
                MarkDirty();
                RefreshPackageList();
            });

            m_CollectorDropdown.RegisterValueChangedCallback(evt =>
            {
                var package = GetSelectedPackage();
                if (package == null)
                {
                    return;
                }

                package.CollectorId = FindCollectorIdByName(evt.newValue);
                MarkDirty();
                RefreshPreviewAndIssues();
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
                MarkDirty();
                RefreshPackageList();
            });
        }

        private void BindPackageList()
        {
            m_PackageList.itemsSource = m_Settings.Packages;
            m_PackageList.selectionType = SelectionType.Single;
            m_PackageList.fixedItemHeight = 58;
            m_PackageList.makeItem = MakePackageRow;
            m_PackageList.bindItem = BindPackageRow;
            m_PackageList.selectionChanged += _ =>
            {
                m_Settings.SelectedPackageIndex = m_PackageList.selectedIndex;
                RefreshPackageDetail();
            };
        }

        private static VisualElement MakePackageRow()
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
            return row;
        }

        private void BindPackageRow(VisualElement element, int index)
        {
            var package = m_Settings.Packages[index];
            var name = element.Q<Label>("name");
            var badge = element.Q<Label>("badge");
            var meta = element.Q<Label>("meta");

            name.text = string.IsNullOrWhiteSpace(package.Name) ? "(未命名)" : package.Name;
            badge.text = package.IsHotUpdate ? "热更" : "内置";
            badge.RemoveFromClassList("badge--hot");
            badge.RemoveFromClassList("badge--builtin");
            badge.AddToClassList(package.IsHotUpdate ? "badge--hot" : "badge--builtin");

            var collector = m_Registry.GetCollector(package.CollectorId)?.DisplayName ?? "Missing collector";
            var build = m_Registry.GetBuildStrategy(package.BuildStrategyId)?.DisplayName ?? "Missing build";
            meta.text = $"{package.Bundles.Count} bundles · {collector} · {build}";
        }

        private void RefreshAll()
        {
            saveChangesMessage = "资源编辑器配置未保存。";
            RefreshDropdowns();
            RefreshPackageList();
            RefreshPackageDetail();
            RefreshPreviewAndIssues();
            UpdateDirtyState();
        }

        private void RefreshRegistryAndPreview()
        {
            m_Registry = ResourceEditorRegistryCache.Refresh();
            RefreshAll();
        }

        private void RefreshDropdowns()
        {
            m_CollectorDropdown.choices = m_Registry.Collectors.Select(x => x.DisplayName).ToList();
            m_BuildDropdown.choices = m_Registry.BuildStrategies.Select(x => x.DisplayName).ToList();
        }

        private void RefreshPackageList()
        {
            m_PackageList.Rebuild();
            if (m_Settings.SelectedPackageIndex >= 0 && m_Settings.SelectedPackageIndex < m_Settings.Packages.Count)
            {
                m_PackageList.SetSelection(m_Settings.SelectedPackageIndex);
            }
        }

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
            SetValueWithoutNotify(m_PackageVersionField, package.Version);
            m_HotUpdateToggle.SetValueWithoutNotify(package.IsHotUpdate);
            m_CollectorDropdown.SetValueWithoutNotify(m_Registry.GetCollector(package.CollectorId)?.DisplayName ?? MissingLabel(package.CollectorId));
            m_BuildDropdown.SetValueWithoutNotify(m_Registry.GetBuildStrategy(package.BuildStrategyId)?.DisplayName ?? MissingLabel(package.BuildStrategyId));
            RefreshBundles(package);
        }

        private static string MissingLabel(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : $"Missing: {id}";
        }

        private void RefreshBundles(ResourceEditorPackage package)
        {
            m_BundleContainer.Clear();
            foreach (var bundle in package.Bundles)
            {
                m_BundleContainer.Add(CreateBundleElement(package, bundle));
            }
        }

        private VisualElement CreateBundleElement(ResourceEditorPackage package, ResourceEditorBundle bundle)
        {
            var previewCount = GetPreview(bundle).Count;
            var card = new Foldout
            {
                text = $"{bundle.Name} · Group: {bundle.Group} · {previewCount} resources",
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
                MarkDirty();
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

            var meta = new Label($"Group: {bundle.Group} · Resources: {GetPreview(bundle).Count}");
            meta.AddToClassList("bundle-meta");

            var nameField = new TextField("Bundle");
            nameField.SetValueWithoutNotify(bundle.Name);
            nameField.RegisterValueChangedCallback(evt =>
            {
                bundle.Name = evt.newValue;
                MarkDirty();
                RefreshPreviewAndIssues();
                RefreshPackageDetail();
            });

            var groupField = new TextField("Group");
            groupField.SetValueWithoutNotify(bundle.Group);
            groupField.RegisterValueChangedCallback(evt =>
            {
                bundle.Group = evt.newValue;
                MarkDirty();
                RefreshPreviewAndIssues();
                RefreshPackageDetail();
            });

            var dependenciesField = new TextField("依赖 Bundle")
            {
                multiline = true
            };
            dependenciesField.SetValueWithoutNotify(string.Join("\n", bundle.Dependencies));
            dependenciesField.RegisterValueChangedCallback(evt =>
            {
                bundle.Dependencies.Clear();
                bundle.Dependencies.AddRange(SplitTokens(evt.newValue));
                MarkDirty();
                RefreshPreviewAndIssues();
            });

            var labelsField = new TextField("标签")
            {
                multiline = true
            };
            labelsField.SetValueWithoutNotify(string.Join("\n", bundle.Labels));
            labelsField.RegisterValueChangedCallback(evt =>
            {
                bundle.Labels.Clear();
                bundle.Labels.AddRange(SplitTokens(evt.newValue));
                MarkDirty();
                RefreshPreviewAndIssues();
            });

            var parameterField = new TextField("收集参数")
            {
                multiline = true
            };
            parameterField.SetValueWithoutNotify(bundle.CollectorParameter);
            parameterField.RegisterValueChangedCallback(evt =>
            {
                bundle.CollectorParameter = evt.newValue;
                MarkDirty();
                RefreshPreviewAndIssues();
            });

            var pathsField = new TextField("资源路径 / 目录")
            {
                multiline = true
            };
            pathsField.SetValueWithoutNotify(string.Join("\n", bundle.AssetPaths));
            pathsField.RegisterValueChangedCallback(evt =>
            {
                bundle.AssetPaths.Clear();
                bundle.AssetPaths.AddRange(SplitLines(evt.newValue));
                MarkDirty();
                RefreshPreviewAndIssues();
                RefreshPackageDetail();
            });

            card.Add(header);
            card.Add(meta);
            card.Add(nameField);
            card.Add(groupField);
            card.Add(dependenciesField);
            card.Add(labelsField);
            card.Add(parameterField);
            card.Add(pathsField);

            var group = new VisualElement();
            group.AddToClassList("group-preview");
            group.Add(new Label(string.IsNullOrWhiteSpace(bundle.Group) ? "Group: (空)" : $"Group: {bundle.Group}"));

            foreach (var preview in GetPreview(bundle))
            {
                var row = new VisualElement();
                row.AddToClassList("preview-row");
                row.Add(new Label(preview.Location));
                row.Add(new Label(preview.TypeName));
                group.Add(row);
            }

            card.Add(group);
            return card;
        }

        private static IEnumerable<string> SplitLines(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => string.IsNullOrWhiteSpace(x) is false);
        }

        private static IEnumerable<string> SplitTokens(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => string.IsNullOrWhiteSpace(x) is false);
        }

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

                var collectorDescriptor = m_Registry.GetCollector(package.CollectorId);
                if (collectorDescriptor == null)
                {
                    m_Issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, "Registry", MissingLabel(package.CollectorId), package));
                }

                if (string.IsNullOrWhiteSpace(package.BuildStrategyId) is false && m_Registry.GetBuildStrategy(package.BuildStrategyId) == null)
                {
                    m_Issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, "Registry", MissingLabel(package.BuildStrategyId), package));
                }

                var collector = collectorDescriptor?.Instance;
                foreach (var bundle in package.Bundles)
                {
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

            BindIssues();
            RefreshPackageDetail();
        }

        private IReadOnlyList<ResourceGroupPreview> GetPreview(ResourceEditorBundle bundle)
        {
            return bundle != null && m_Previews.TryGetValue(bundle, out var preview) ? preview : Array.Empty<ResourceGroupPreview>();
        }

        private void BindIssues()
        {
            m_IssueList.itemsSource = m_Issues;
            m_IssueList.fixedItemHeight = 30;
            m_IssueList.makeItem = () =>
            {
                var row = new Label();
                row.AddToClassList("issue-row");
                return row;
            };
            m_IssueList.bindItem = (element, index) =>
            {
                var issue = m_Issues[index];
                var label = (Label)element;
                label.text = $"[{issue.Severity}] {issue.Source}: {issue.Message}{IssueTarget(issue)}";
                label.RemoveFromClassList("issue-row--error");
                label.RemoveFromClassList("issue-row--warning");
                if (issue.Severity == ResourceValidationSeverity.Error)
                {
                    label.AddToClassList("issue-row--error");
                }
                else if (issue.Severity == ResourceValidationSeverity.Warning)
                {
                    label.AddToClassList("issue-row--warning");
                }
            };
            m_IssueList.Rebuild();
        }

        private static string IssueTarget(ResourceValidationIssue issue)
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
            MarkDirty();
            RefreshAll();
        }

        private void RemoveSelectedPackage()
        {
            var package = GetSelectedPackage();
            if (package == null)
            {
                return;
            }

            m_Settings.Packages.Remove(package);
            m_Settings.SelectedPackageIndex = Math.Min(m_Settings.SelectedPackageIndex, m_Settings.Packages.Count - 1);
            MarkDirty();
            RefreshAll();
        }

        private void AddBundle()
        {
            var package = GetSelectedPackage();
            if (package == null)
            {
                return;
            }

            var bundle = new ResourceEditorBundle
            {
                Name = $"Bundle{package.Bundles.Count + 1}"
            };
            bundle.EnsureDefaults();
            package.Bundles.Add(bundle);
            MarkDirty();
            RefreshPreviewAndIssues();
            RefreshPackageList();
        }

        private ResourceEditorPackage GetSelectedPackage()
        {
            if (m_Settings.SelectedPackageIndex < 0 || m_Settings.SelectedPackageIndex >= m_Settings.Packages.Count)
            {
                return null;
            }

            return m_Settings.Packages[m_Settings.SelectedPackageIndex];
        }

        private string FindCollectorIdByName(string displayName)
        {
            return m_Registry.Collectors.FirstOrDefault(x => x.DisplayName == displayName)?.Id ?? GetSelectedPackage()?.CollectorId;
        }

        private string FindBuildIdByName(string displayName)
        {
            return m_Registry.BuildStrategies.FirstOrDefault(x => x.DisplayName == displayName)?.Id ?? GetSelectedPackage()?.BuildStrategyId;
        }

        private bool Save()
        {
            RefreshPreviewAndIssues();
            if (m_Issues.Any(issue => issue.Severity == ResourceValidationSeverity.Error))
            {
                EditorUtility.DisplayDialog("资源配置未保存", "当前存在 Error 级资源检查问题，请修复后再保存。", "确定");
                return false;
            }

            ResourceManifestPreviewBuilder.Build(m_Settings, m_Previews);
            m_Settings.SelectedPackageIndex = m_PackageList.selectedIndex;
            m_Settings.SaveSettings();
            m_IsDirty = false;
            UpdateDirtyState();
            return true;
        }

        private void MarkDirty()
        {
            m_IsDirty = true;
            UpdateDirtyState();
        }

        private void UpdateDirtyState()
        {
            if (m_DirtyBadge == null)
            {
                return;
            }

            m_DirtyBadge.text = m_IsDirty ? "未保存" : "已保存";
            m_DirtyBadge.EnableInClassList("state-badge--dirty", m_IsDirty);
            hasUnsavedChanges = m_IsDirty;
        }

        public override void SaveChanges()
        {
            if (Save())
            {
                base.SaveChanges();
            }
        }

        public override void DiscardChanges()
        {
            m_IsDirty = false;
            UpdateDirtyState();
            base.DiscardChanges();
        }

        private static void SetValueWithoutNotify(TextField field, string value)
        {
            field.SetValueWithoutNotify(value ?? string.Empty);
        }
    }
}

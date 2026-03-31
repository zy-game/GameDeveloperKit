using System;
using System.Linq;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    public sealed class ResourceCenterWindow : EditorWindow
    {
        private const string CommonUssPath = "Assets/GameDeveloperKit/Editor/Common/EditorCommonStyle.uss";
        private const string UxmlPath = "Assets/GameDeveloperKit/Editor/ResourceSettings/ResourceCenterWindow.uxml";
        private const string UssPath = "Assets/GameDeveloperKit/Editor/ResourceSettings/ResourceCenterWindow.uss";

        [MenuItem("Tools/GameDeveloperKit/Resource Center", priority = 1001)]
        public static void Open()
        {
            var window = GetWindow<ResourceCenterWindow>("Resource Settings");
            window.minSize = new Vector2(920f, 680f);
            window.Show();
        }

        private ResourceCenterController _controller;
        private VisualElement _packageListHost;
        private VisualElement _detailHost;
        private VisualElement _settingsView;
        private VisualElement _emptyState;
        private VisualElement _playModeHost;
        private VisualElement _remoteUrlHost;
        private VisualElement _headerActionsHost;
        private Label _detailTitle;
        private Button _settingsButton;
        private ResourcePackageDetailViewBuilder _packageDetailViewBuilder;
        private ResourceBuildService _buildService;

        private void CreateGUI()
        {
            titleContent = new GUIContent("Resource Settings");
            minSize = new Vector2(920f, 680f);

            _controller ??= new ResourceCenterController();
            _controller.Initialize();
            _buildService ??= new ResourceBuildService();
            _packageDetailViewBuilder ??= new ResourcePackageDetailViewBuilder(
                () =>
                {
                    SaveSettings(false);
                    RefreshView();
                },
                () =>
                {
                    _controller.CollectSelectedPackage(saveImmediately: true);
                    RefreshView();
                });

            var layout = ResourceCenterLayoutFactory.Build(rootVisualElement, CommonUssPath, UxmlPath, UssPath);
            if (layout == null)
            {
                rootVisualElement.Clear();
                rootVisualElement.Add(new HelpBox($"Invalid Resource Center layout: {UxmlPath}", HelpBoxMessageType.Error));
                return;
            }

            _packageListHost = layout.PackageListHost;
            _detailHost = layout.DetailHost;
            _settingsView = layout.SettingsView;
            _emptyState = layout.EmptyState;
            _playModeHost = layout.PlayModeHost;
            _remoteUrlHost = layout.RemoteUrlHost;
            _headerActionsHost = layout.HeaderActionsHost;
            _detailTitle = layout.DetailTitle;
            _settingsButton = layout.SettingsButton;
            var addPackageButton = layout.AddPackageButton;

            _packageListHost.Clear();
            _detailHost.Clear();
            _headerActionsHost.Clear();
            _playModeHost.Clear();
            _remoteUrlHost.Clear();

            addPackageButton.clicked += AddPackage;
            _settingsButton.clicked += ShowMainMenu;

            var playMode = new SingleSelectDropdownField("Play Mode");
            playMode.SetOptions(Enum.GetValues(typeof(ResourcePlayMode)).Cast<ResourcePlayMode>().Select(static value => value.ToString()));
            playMode.SetValue(_controller.Settings.PlayMode.ToString());
            playMode.ValueChanged += value =>
            {
                if (!Enum.TryParse(value, out ResourcePlayMode playModeValue))
                {
                    return;
                }

                _controller.Settings.PlayMode = playModeValue;
                SaveSettings(false);
            };
            playMode.AddToClassList("gdk-inline-field");
            _playModeHost.Add(playMode);

            var remoteUrl = new TextField("Remote URL")
            {
                value = _controller.Settings.RemoteBaseUrl ?? string.Empty,
                isDelayed = true
            };
            remoteUrl.AddToClassList("resource-field");
            remoteUrl.RegisterValueChangedCallback(evt =>
            {
                _controller.Settings.RemoteBaseUrl = evt.newValue;
                SaveSettings(false);
            });
            _remoteUrlHost.Add(remoteUrl);

            RefreshView();
        }

        private void OnDisable()
        {
            if (_controller?.HasUnsavedChanges == true)
            {
                SaveSettings();
            }
        }

        private void RefreshView()
        {
            _controller.EnsureSelectionIsValid();
            _packageListHost?.Clear();
            _detailHost?.Clear();
            if (_controller?.Settings == null)
            {
                return;
            }

            if (_controller.Settings.Packages == null || _controller.Settings.Packages.Count == 0)
            {
                _detailTitle.text = "Package";
                _emptyState.style.display = DisplayStyle.Flex;
                _detailHost.style.display = DisplayStyle.None;
                _settingsView.style.display = _controller.ShowSettingsView ? DisplayStyle.Flex : DisplayStyle.None;
                return;
            }

            for (var i = 0; i < _controller.Settings.Packages.Count; i++)
            {
                var package = _controller.Settings.Packages[i];
                if (package == null)
                {
                    continue;
                }

                _packageListHost.Add(CreatePackageListItem(i, package));
            }

            RenderRightPanel();
        }

        private VisualElement CreatePackageListItem(int index, ResourcePackageDefinition package)
        {
            var item = new Button(() =>
            {
                _controller.SelectPackage(index);
                RefreshView();
            });
            item.AddToClassList("package-item");
            if (index == _controller.SelectedPackageIndex && !_controller.ShowSettingsView)
            {
                item.AddToClassList("package-item--selected");
            }

            var validation = ResourceValidationService.ValidatePackageSummary(_controller.Settings, package, index);
            var dot = new VisualElement();
            dot.AddToClassList("package-status-dot");
            dot.AddToClassList(validation.Status switch
            {
                ResourcePackageValidationStatus.Error => "package-status-dot--invalid",
                ResourcePackageValidationStatus.Warning => "package-status-dot--warning",
                _ => "package-status-dot--valid"
            });
            dot.tooltip = validation.Message;
            item.Add(dot);

            var summary = new VisualElement();
            summary.AddToClassList("resource-package-summary");
            var name = new Label(ResolveText(package.PackageName));
            name.AddToClassList("package-name");
            summary.Add(name);
            var meta = new Label($"v{ResolveText(package.Version)} | {package.Role} | {package.BuildStrategy}");
            meta.AddToClassList("package-meta");
            summary.Add(meta);
            item.Add(summary);
            return item;
        }

        private void RenderRightPanel()
        {
            _headerActionsHost.Clear();

            if (_controller.ShowSettingsView)
            {
                _detailTitle.text = "Resource Settings";
                _settingsView.style.display = DisplayStyle.Flex;
                _emptyState.style.display = DisplayStyle.None;
                _detailHost.style.display = DisplayStyle.None;
                return;
            }

            _settingsView.style.display = DisplayStyle.None;

            if (!_controller.TryGetSelectedPackage(out var package))
            {
                _detailTitle.text = "Package";
                _emptyState.style.display = DisplayStyle.Flex;
                _detailHost.style.display = DisplayStyle.None;
                return;
            }

            _detailTitle.text = ResolveText(package?.PackageName);
            _emptyState.style.display = DisplayStyle.None;
            _detailHost.style.display = DisplayStyle.Flex;

            _detailHost.Add(CreatePackageDetailView(package));
            if (package.CollectionStrategy != ResourcePackageCollectionStrategy.ManualEntries)
            {
                _headerActionsHost.Add(CreateHeaderButton("Collect", CollectSelectedPackage, danger: false));
            }

            _headerActionsHost.Add(CreateHeaderButton("Save", () => SaveSettings(), danger: false));

            _headerActionsHost.Add(CreateHeaderButton("Remove", () =>
            {
                if (_controller.RemoveSelectedPackage())
                {
                    RefreshView();
                }
            }, danger: true));
        }

        private VisualElement CreatePackageDetailView(ResourcePackageDefinition package)
        {
            return _packageDetailViewBuilder.Build(package, _controller.Settings, _controller.HasUnsavedChanges);
        }

        private Button CreateHeaderButton(string text, Action action, bool danger)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("btn");
            button.AddToClassList("btn-sm");
            button.AddToClassList(danger ? "btn-danger" : "btn-primary");
            button.AddToClassList("resource-package-header-button");
            return button;
        }

        private void ShowMainMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("设置"), _controller.ShowSettingsView, () =>
            {
                ToggleSettingsView();
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("编译当前"), false, () =>
            {
                BuildSelectedPackage();
            });
            menu.AddItem(new GUIContent("编译全部"), false, () =>
            {
                BuildAllPackages();
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("检查资源"), false, () =>
            {
                CheckResources();
            });
            menu.AddItem(new GUIContent("缓存清理"), false, () =>
            {
                ClearBuildCache();
            });

            // Show menu at current mouse position (most reliable cross-platform approach)
            menu.ShowAsContext();
        }

        private void ToggleSettingsView()
        {
            _controller.ToggleSettingsView();
            RefreshView();
        }

        private void BuildSelectedPackage()
        {
            if (!_controller.TryGetSelectedPackage(out var package))
            {
                Debug.LogWarning(_controller.BuildSelectedPackageLogMessage());
                return;
            }

            var result = _buildService.BuildPackage(_controller.Settings, package);
            if (result.Success)
            {
                Debug.Log($"[Resource Center] {result.Message}");
                EditorUtility.DisplayDialog("资源打包", $"{result.Message}\n输出目录：{result.OutputRoot}", "确定");
                SaveSettings();
                RefreshView();
                return;
            }

            Debug.LogError($"[Resource Center] {result.Message}");
            EditorUtility.DisplayDialog("资源打包", result.Message, "确定");
        }

        private void BuildAllPackages()
        {
            if (_controller.Settings?.Packages == null || _controller.Settings.Packages.Count == 0)
            {
                Debug.LogWarning(_controller.BuildAllPackagesLogMessage());
                return;
            }

            var result = _buildService.BuildAll(_controller.Settings);
            if (result.Success)
            {
                Debug.Log($"[Resource Center] {result.Message}");
                EditorUtility.DisplayDialog("资源打包", $"{result.Message}\n输出目录：{result.OutputRoot}", "确定");
                SaveSettings();
                RefreshView();
                return;
            }

            Debug.LogError($"[Resource Center] {result.Message}");
            EditorUtility.DisplayDialog("资源打包", result.Message, "确定");
        }

        private void CheckResources()
        {
            if (_controller.Settings?.Packages == null || _controller.Settings.Packages.Count == 0)
            {
                Debug.LogWarning("[Resource Center] No packages to check.");
                return;
            }

            var summary = _controller.BuildResourceCheckSummary();
            Debug.Log($"[Resource Center] Resource check:{Environment.NewLine}{summary}");
            EditorUtility.DisplayDialog("资源检查", summary, "确定");
        }

        private void ClearBuildCache()
        {
            var result = _controller.ClearBuildCache();
            if (result.Success)
            {
                Debug.Log($"[Resource Center] {result.Message}");
                EditorUtility.DisplayDialog("缓存清理", result.Message, "确定");
                return;
            }

            Debug.LogError($"[Resource Center] {result.Message}");
            EditorUtility.DisplayDialog("缓存清理", result.Message, "确定");
        }

        private void AddPackage()
        {
            _controller.AddPackage();
            RefreshView();
        }

        private void SaveSettings(bool saveImmediately = true)
        {
            _controller.SaveSettings(saveImmediately);
            if (saveImmediately)
            {
                RemoveNotification();
                ShowNotification(new GUIContent("Resource settings saved."));
            }
        }

        private void CollectSelectedPackage()
        {
            var entryCount = _controller.CollectSelectedPackage(saveImmediately: true);
            if (entryCount <= 0 && !_controller.TryGetSelectedPackage(out _))
            {
                return;
            }

            RefreshView();
            RemoveNotification();
            ShowNotification(new GUIContent($"Collected {entryCount} entries and saved."));
        }

        private static string ResolveText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<Empty>" : value;
        }
    }
}

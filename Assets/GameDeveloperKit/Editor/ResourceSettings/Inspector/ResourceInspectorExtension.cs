using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    /// <summary>
    /// 资源 Inspector 扩展 - 在可打包资源的 Inspector 顶部显示所属 Package 信息，
    /// 并提供快速加入打包 / 切换 Package 的功能。
    /// </summary>
    [InitializeOnLoad]
    public static class ResourceInspectorExtension
    {
        private const string PANEL_NAME = "resource-package-inspector-panel";

        private static bool _pendingUpdate;
        private static StyleSheet _styleSheet;
        private static Dictionary<string, string> _packageOwnershipCache = new();
        private static bool _isCacheDirty = true;

        static ResourceInspectorExtension()
        {
            _styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/GameDeveloperKit/Editor/Common/EditorCommonStyle.uss");
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.projectChanged += InvalidateOwnershipCache;
        }

        private static void OnSelectionChanged()
        {
            _pendingUpdate = false;

            EditorApplication.delayCall += () =>
            {
                RemovePanelsFromAllInspectors();
            };
        }

        private static void InvalidateOwnershipCache()
        {
            _isCacheDirty = true;
            _packageOwnershipCache.Clear();
        }

        private static void OnPostHeaderGUI(UnityEditor.Editor editor)
        {
            if (editor.targets.Length != 1) return;
            if (_pendingUpdate) return;
            if (!TryResolveInspectableAssetPath(editor.target, out var assetPath)) return;

            var editorElement = GetEditorVisualElement(editor);
            if (editorElement == null) return;

            var existingPanel = editorElement.Q<VisualElement>(PANEL_NAME);
            if (existingPanel == null || !string.Equals(existingPanel.userData as string, assetPath))
            {
                SchedulePanelUpdate(editorElement, assetPath);
            }
        }

        private static bool TryResolveInspectableAssetPath(Object target, out string assetPath)
        {
            assetPath = null;
            if (target == null)
            {
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(target);
            if (target is GameObject go)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    if (!AssetDatabase.IsMainAsset(go))
                    {
                        return false;
                    }
                }
                else
                {
                    var prefabAssetType = PrefabUtility.GetPrefabAssetType(go);
                    if (prefabAssetType == PrefabAssetType.NotAPrefab || !PrefabUtility.IsAnyPrefabInstanceRoot(go))
                    {
                        return false;
                    }

                    assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        return false;
                    }
                }
            }
            else if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsMainAsset(target))
            {
                return false;
            }

            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.StartsWith("Assets/") &&
                   !AssetDatabase.IsValidFolder(assetPath) &&
                   !assetPath.EndsWith(".cs") &&
                   !assetPath.EndsWith(".js");
        }

        private static VisualElement GetEditorVisualElement(UnityEditor.Editor editor)
        {
            var propertyInfo = typeof(UnityEditor.Editor).GetProperty(
                "visualTree", BindingFlags.NonPublic | BindingFlags.Instance);
            if (propertyInfo != null)
                return propertyInfo.GetValue(editor) as VisualElement;

            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var windows = Resources.FindObjectsOfTypeAll(inspectorType);
            foreach (var window in windows)
            {
                if (window is EditorWindow editorWindow)
                    return editorWindow.rootVisualElement;
            }
            return null;
        }

        private static void RemovePanelsFromAllInspectors()
        {
            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var windows = Resources.FindObjectsOfTypeAll(inspectorType);
            foreach (var window in windows)
            {
                if (window is not EditorWindow editorWindow)
                {
                    continue;
                }

                var panels = editorWindow.rootVisualElement?.Query<VisualElement>(PANEL_NAME).ToList();
                if (panels == null)
                {
                    continue;
                }

                foreach (var panel in panels)
                {
                    panel.RemoveFromHierarchy();
                }
            }
        }

        private static void SchedulePanelUpdate(VisualElement editorElement, string assetPath)
        {
            if (_pendingUpdate) return;
            _pendingUpdate = true;

            EditorApplication.delayCall += () =>
            {
                _pendingUpdate = false;
                if (editorElement == null) return;

                var oldPanels = editorElement.Query<VisualElement>(PANEL_NAME).ToList();
                foreach (var p in oldPanels) p.RemoveFromHierarchy();

                CreatePanel(editorElement, assetPath);
            };
        }

        private static string FindOwningPackage(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            RebuildOwnershipCacheIfNeeded();
            return _packageOwnershipCache.TryGetValue(assetPath, out var packageName) ? packageName : null;
        }

        private static void RebuildOwnershipCacheIfNeeded()
        {
            if (!_isCacheDirty)
            {
                return;
            }

            _packageOwnershipCache.Clear();
            var settings = GameFrameworkConfigurationBridge.LoadResourceSettingsData();
            if (settings?.Packages == null)
            {
                _isCacheDirty = false;
                return;
            }

            foreach (var package in settings.Packages)
            {
                if (package == null || string.IsNullOrWhiteSpace(package.PackageName))
                {
                    continue;
                }

                var entries = ResourceCollectionService.BuildCollectedEntries(package);
                if (entries == null)
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                    {
                        continue;
                    }

                    _packageOwnershipCache[entry.FullPath] = package.PackageName;
                }
            }

            _isCacheDirty = false;
        }

        private static void CreatePanel(VisualElement parent, string assetPath)
        {
            var settings = GameFrameworkConfigurationBridge.LoadResourceSettingsData();

            var panel = new VisualElement();
            panel.name = PANEL_NAME;
            panel.userData = assetPath;
            if (_styleSheet != null)
                panel.styleSheets.Add(_styleSheet);

            panel.style.marginTop = 4;
            panel.style.marginBottom = 4;
            panel.style.marginLeft = 0;
            panel.style.marginRight = 0;
            panel.style.paddingTop = 6;
            panel.style.paddingBottom = 6;
            panel.style.paddingLeft = 8;
            panel.style.paddingRight = 8;
            panel.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            panel.style.borderTopWidth = 1;
            panel.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            panel.style.borderBottomWidth = 1;
            panel.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;

            // Header row
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var title = new Label("Resource Package");
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            title.style.flexGrow = 1;
            header.Add(title);

            var openBtn = new Button(() =>
            {
                var window = EditorWindow.GetWindow<ResourceCenterWindow>("Resource Settings");
                window.Show();
            })
            { text = "Open Manager" };
            openBtn.style.fontSize = 10;
            openBtn.style.paddingTop = 2;
            openBtn.style.paddingBottom = 2;
            openBtn.style.paddingLeft = 6;
            openBtn.style.paddingRight = 6;
            header.Add(openBtn);

            panel.Add(header);

            // Find owning package
            var owningPackageName = FindOwningPackage(assetPath);

            // Status label
            var statusLabel = new Label();
            statusLabel.style.fontSize = 11;
            statusLabel.style.marginTop = 4;

            if (owningPackageName != null)
            {
                statusLabel.text = $"✓ Included in: {owningPackageName}";
                statusLabel.style.color = new Color(0.4f, 0.8f, 0.4f, 1f);
            }
            else
            {
                statusLabel.text = "✗ Not included in any package";
                statusLabel.style.color = new Color(0.8f, 0.7f, 0.3f, 1f);
            }
            panel.Add(statusLabel);

            // Quick add to package section
            var packageNames = settings?.Packages?
                .Where(p => p != null)
                .Select(p => p.PackageName ?? "(unnamed)")
                .ToList() ?? new List<string>();

            if (packageNames.Count > 0)
            {
                var addRow = new VisualElement();
                addRow.style.flexDirection = FlexDirection.Row;
                addRow.style.alignItems = Align.Center;
                addRow.style.marginTop = 6;

                var addLabel = new Label("Add to: ");
                addLabel.style.fontSize = 11;
                addLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                addLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                addRow.Add(addLabel);

                var dropdownChoices = packageNames.ToList();
                var dropdown = new DropdownField(dropdownChoices, 0);
                dropdown.style.flexGrow = 1;
                dropdown.style.fontSize = 11;
                addRow.Add(dropdown);

                var addBtn = new Button(() =>
                {
                    var selectedName = dropdown.value;
                    var targetPackage = settings?.Packages?
                        .FirstOrDefault(p => p.PackageName == selectedName);
                    if (targetPackage == null) return;

                    AddAssetToPackage(assetPath, targetPackage);
                    var configuration = GameFrameworkConfigurationBridge.ResolveSelectedOrFirstConfiguration();
                    if (configuration != null)
                    {
                        GameFrameworkConfigurationBridge.ApplyResourceSettings(configuration, settings);
                        GameFrameworkConfigurationBridge.SaveConfiguration(configuration);
                    }
                    GameFrameworkConfigurationBridge.SaveResourceSettingsData(settings);
                    InvalidateOwnershipCache();

                    var oldPanels = parent.Query<VisualElement>(PANEL_NAME).ToList();
                    foreach (var p in oldPanels) p.RemoveFromHierarchy();
                    CreatePanel(parent, assetPath);
                })
                { text = "Add" };
                addBtn.style.fontSize = 10;
                addBtn.style.paddingTop = 2;
                addBtn.style.paddingBottom = 2;
                addBtn.style.paddingLeft = 8;
                addBtn.style.paddingRight = 8;
                addRow.Add(addBtn);

                panel.Add(addRow);
            }

            // Hint
            var hintLabel = new Label("Tip: Add this asset to a package's GUID list collector for explicit inclusion.");
            hintLabel.style.fontSize = 9;
            hintLabel.style.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            hintLabel.style.marginTop = 4;
            panel.Add(hintLabel);

            parent.Add(panel);
        }

        /// <summary>
        /// Add an asset to a package by switching to Directory strategy and
        /// adding the asset's parent directory to CollectRoots if not already present.
        /// </summary>
        private static void AddAssetToPackage(string assetPath, ResourcePackageDefinition package)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            ResourceCollectionService.NormalizePackage(package);

            if (package.CollectionStrategy == ResourcePackageCollectionStrategy.ManualEntries)
            {
                package.CollectionStrategy = ResourcePackageCollectionStrategy.Directory;
            }

            if (package.CollectionStrategy == ResourcePackageCollectionStrategy.Directory)
            {
                var dir = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir))
                {
                    package.CollectRoots ??= new List<string>();
                    if (!package.CollectRoots.Contains(dir))
                    {
                        package.CollectRoots.Add(dir);
                    }
                }
            }
        }

    }
}

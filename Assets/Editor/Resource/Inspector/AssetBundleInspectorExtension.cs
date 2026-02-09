using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源 Inspector 扩展 - 在所有资源的 Inspector 底部添加打包配置面板
    /// 使用新的收集器系统
    /// </summary>
    [InitializeOnLoad]
    public static class AssetBundleInspectorExtension
    {
        private const string PANEL_NAME = "asset-bundle-panel";

        private static int _selectedPackageIndex;
        private static string[] _packageNames;
        private static string _currentAssetPath;
        private static bool _pendingUpdate;
        private static StyleSheet _styleSheet;

        // UI Elements
        private static VisualElement _panelRoot;
        private static DropdownField _packageDropdown;
        private static Label _statusLabel;

        static AssetBundleInspectorExtension()
        {
            _styleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
            Selection.selectionChanged += OnSelectionChanged;
            
            // 监听资源包数据变化，自动刷新面板
            ResourcePackagesData.OnDataSaved += OnResourcePackagesDataSaved;
        }
        
        private static void OnResourcePackagesDataSaved()
        {
            if (!string.IsNullOrEmpty(_currentAssetPath))
            {
                var assetPath = _currentAssetPath;
                _currentAssetPath = null;
                _panelRoot = null;
                _pendingUpdate = false;
                
                EditorApplication.delayCall += () =>
                {
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                };
            }
        }

        private static void OnSelectionChanged()
        {
            _currentAssetPath = null;
            _panelRoot = null;
            _pendingUpdate = false;
            
            EditorApplication.delayCall += () =>
            {
                var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
                var windows = Resources.FindObjectsOfTypeAll(inspectorType);
                foreach (var window in windows)
                {
                    var editorWindow = window as EditorWindow;
                    if (editorWindow != null)
                    {
                        var root = editorWindow.rootVisualElement;
                        var panels = root?.Query<VisualElement>(PANEL_NAME).ToList();
                        if (panels != null)
                        {
                            foreach (var panel in panels)
                                panel.RemoveFromHierarchy();
                        }
                    }
                }
            };
        }

        private static void OnPostHeaderGUI(UnityEditor.Editor editor)
        {
            if (editor.targets.Length != 1) return;
            if (_pendingUpdate) return;

            var target = editor.target;
            if (target == null) return;

            var assetPath = AssetDatabase.GetAssetPath(target);
            
            // 处理场景中的Prefab实例
            if (target is GameObject go)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    if (!AssetDatabase.IsMainAsset(go)) return;
                }
                else
                {
                    var prefabAssetType = PrefabUtility.GetPrefabAssetType(go);
                    if (prefabAssetType == PrefabAssetType.NotAPrefab) return;
                    
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) return;
                    
                    var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                    if (string.IsNullOrEmpty(prefabPath)) return;
                    assetPath = prefabPath;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/")) return;
                if (!AssetDatabase.IsMainAsset(target)) return;
            }
            
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/")) return;
            
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            if (assetPath.EndsWith(".cs") || assetPath.EndsWith(".js")) return;

            var editorElement = GetEditorVisualElement(editor);
            if (editorElement == null) return;

            var existingPanel = editorElement.Q<VisualElement>(PANEL_NAME);

            if (_currentAssetPath != assetPath)
            {
                _currentAssetPath = assetPath;
                LoadAssetConfig(assetPath);
                SchedulePanelUpdate(editorElement, assetPath);
            }
            else if (existingPanel == null)
            {
                LoadAssetConfig(assetPath);
                SchedulePanelUpdate(editorElement, assetPath);
            }
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
                var editorWindow = window as EditorWindow;
                if (editorWindow != null)
                    return editorWindow.rootVisualElement;
            }
            return null;
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

        private static void LoadAssetConfig(string assetPath)
        {
            RefreshPackageList();
            
            // 查找资源所属的 Package
            var packagesData = ResourcePackagesData.Instance;
            string foundPackage = null;
            
            if (packagesData?.packages != null)
            {
                foreach (var package in packagesData.packages)
                {
                    if (package.collector != null)
                    {
                        var assets = package.CollectAssets();
                        if (assets.Any(a => a.assetPath == assetPath))
                        {
                            foundPackage = package.packageName;
                            break;
                        }
                    }
                }
            }

            if (foundPackage != null)
            {
                _selectedPackageIndex = System.Array.IndexOf(_packageNames, foundPackage);
                if (_selectedPackageIndex < 0) _selectedPackageIndex = 0;
            }
            else
            {
                _selectedPackageIndex = 0;
            }
        }

        private static void RefreshPackageList()
        {
            var packagesData = ResourcePackagesData.Instance;
            if (packagesData?.packages != null && packagesData.packages.Count > 0)
                _packageNames = packagesData.packages.Select(p => p.packageName).ToArray();
            else
                _packageNames = new string[] { "(No Packages)" };
        }

        private static void CreatePanel(VisualElement parent, string assetPath)
        {
            _panelRoot = new VisualElement();
            _panelRoot.name = PANEL_NAME;
            if (_styleSheet != null)
                _panelRoot.styleSheets.Add(_styleSheet);

            _panelRoot.AddToClassList("info-card");
            _panelRoot.style.marginTop = 8;
            _panelRoot.style.marginBottom = 8;
            _panelRoot.style.marginLeft = 4;
            _panelRoot.style.marginRight = 4;
            _panelRoot.style.paddingTop = 8;
            _panelRoot.style.paddingBottom = 8;
            _panelRoot.style.paddingLeft = 8;
            _panelRoot.style.paddingRight = 8;

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var title = new Label("Asset Bundle");
            title.AddToClassList("card-title");
            title.style.flexGrow = 1;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            var managerBtn = new Button(() => ResourcePackagesWindow.ShowWindow()) { text = "Open Manager" };
            managerBtn.AddToClassList("btn");
            managerBtn.AddToClassList("btn-sm");
            managerBtn.AddToClassList("btn-secondary");
            header.Add(managerBtn);

            _panelRoot.Add(header);

            // 查找资源所属的 Package
            var packagesData = ResourcePackagesData.Instance;
            string foundPackage = null;
            
            if (packagesData?.packages != null)
            {
                foreach (var package in packagesData.packages)
                {
                    if (package.collector != null)
                    {
                        var assets = package.CollectAssets();
                        if (assets.Any(a => a.assetPath == assetPath))
                        {
                            foundPackage = package.packageName;
                            break;
                        }
                    }
                }
            }

            // Status Label
            _statusLabel = new Label();
            _statusLabel.style.marginTop = 4;
            
            if (foundPackage != null)
            {
                _statusLabel.text = $"Included in: {foundPackage}";
                _statusLabel.style.color = new Color(0.5f, 0.8f, 0.5f);
            }
            else
            {
                _statusLabel.text = "Not included in any package";
                _statusLabel.style.color = new Color(0.8f, 0.8f, 0.5f);
            }
            
            _panelRoot.Add(_statusLabel);

            // 提示信息
            var hintLabel = new Label("Use the Resource Package Manager to configure collectors.");
            hintLabel.style.fontSize = 10;
            hintLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            hintLabel.style.marginTop = 4;
            _panelRoot.Add(hintLabel);

            parent.Add(_panelRoot);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源调试面板 - IMGUI 版本
    /// </summary>
    public class ResourceDebugPanel : IDebugPanelIMGUI
    {
        public string Name => "Resource";
        public int Order => 40;

        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private float _lastRefreshTime;
        private const float RefreshInterval = 1f;

        private FieldInfo _packagesField;
        private FieldInfo _bundleServiceField;
        private FieldInfo _loadedBundlesField;
        private FieldInfo _referenceCountsField;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _refStyle;
        private bool _stylesInitialized;

        public ResourceDebugPanel()
        {
            var resourceModuleType = typeof(ResourceModule);
            _packagesField = resourceModuleType.GetField("_packages", BindingFlags.NonPublic | BindingFlags.Instance);
            _bundleServiceField = resourceModuleType.GetField("_bundleService", BindingFlags.NonPublic | BindingFlags.Instance);
            var bundleCacheType = typeof(BundleCache);
            _loadedBundlesField = bundleCacheType.GetField("_loadedBundles", BindingFlags.NonPublic | BindingFlags.Instance);
            _referenceCountsField = bundleCacheType.GetField("_referenceCounts", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void OnGUI()
        {
            InitStyles();

            var resourceModule = Game.Resource as ResourceModule;
            if (resourceModule == null) { GUILayout.Label("ResourceModule not available"); return; }

            // Toolbar
            GUILayout.BeginHorizontal();
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", GUILayout.Width(100));
            if (GUILayout.Button("Unload Unused", GUILayout.Width(100)))
                resourceModule.UnloadUnusedAssets();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            int totalPackages = 0, totalHandles = 0, totalBundles = 0;

            // Packages
            var packages = _packagesField?.GetValue(resourceModule) as Dictionary<string, ResourcePackage>;
            if (packages != null)
            {
                totalPackages = packages.Count;
                foreach (var kvp in packages)
                {
                    var handles = GetCachedHandles(kvp.Value);
                    totalHandles += handles.Count;
                    DrawPackageSection(kvp.Key, kvp.Value, handles);
                }
            }

            // Bundle Cache
            var bundleService = _bundleServiceField?.GetValue(resourceModule) as BundleLoaderService;
            if (bundleService != null)
                DrawBundleSection(bundleService, ref totalBundles);

            GUILayout.EndScrollView();

            GUILayout.Label($"Packages: {totalPackages} | Handles: {totalHandles} | Bundles: {totalBundles}", _labelStyle);
        }

        private void DrawPackageSection(string name, ResourcePackage package, List<BaseHandle> handles)
        {
            var statusColor = package.Status switch
            {
                PackageStatus.Ready => Color.green,
                PackageStatus.Initializing => Color.yellow,
                PackageStatus.Failed => Color.red,
                _ => Color.gray
            };

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Package: {name}", _headerStyle);
            GUI.color = statusColor;
            GUILayout.Label($"[{package.Status}]", GUILayout.Width(100));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Label($"Version: {package.Version}", _labelStyle);

            if (handles.Count > 0)
            {
                GUILayout.Label($"Cached Handles ({handles.Count}):", _labelStyle);
                int count = 0;
                foreach (var handle in handles.Take(20))
                {
                    var refColor = handle.ReferenceCount == 0 ? Color.red : handle.ReferenceCount == 1 ? Color.green : Color.yellow;
                    GUILayout.BeginHorizontal();
                    GUI.color = refColor;
                    GUILayout.Label($"[{handle.ReferenceCount}]", GUILayout.Width(30));
                    GUI.color = Color.white;
                    GUILayout.Label(handle.Address ?? handle.Name ?? "Unknown");
                    GUILayout.EndHorizontal();
                    count++;
                }
                if (handles.Count > 20)
                    GUILayout.Label($"... and {handles.Count - 20} more", _labelStyle);
            }
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private void DrawBundleSection(BundleLoaderService bundleService, ref int totalBundles)
        {
            var cacheField = typeof(BundleLoaderService).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance);
            var cache = cacheField?.GetValue(bundleService) as BundleCache;
            if (cache == null) return;

            var loadedBundles = _loadedBundlesField?.GetValue(cache) as Dictionary<string, AssetBundle>;
            var refCounts = _referenceCountsField?.GetValue(cache) as Dictionary<string, int>;
            if (loadedBundles == null || loadedBundles.Count == 0) return;

            totalBundles = loadedBundles.Count;

            GUILayout.BeginVertical("box");
            GUILayout.Label($"Bundle Cache ({loadedBundles.Count})", _headerStyle);

            int count = 0;
            foreach (var kvp in loadedBundles.Take(30))
            {
                var refCount = refCounts?.GetValueOrDefault(kvp.Key, 0) ?? 0;
                var refColor = refCount == 0 ? Color.red : refCount == 1 ? Color.green : Color.yellow;
                GUILayout.BeginHorizontal();
                GUI.color = refColor;
                GUILayout.Label($"[{refCount}]", GUILayout.Width(30));
                GUI.color = Color.white;
                GUILayout.Label(kvp.Key);
                GUILayout.EndHorizontal();
                count++;
            }
            if (loadedBundles.Count > 30)
                GUILayout.Label($"... and {loadedBundles.Count - 30} more", _labelStyle);

            GUILayout.EndVertical();
        }

        private List<BaseHandle> GetCachedHandles(ResourcePackage package)
        {
            var handles = new List<BaseHandle>();
            var providerSystem = package.GetProviderSystem();
            if (providerSystem == null) return handles;

            var fields = new[] { "_defaultBundleProvider", "_defaultBuiltinProvider", "_defaultRemoteProvider",
                                 "_defaultBundleSceneProvider", "_defaultBuiltinSceneProvider", "_defaultRemoteSceneProvider" };

            foreach (var fieldName in fields)
            {
                var field = typeof(ProviderSystem).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                var provider = field?.GetValue(providerSystem);
                if (provider == null) continue;

                var cachedField = provider.GetType().GetField("_cachedAssets", BindingFlags.NonPublic | BindingFlags.Instance)
                               ?? provider.GetType().GetField("_cachedScenes", BindingFlags.NonPublic | BindingFlags.Instance);
                if (cachedField?.GetValue(provider) is System.Collections.IDictionary cached)
                    foreach (var value in cached.Values)
                        if (value is BaseHandle handle) handles.Add(handle);
            }
            return handles;
        }

        public void OnUpdate()
        {
            if (!_autoRefresh) return;
            _lastRefreshTime += Time.deltaTime;
            if (_lastRefreshTime >= RefreshInterval) _lastRefreshTime = 0;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;
            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 0.8f, 1f) } };
            _labelStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
        }
    }
}

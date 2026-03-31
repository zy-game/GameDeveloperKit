using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// AssetBundle 资源运行时实现（非 EditorSimulate 模式使用）。
    /// </summary>
    public class AssetBundleResourceRuntime : ResourceRuntimeBase
    {
        private readonly Dictionary<string, AssetBundle> _bundleCache = new(StringComparer.OrdinalIgnoreCase);

        public override UniTask EnsurePackageReadyAsync(ResourcePackageContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WarmupBundleCache(context);
            return UniTask.CompletedTask;
        }

        public override UnityEngine.Object LoadAsset(ResourcePackageContext context, ResourceEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            var resourcesAsset = TryLoadFromResources(entry);
            if (resourcesAsset != null)
            {
                return resourcesAsset;
            }

            var bundle = LoadBundle(context, entry.BundleName);
            if (bundle == null)
            {
                return null;
            }

            var assetName = ResolveBundleAssetName(entry);
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            var loaded = entry.AssetType != null
                ? bundle.LoadAsset(assetName, entry.AssetType)
                : bundle.LoadAsset(assetName);
            if (loaded != null)
            {
                return loaded;
            }

            var fallbackName = Path.GetFileNameWithoutExtension(assetName);
            if (string.Equals(fallbackName, assetName, StringComparison.Ordinal))
            {
                return null;
            }

            return entry.AssetType != null
                ? bundle.LoadAsset(fallbackName, entry.AssetType)
                : bundle.LoadAsset(fallbackName);
        }

        public override UniTask<UnityEngine.Object> LoadAssetAsync(ResourcePackageContext context, ResourceEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(LoadAsset(context, entry));
        }

        public override string ResolveScenePath(ResourcePackageContext context, ResourceEntry entry)
        {
            var bundle = LoadBundle(context, entry?.BundleName);
            if (bundle == null)
            {
                return base.ResolveScenePath(context, entry);
            }

            var scenePaths = bundle.GetAllScenePaths();
            if (scenePaths == null || scenePaths.Length == 0)
            {
                return base.ResolveScenePath(context, entry);
            }

            var nameWithExtension = entry?.Name;
            var fullPath = entry?.FullPath;
            for (var i = 0; i < scenePaths.Length; i++)
            {
                var scenePath = scenePaths[i]?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(nameWithExtension) &&
                    scenePath.EndsWith(nameWithExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return scenePath;
                }

                if (!string.IsNullOrWhiteSpace(fullPath) &&
                    scenePath.EndsWith(fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return scenePath;
                }
            }

            return scenePaths[0];
        }

        protected override string ResolvePathByMode(ResourcePackageContext context, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return context.ResolvePersistentPath(path);
        }

        protected virtual AssetBundle LoadBundle(ResourcePackageContext context, string bundleName)
        {
            var bundlePath = ResolveBundleFilePath(context, bundleName);
            if (string.IsNullOrWhiteSpace(bundlePath) || !File.Exists(bundlePath))
            {
                return null;
            }

            if (_bundleCache.TryGetValue(bundlePath, out var cachedBundle) && cachedBundle != null)
            {
                return cachedBundle;
            }

            var loadedBundle = AssetBundle.LoadFromFile(bundlePath);
            if (loadedBundle == null)
            {
                return null;
            }

            _bundleCache[bundlePath] = loadedBundle;
            return loadedBundle;
        }

        protected virtual void WarmupBundleCache(ResourcePackageContext context)
        {
            if (context == null)
            {
                return;
            }

            var manifestPath = context.ResolvePersistentPath(context.ManifestRelativePath);
            var manifest = ResourceManifestUtility.LoadFromFile(manifestPath);
            var entries = ResourceManifestUtility.ResolveEntries(manifest, context.PackageName);
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.BundleName))
                {
                    continue;
                }

                if (!loaded.Add(entry.BundleName))
                {
                    continue;
                }

                LoadBundle(context, entry.BundleName);
            }
        }

        protected virtual string ResolveBundleFilePath(ResourcePackageContext context, string bundleName)
        {
            if (context == null || string.IsNullOrWhiteSpace(bundleName))
            {
                return string.Empty;
            }

            var token = bundleName.Replace('\\', '/').Trim();
            var bundlesRoot = context.ResolvePersistentPath("bundles");

            var exact = context.ResolvePersistentPath(Path.Combine("bundles", token).Replace('\\', '/'));
            if (File.Exists(exact))
            {
                return exact;
            }

            if (!token.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
            {
                var withExt = exact + ".bundle";
                if (File.Exists(withExt))
                {
                    return withExt;
                }
            }

            if (Directory.Exists(bundlesRoot))
            {
                var prefix = Path.GetFileNameWithoutExtension(token);
                var matches = Directory.GetFiles(bundlesRoot, $"{prefix}*.bundle", SearchOption.TopDirectoryOnly);
                if (matches.Length > 0)
                {
                    return matches[0];
                }
            }

            var direct = context.ResolvePersistentPath(token);
            return File.Exists(direct) ? direct : string.Empty;
        }

        protected static string ResolveBundleAssetName(ResourceEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry?.Name))
            {
                return entry.Name;
            }

            return Path.GetFileName(entry?.FullPath);
        }

        protected static UnityEngine.Object TryLoadFromResources(ResourceEntry entry)
        {
            var key = !string.IsNullOrWhiteSpace(entry?.Name) ? entry.Name : entry?.FullPath;
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var normalized = key.Replace('\\', '/');
            if (!normalized.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var path = normalized.Substring("Resources/".Length);
            var extension = Path.GetExtension(path);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                path = Path.ChangeExtension(path, null);
            }

            return entry.AssetType != null
                ? Resources.Load(path, entry.AssetType)
                : Resources.Load(path);
        }
    }
}

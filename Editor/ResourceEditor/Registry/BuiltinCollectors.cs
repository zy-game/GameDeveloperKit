using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor.Registry
{
    /// <summary>
    /// 定义 Explicit Asset Resource Collector 类型。
    /// </summary>
    [Collector("explicit-assets", "显式资源列表", order: 0, Description = "使用 bundle 配置中维护的资源路径列表。")]
    public sealed class ExplicitAssetCollector : Collector
    {
        /// <summary>
        /// 执行 Collect。
        /// </summary>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
        public override IReadOnlyList<ResourceGroupPreview> Collect(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            return GameDeveloperKit.ResourceEditor.Authoring.EntryPreviewBuilder.Build(bundle);
        }

        /// <summary>
        /// 执行 Normalize Location。
        /// </summary>
        /// <param name="assetPath">asset Path 参数。</param>
        /// <returns>执行结果。</returns>
        internal static string NormalizeLocation(string assetPath)
        {
            return assetPath.Replace('\\', '/');
        }

        internal static string ResolveLocation(string providerId, string assetPath)
        {
            return ResourceProviderIds.IsResources(providerId)
                ? UnityResourcesCollector.ToResourcesLocation(assetPath)
                : NormalizeLocation(assetPath);
        }
    }

    /// <summary>
    /// 定义 Folder Resource Collector 类型。
    /// </summary>
    [Collector("folder-assets", "目录资源", order: 10, Description = "使用 bundle 目录选择器配置的目录收集资源。")]
    public sealed class FolderCollector : Collector
    {
        /// <summary>
        /// 执行 Collect。
        /// </summary>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
        public override IReadOnlyList<ResourceGroupPreview> Collect(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            if (bundle == null)
            {
                return Array.Empty<ResourceGroupPreview>();
            }

            if (AssetDatabase.IsValidFolder(bundle.SourceFolder) is false)
            {
                return Array.Empty<ResourceGroupPreview>();
            }

            var previews = new List<ResourceGroupPreview>();
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { bundle.SourceFolder }))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                var labels = asset == null ? Array.Empty<string>() : AssetDatabase.GetLabels(asset);
                previews.Add(new ResourceGroupPreview(
                    assetPath,
                    GameDeveloperKit.ResourceEditor.Registry.ExplicitAssetCollector.NormalizeLocation(assetPath),
                    type?.Name ?? string.Empty,
                    labels,
                    bundle.Name,
                    bundle.Group));
            }

            return previews;
        }

    }

    [Collector(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ResourcesCollectorId, "Unity Resources", order: 20, Description = "收集运行时 Resources 目录中的资源，生成 Resources/ 相对地址。")]
    public sealed class UnityResourcesCollector : Collector
    {
        public override IReadOnlyList<ResourceGroupPreview> Collect(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            var previews = new List<ResourceGroupPreview>();
            foreach (var folder in EnumerateResourceFolders())
            {
                foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { folder }))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (IsRuntimeResourceAsset(assetPath) is false)
                    {
                        continue;
                    }

                    var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    var labels = asset == null ? Array.Empty<string>() : AssetDatabase.GetLabels(asset);
                    previews.Add(new ResourceGroupPreview(
                        assetPath,
                        ToResourcesLocation(assetPath),
                        type?.Name ?? string.Empty,
                        labels,
                        bundle?.Name,
                        bundle?.Group));
                }
            }

            return previews
                .OrderBy(x => x.Location, StringComparer.Ordinal)
                .ToList();
        }

        internal static string ToResourcesLocation(string assetPath)
        {
            var normalized = GameDeveloperKit.ResourceEditor.Registry.ExplicitAssetCollector.NormalizeLocation(assetPath);
            const string assetsResourcesPrefix = "Assets/Resources/";
            if (normalized.StartsWith(assetsResourcesPrefix, StringComparison.Ordinal))
            {
                var assetsRelative = normalized.Substring(assetsResourcesPrefix.Length);
                return $"Resources/{Path.ChangeExtension(assetsRelative, null)}";
            }

            var resourcesIndex = normalized.LastIndexOf("/Resources/", StringComparison.Ordinal);
            if (resourcesIndex < 0)
            {
                return string.Empty;
            }

            var relative = normalized.Substring(resourcesIndex + "/Resources/".Length);
            return $"Resources/{Path.ChangeExtension(relative, null)}";
        }

        internal static bool IsRuntimeResourceAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var normalized = GameDeveloperKit.ResourceEditor.Registry.ExplicitAssetCollector.NormalizeLocation(assetPath);
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return false;
            }

            if (normalized.Contains("/Editor/", StringComparison.Ordinal))
            {
                return false;
            }

            if (normalized.Contains(".bundle/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return normalized.StartsWith("Assets/Resources/", StringComparison.Ordinal) ||
                   normalized.Contains("/Resources/", StringComparison.Ordinal);
        }

        private static IEnumerable<string> EnumerateResourceFolders()
        {
            return AssetDatabase.FindAssets("t:DefaultAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.IsValidFolder(path))
                .Select(GameDeveloperKit.ResourceEditor.Registry.ExplicitAssetCollector.NormalizeLocation)
                .Where(path => path.EndsWith("/Resources", StringComparison.Ordinal))
                .Where(path => path.Contains("/Editor/", StringComparison.Ordinal) is false)
                .Distinct(StringComparer.Ordinal);
        }
    }
}

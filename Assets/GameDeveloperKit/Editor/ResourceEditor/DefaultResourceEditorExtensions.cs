using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor
{
    [Colletion("explicit-assets", "显式资源列表", order: 0, Description = "使用 bundle 配置中维护的资源路径列表。")]
    public sealed class ExplicitAssetResourceCollector : ResourceCollector
    {
        public override IReadOnlyList<ResourceGroupPreview> Collect(ResourceEditorPackage package, ResourceEditorBundle bundle)
        {
            if (bundle == null || bundle.AssetPaths == null || bundle.AssetPaths.Count == 0)
            {
                return Array.Empty<ResourceGroupPreview>();
            }

            var previews = new List<ResourceGroupPreview>();
            foreach (var assetPath in bundle.AssetPaths)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                var labels = asset == null ? Array.Empty<string>() : AssetDatabase.GetLabels(asset);
                previews.Add(new ResourceGroupPreview(
                    assetPath,
                    NormalizeLocation(assetPath),
                    type?.Name ?? string.Empty,
                    labels,
                    bundle.Name,
                    bundle.Group));
            }

            return previews;
        }

        internal static string NormalizeLocation(string assetPath)
        {
            var location = assetPath.Replace('\\', '/');
            var extension = Path.GetExtension(location);
            if (string.IsNullOrEmpty(extension) is false)
            {
                location = location.Substring(0, location.Length - extension.Length);
            }

            return location;
        }
    }

    [Colletion("folder-assets", "目录资源", order: 10, Description = "使用收集参数或资源路径中配置的目录收集资源。")]
    public sealed class FolderResourceCollector : ResourceCollector
    {
        public override IReadOnlyList<ResourceGroupPreview> Collect(ResourceEditorPackage package, ResourceEditorBundle bundle)
        {
            if (bundle == null)
            {
                return Array.Empty<ResourceGroupPreview>();
            }

            var folders = ResolveFolders(bundle).ToArray();
            if (folders.Length == 0)
            {
                return Array.Empty<ResourceGroupPreview>();
            }

            var previews = new List<ResourceGroupPreview>();
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, folders))
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
                    ExplicitAssetResourceCollector.NormalizeLocation(assetPath),
                    type?.Name ?? string.Empty,
                    labels,
                    bundle.Name,
                    bundle.Group));
            }

            return previews;
        }

        private static IEnumerable<string> ResolveFolders(ResourceEditorBundle bundle)
        {
            foreach (var folder in SplitPaths(bundle.CollectorParameter))
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    yield return folder;
                }
            }

            foreach (var folder in bundle.AssetPaths.Where(AssetDatabase.IsValidFolder))
            {
                yield return folder;
            }
        }

        private static IEnumerable<string> SplitPaths(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => string.IsNullOrWhiteSpace(x) is false);
        }
    }

    [Builded("single-bundle", "单 Bundle", order: 0, Description = "首版只记录打包方式，不执行构建。")]
    public sealed class SingleBundleBuildStrategy : ResourceBuildStrategy
    {
    }

    [Builded("bundle-per-group", "按 Group 分包", order: 10, Description = "首版只记录打包方式，不执行构建。")]
    public sealed class BundlePerGroupBuildStrategy : ResourceBuildStrategy
    {
    }

    public sealed class BasicResourceChecker : ResourceChecker
    {
        public override void Check(ResourceCheckContext context, List<ResourceValidationIssue> issues)
        {
            if (context.Package == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Package.Name))
            {
                issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(BasicResourceChecker), "Package name cannot be empty.", context.Package));
            }

            if (context.Bundle == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Bundle.Name))
            {
                issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(BasicResourceChecker), "Bundle name cannot be empty.", context.Package, context.Bundle));
            }

            if (string.IsNullOrWhiteSpace(context.Bundle.Group))
            {
                issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(BasicResourceChecker), "Bundle group cannot be empty.", context.Package, context.Bundle));
            }

            if (context.Resources.Count == 0)
            {
                issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Warning, nameof(BasicResourceChecker), "Collector did not return resources.", context.Package, context.Bundle));
            }

            foreach (var resource in context.Resources)
            {
                if (string.IsNullOrWhiteSpace(resource.Location))
                {
                    issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(BasicResourceChecker), "Resource location cannot be empty.", context.Package, context.Bundle, resource));
                }
            }
        }
    }

    public sealed class DuplicateResourceChecker : ResourceChecker
    {
        public override void Check(ResourceCheckContext context, List<ResourceValidationIssue> issues)
        {
            if (context.Settings == null || context.Bundle == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Bundle.Name) is false)
            {
                var duplicatedBundleCount = context.Settings.Packages
                    .Where(package => package != null)
                    .SelectMany(package => package.Bundles)
                    .Count(bundle => bundle != null && bundle.Name == context.Bundle.Name);

                if (duplicatedBundleCount > 1)
                {
                    issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(DuplicateResourceChecker), $"Duplicate bundle name: {context.Bundle.Name}", context.Package, context.Bundle));
                }
            }

            foreach (var resource in context.Resources)
            {
                if (string.IsNullOrWhiteSpace(resource.Location))
                {
                    continue;
                }

                var duplicatedAssetCount = context.Previews == null
                    ? context.Resources.Count(x => x.Location == resource.Location)
                    : context.Previews.SelectMany(x => x.Value).Count(x => x.Location == resource.Location);

                if (duplicatedAssetCount > 1)
                {
                    issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(DuplicateResourceChecker), $"Duplicate asset location: {resource.Location}", context.Package, context.Bundle, resource));
                }
            }
        }
    }

    public sealed class DependencyResourceChecker : ResourceChecker
    {
        public override void Check(ResourceCheckContext context, List<ResourceValidationIssue> issues)
        {
            if (context.Settings == null || context.Bundle == null)
            {
                return;
            }

            var bundleNames = new HashSet<string>(
                context.Settings.Packages
                    .Where(package => package != null)
                    .SelectMany(package => package.Bundles)
                    .Where(bundle => bundle != null && string.IsNullOrWhiteSpace(bundle.Name) is false)
                    .Select(bundle => bundle.Name));

            foreach (var dependency in context.Bundle.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dependency))
                {
                    continue;
                }

                if (bundleNames.Contains(dependency) is false)
                {
                    issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(DependencyResourceChecker), $"Missing dependency bundle: {dependency}", context.Package, context.Bundle));
                }
            }
        }
    }
}

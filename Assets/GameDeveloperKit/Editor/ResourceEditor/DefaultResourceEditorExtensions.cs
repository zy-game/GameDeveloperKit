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

    [Colletion("folder-assets", "目录资源", order: 10, Description = "使用 bundle 目录选择器配置的目录收集资源。")]
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
            if (AssetDatabase.IsValidFolder(bundle.SourceFolder))
            {
                yield return bundle.SourceFolder;
            }

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

    [Builded("single-bundle", "单 Bundle", order: 0, Description = "每个 bundle 配置生成一个 AssetBundle 构建计划。")]
    public sealed class SingleBundleBuildStrategy : ResourceBuildStrategy
    {
        public override ResourceBuildPlan CreatePlan(ResourceBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var plan = new ResourceBuildPlan();
            foreach (var package in context.Packages.Where(x => x != null))
            {
                foreach (var bundle in package.Bundles.Where(x => x != null))
                {
                    var resources = GetResources(context, bundle);
                    var bundleName = CreateBundleBuildName(package, bundle, resources, "single-bundle");
                    plan.AddBundle(new ResourceBuildPlanBundle(package, bundle, bundleName, resources));
                }
            }

            return plan;
        }

        internal static IReadOnlyList<ResourceGroupPreview> GetResources(ResourceBuildContext context, ResourceEditorBundle bundle)
        {
            return context.Previews != null && context.Previews.TryGetValue(bundle, out var resources)
                ? resources
                : Array.Empty<ResourceGroupPreview>();
        }

        internal static string CreateBundleBuildName(ResourceEditorPackage package, ResourceEditorBundle bundle, IReadOnlyList<ResourceGroupPreview> resources, string strategy)
        {
            var payload = string.Join("\n", new[]
                {
                    strategy ?? string.Empty,
                    package?.Name ?? string.Empty,
                    bundle?.Name ?? string.Empty,
                    bundle?.Group ?? string.Empty
                }
                .Concat((resources ?? Array.Empty<ResourceGroupPreview>())
                    .Where(resource => resource != null)
                    .OrderBy(resource => resource.AssetPath, StringComparer.Ordinal)
                    .Select(resource => $"{resource.AssetPath}|{resource.Location}|{resource.TypeName}")));

            return $"{ResourceBuildUtilities.ComputeHashFromText(payload)}.bundle";
        }
    }

    [Builded("bundle-per-group", "按 Group 分包", order: 10, Description = "按 bundle group 生成 AssetBundle 构建计划。")]
    public sealed class BundlePerGroupBuildStrategy : ResourceBuildStrategy
    {
        public override ResourceBuildPlan CreatePlan(ResourceBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var plan = new ResourceBuildPlan();
            foreach (var package in context.Packages.Where(x => x != null))
            {
                foreach (var bundle in package.Bundles.Where(x => x != null))
                {
                    var resources = SingleBundleBuildStrategy.GetResources(context, bundle);
                    var bundleName = SingleBundleBuildStrategy.CreateBundleBuildName(package, bundle, resources, "bundle-per-group");
                    plan.AddBundle(new ResourceBuildPlanBundle(package, bundle, bundleName, resources));
                }
            }

            return plan;
        }
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

}

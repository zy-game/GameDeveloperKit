using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Explicit Asset Resource Collector 类型。
    /// </summary>
    [Colletion("explicit-assets", "显式资源列表", order: 0, Description = "使用 bundle 配置中维护的资源路径列表。")]
    public sealed class ExplicitAssetResourceCollector : ResourceCollector
    {
        /// <summary>
        /// 执行 Collect。
        /// </summary>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 Normalize Location。
        /// </summary>
        /// <param name="assetPath">asset Path 参数。</param>
        /// <returns>执行结果。</returns>
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

    /// <summary>
    /// 定义 Folder Resource Collector 类型。
    /// </summary>
    [Colletion("folder-assets", "目录资源", order: 10, Description = "使用 bundle 目录选择器配置的目录收集资源。")]
    public sealed class FolderResourceCollector : ResourceCollector
    {
        /// <summary>
        /// 执行 Collect。
        /// </summary>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 解析 Folders。
        /// </summary>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 Split Paths。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<string> SplitPaths(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => string.IsNullOrWhiteSpace(x) is false);
        }
    }

    /// <summary>
    /// 定义 Single Bundle Build Strategy 类型。
    /// </summary>
    [Builded("single-bundle", "单 Bundle", order: 0, Description = "每个 bundle 配置生成一个 AssetBundle 构建计划。")]
    public sealed class SingleBundleBuildStrategy : ResourceBuildStrategy
    {
        /// <summary>
        /// 创建 Plan。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 获取 Resources。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
        internal static IReadOnlyList<ResourceGroupPreview> GetResources(ResourceBuildContext context, ResourceEditorBundle bundle)
        {
            return context.Previews != null && context.Previews.TryGetValue(bundle, out var resources)
                ? resources
                : Array.Empty<ResourceGroupPreview>();
        }

        /// <summary>
        /// 创建 Bundle Build Name。
        /// </summary>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <param name="resources">resources 参数。</param>
        /// <param name="strategy">strategy 参数。</param>
        /// <returns>执行结果。</returns>
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

    /// <summary>
    /// 定义 Bundle Per Group Build Strategy 类型。
    /// </summary>
    [Builded("bundle-per-group", "按 Group 分包", order: 10, Description = "按 bundle group 生成 AssetBundle 构建计划。")]
    public sealed class BundlePerGroupBuildStrategy : ResourceBuildStrategy
    {
        /// <summary>
        /// 创建 Plan。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <returns>执行结果。</returns>
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

    /// <summary>
    /// 定义 Basic Resource Checker 类型。
    /// </summary>
    public sealed class BasicResourceChecker : ResourceChecker
    {
        /// <summary>
        /// 执行 Check。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="issues">issues 参数。</param>
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

    /// <summary>
    /// 定义 Duplicate Resource Checker 类型。
    /// </summary>
    public sealed class DuplicateResourceChecker : ResourceChecker
    {
        /// <summary>
        /// 执行 Check。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="issues">issues 参数。</param>
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

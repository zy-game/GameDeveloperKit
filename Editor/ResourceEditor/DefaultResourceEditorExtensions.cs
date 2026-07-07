using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
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
            return assetPath.Replace('\\', '/');
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

    [Colletion(ResourceEditorBuiltinConstants.ResourcesCollectorId, "Unity Resources", order: 20, Description = "收集运行时 Resources 目录中的资源，生成 Resources/ 相对地址。")]
    public sealed class UnityResourcesCollector : ResourceCollector
    {
        public override IReadOnlyList<ResourceGroupPreview> Collect(ResourceEditorPackage package, ResourceEditorBundle bundle)
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
            var normalized = ExplicitAssetResourceCollector.NormalizeLocation(assetPath);
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

            var normalized = ExplicitAssetResourceCollector.NormalizeLocation(assetPath);
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
                .Select(ExplicitAssetResourceCollector.NormalizeLocation)
                .Where(path => path.EndsWith("/Resources", StringComparison.Ordinal))
                .Where(path => path.Contains("/Editor/", StringComparison.Ordinal) is false)
                .Distinct(StringComparer.Ordinal);
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
                    if (ShouldSkipEmptyBundle(bundle, resources))
                    {
                        continue;
                    }

                    var bundleName = CreateBundleBuildName(package, bundle, resources, "single-bundle");
                    plan.AddBundle(new ResourceBuildPlanBundle(package, bundle, bundleName, resources));
                }
            }

            return plan;
        }

        /// <summary>
        /// AssetBundle 型 bundle 在没有任何可打包资源（例如条目全部被排除）时跳过，
        /// 避免构建校验因空资源而失败。
        /// </summary>
        /// <param name="bundle">bundle 参数。</param>
        /// <param name="resources">resources 参数。</param>
        /// <returns>需要跳过时返回 true。</returns>
        internal static bool ShouldSkipEmptyBundle(ResourceEditorBundle bundle, IReadOnlyList<ResourceGroupPreview> resources)
        {
            return bundle != null
                && ResourceProviderIds.IsAssetBundle(bundle.ProviderId)
                && (resources == null || resources.Count == 0);
        }

        /// <summary>
        /// 获取 Resources。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
        internal static IReadOnlyList<ResourceGroupPreview> GetResources(ResourceBuildContext context, ResourceEditorBundle bundle)
        {
            return context.GetResources(bundle);
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
                    if (SingleBundleBuildStrategy.ShouldSkipEmptyBundle(bundle, resources))
                    {
                        continue;
                    }

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

            if (ResourceProviderIds.IsResources(context.Bundle.ProviderId) is false &&
                ResourceProviderIds.IsAssetBundle(context.Bundle.ProviderId) is false)
            {
                issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(BasicResourceChecker), $"Unsupported provider: {context.Bundle.ProviderId}", context.Package, context.Bundle));
            }

            if (context.Resources.Count == 0)
            {
                issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Warning, nameof(BasicResourceChecker), "Group has no asset entries.", context.Package, context.Bundle));
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

    public sealed class BuiltinResourceChecker : ResourceChecker
    {
        public override void Check(ResourceCheckContext context, List<ResourceValidationIssue> issues)
        {
            if (context.Package == null || context.Bundle == null)
            {
                return;
            }

            if (ResourceEditorBuiltinConstants.IsBuiltinPackage(context.Package))
            {
                CheckBuiltinPackage(context, issues);
            }

            if (ResourceProviderIds.IsResources(context.Bundle.ProviderId))
            {
                CheckResourcesProvider(context, issues);
                return;
            }

            if (ResourceProviderIds.IsAssetBundle(context.Bundle.ProviderId))
            {
                CheckAssetBundleProvider(context, issues);
            }
        }

        private static void CheckBuiltinPackage(ResourceCheckContext context, List<ResourceValidationIssue> issues)
        {
            if (context.Package.IsHotUpdate)
            {
                issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(BuiltinResourceChecker), $"{ResourceEditorBuiltinConstants.PackageName} cannot be hot update.", context.Package));
            }

        }

        private static void CheckResourcesProvider(ResourceCheckContext context, List<ResourceValidationIssue> issues)
        {
            foreach (var resource in context.Resources)
            {
                if (resource == null)
                {
                    continue;
                }

                if (resource.Location.StartsWith("Resources/", StringComparison.Ordinal) is false)
                {
                    issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(BuiltinResourceChecker), $"Resources provider location must start with Resources/: {resource.Location}", context.Package, context.Bundle, resource));
                }

                if (Path.HasExtension(resource.Location))
                {
                    issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(BuiltinResourceChecker), $"Resources provider location must not include extension: {resource.Location}", context.Package, context.Bundle, resource));
                }

                if (UnityResourcesCollector.IsRuntimeResourceAsset(resource.AssetPath) is false)
                {
                    issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(BuiltinResourceChecker), $"Resources provider asset is not a runtime Resources asset: {resource.AssetPath}", context.Package, context.Bundle, resource));
                }
            }
        }

        private static void CheckAssetBundleProvider(ResourceCheckContext context, List<ResourceValidationIssue> issues)
        {
            foreach (var resource in context.Resources)
            {
                if (resource == null || UnityResourcesCollector.IsRuntimeResourceAsset(resource.AssetPath) is false)
                {
                    continue;
                }

                issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Warning, nameof(BuiltinResourceChecker), $"Resources asset assigned to asset-bundle group may be duplicated in player build: {resource.AssetPath}", context.Package, context.Bundle, resource));
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
                if (resource == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(resource.AssetPath) is false)
                {
                    var duplicatedAssetPathCount = context.Previews == null
                        ? context.Resources.Count(x => x != null && x.AssetPath == resource.AssetPath)
                        : context.Previews.SelectMany(x => x.Value).Count(x => x != null && x.AssetPath == resource.AssetPath);

                    if (duplicatedAssetPathCount > 1)
                    {
                        issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(DuplicateResourceChecker), $"Duplicate asset path: {resource.AssetPath}", context.Package, context.Bundle, resource));
                    }
                }

                if (string.IsNullOrWhiteSpace(resource.Location))
                {
                    continue;
                }

                var duplicatedAssetCount = context.Previews == null
                    ? context.Resources.Count(x => x != null && x.Location == resource.Location)
                    : context.Previews.SelectMany(x => x.Value).Count(x => x != null && x.Location == resource.Location);

                if (duplicatedAssetCount > 1)
                {
                    issues.Add(new ResourceValidationIssue(ResourceValidationSeverity.Error, nameof(DuplicateResourceChecker), $"Duplicate asset location: {resource.Location}", context.Package, context.Bundle, resource));
                }
            }
        }
    }

}

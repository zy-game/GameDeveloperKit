using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor.Validation
{
    public sealed class BasicChecker : Checker
    {
        /// <summary>
        /// 执行 Check。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="issues">issues 参数。</param>
        public override void Check(GameDeveloperKit.ResourceEditor.Validation.CheckContext context, List<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            if (context.Package == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Package.Name))
            {
                issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.BasicChecker), "Package name cannot be empty.", context.Package));
            }

            if (context.Bundle == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Bundle.Name))
            {
                issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.BasicChecker), "Bundle name cannot be empty.", context.Package, context.Bundle));
            }

            if (string.IsNullOrWhiteSpace(context.Bundle.Group))
            {
                issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.BasicChecker), "Bundle group cannot be empty.", context.Package, context.Bundle));
            }

            if (ResourceProviderIds.IsResources(context.Bundle.ProviderId) is false &&
                ResourceProviderIds.IsAssetBundle(context.Bundle.ProviderId) is false)
            {
                issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.BasicChecker), $"Unsupported provider: {context.Bundle.ProviderId}", context.Package, context.Bundle));
            }

            if (string.Equals(context.Bundle.CollectorId, GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(context.Bundle.SourceFolder) || AssetDatabase.IsValidFolder(context.Bundle.SourceFolder) is false)
                {
                    issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.BasicChecker), "Folder collector requires one valid Project folder.", context.Package, context.Bundle));
                }
                else
                {
                    if (GameDeveloperKit.ResourceEditor.Authoring.FolderOwnership.TryFindConflict(
                            context.Settings,
                            context.Bundle,
                            context.Bundle.SourceFolder,
                            out var conflictingGroup))
                    {
                        issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(
                            GameDeveloperKit.ResourceEditor.Validation.Severity.Error,
                            nameof(GameDeveloperKit.ResourceEditor.Validation.BasicChecker),
                            $"Source folder overlaps Group '{conflictingGroup.Name}': {context.Bundle.SourceFolder} <-> {conflictingGroup.SourceFolder}",
                            context.Package,
                            context.Bundle));
                    }
                }
            }

            if (context.Resources.Count == 0)
            {
                issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Warning, nameof(GameDeveloperKit.ResourceEditor.Validation.BasicChecker), "Group has no asset entries.", context.Package, context.Bundle));
            }

            foreach (var resource in context.Resources)
            {
                if (string.IsNullOrWhiteSpace(resource.Location))
                {
                    issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.BasicChecker), "Resource location cannot be empty.", context.Package, context.Bundle, resource));
                }
            }
        }
    }

    public sealed class BuiltinChecker : Checker
    {
        public override void Check(GameDeveloperKit.ResourceEditor.Validation.CheckContext context, List<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            if (context.Package == null || context.Bundle == null)
            {
                return;
            }

            if (GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(context.Package))
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

        private static void CheckBuiltinPackage(GameDeveloperKit.ResourceEditor.Validation.CheckContext context, List<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            if (context.Package.IsHotUpdate)
            {
                issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.BuiltinChecker), $"{GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.PackageName} cannot be hot update.", context.Package));
            }

        }

        private static void CheckResourcesProvider(GameDeveloperKit.ResourceEditor.Validation.CheckContext context, List<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            foreach (var resource in context.Resources)
            {
                if (resource == null)
                {
                    continue;
                }

                var expectedLocation = GameDeveloperKit.ResourceEditor.Registry.ExplicitAssetCollector.ResolveLocation(
                    ResourceProviderIds.Resources,
                    resource.AssetPath);
                var actualLocation = resource.Location ?? string.Empty;
                if (string.Equals(actualLocation, expectedLocation, StringComparison.Ordinal) is false)
                {
                    issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(
                        GameDeveloperKit.ResourceEditor.Validation.Severity.Error,
                        nameof(GameDeveloperKit.ResourceEditor.Validation.BuiltinChecker),
                        $"Resources provider location must be derived from the current asset path: {resource.AssetPath}. Expected: {expectedLocation}. Actual: {actualLocation}",
                        context.Package,
                        context.Bundle,
                        resource));
                }

                if (actualLocation.StartsWith("Resources/", StringComparison.Ordinal) is false)
                {
                    issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.BuiltinChecker), $"Resources provider location must start with Resources/: {actualLocation}", context.Package, context.Bundle, resource));
                }

                if (Path.HasExtension(actualLocation))
                {
                    issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.BuiltinChecker), $"Resources provider location must not include extension: {actualLocation}", context.Package, context.Bundle, resource));
                }

                if (GameDeveloperKit.ResourceEditor.Registry.UnityResourcesCollector.IsRuntimeResourceAsset(resource.AssetPath) is false)
                {
                    issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.BuiltinChecker), $"Resources provider asset is not a runtime Resources asset: {resource.AssetPath}", context.Package, context.Bundle, resource));
                }
            }
        }

        private static void CheckAssetBundleProvider(GameDeveloperKit.ResourceEditor.Validation.CheckContext context, List<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            foreach (var resource in context.Resources)
            {
                if (resource == null)
                {
                    continue;
                }

                var expectedLocation = GameDeveloperKit.ResourceEditor.Registry.ExplicitAssetCollector.ResolveLocation(
                    ResourceProviderIds.AssetBundle,
                    resource.AssetPath);
                if (string.Equals(resource.Location, expectedLocation, StringComparison.Ordinal) is false)
                {
                    issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(
                        GameDeveloperKit.ResourceEditor.Validation.Severity.Error,
                        nameof(GameDeveloperKit.ResourceEditor.Validation.BuiltinChecker),
                        $"AssetBundle provider location must match the current asset path: {resource.AssetPath}. Actual: {resource.Location}",
                        context.Package,
                        context.Bundle,
                        resource));
                }

                if (GameDeveloperKit.ResourceEditor.Registry.UnityResourcesCollector.IsRuntimeResourceAsset(resource.AssetPath) is false)
                {
                    continue;
                }

                issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Warning, nameof(GameDeveloperKit.ResourceEditor.Validation.BuiltinChecker), $"Resources asset assigned to asset-bundle group may be duplicated in player build: {resource.AssetPath}", context.Package, context.Bundle, resource));
            }
        }
    }

    /// <summary>
    /// 定义 Duplicate Resource Checker 类型。
    /// </summary>
    public sealed class DuplicateChecker : Checker
    {
        /// <summary>
        /// 执行 Check。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="issues">issues 参数。</param>
        public override void Check(GameDeveloperKit.ResourceEditor.Validation.CheckContext context, List<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
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
                    issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.DuplicateChecker), $"Duplicate bundle name: {context.Bundle.Name}", context.Package, context.Bundle));
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
                        issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.DuplicateChecker), $"Duplicate asset path: {resource.AssetPath}", context.Package, context.Bundle, resource));
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
                    issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(GameDeveloperKit.ResourceEditor.Validation.Severity.Error, nameof(GameDeveloperKit.ResourceEditor.Validation.DuplicateChecker), $"Duplicate asset location: {resource.Location}", context.Package, context.Bundle, resource));
                }
            }
        }
    }

}

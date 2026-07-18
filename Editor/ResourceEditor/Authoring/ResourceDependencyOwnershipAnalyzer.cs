using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using IOFile = System.IO.File;
using IOFileInfo = System.IO.FileInfo;

namespace GameDeveloperKit.ResourceEditor
{
    internal static class ResourceDependencyOwnershipAnalyzer
    {
        internal const string IssueSource = "ResourceDependencyOwnership";

        public static void Analyze(
            ResourceEditorSettings settings,
            IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews,
            ICollection<ResourceValidationIssue> issues)
        {
            Analyze(
                settings,
                previews,
                assetPaths => AssetDatabase.GetDependencies(assetPaths.ToArray(), true),
                GetAssetSize,
                issues);
        }

        internal static void Analyze(
            ResourceEditorSettings settings,
            IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews,
            Func<IReadOnlyList<string>, IReadOnlyList<string>> dependencyResolver,
            Func<string, long> sizeResolver,
            ICollection<ResourceValidationIssue> issues)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (previews == null)
            {
                throw new ArgumentNullException(nameof(previews));
            }

            if (dependencyResolver == null)
            {
                throw new ArgumentNullException(nameof(dependencyResolver));
            }

            if (sizeResolver == null)
            {
                throw new ArgumentNullException(nameof(sizeResolver));
            }

            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            var packages = BuildPackageLookup(settings);
            var explicitAssets = new HashSet<string>(
                previews.Values.SelectMany(resources => resources ?? new List<ResourceGroupPreview>())
                    .Where(resource => resource != null && string.IsNullOrWhiteSpace(resource.AssetPath) is false)
                    .Select(resource => NormalizePath(resource.AssetPath)),
                StringComparer.Ordinal);
            var owners = new Dictionary<string, Dictionary<ResourceEditorBundle, ResourceGroupPreview>>(StringComparer.Ordinal);
            foreach (var pair in previews)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    continue;
                }

                var resources = pair.Value
                    .Where(resource => resource != null && string.IsNullOrWhiteSpace(resource.AssetPath) is false)
                    .ToArray();
                var rootPaths = resources
                    .Select(resource => NormalizePath(resource.AssetPath))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (rootPaths.Length == 0)
                {
                    continue;
                }

                var rootPathSet = new HashSet<string>(rootPaths, StringComparer.Ordinal);
                foreach (var dependency in dependencyResolver(rootPaths) ?? Array.Empty<string>())
                {
                    var dependencyPath = NormalizePath(dependency);
                    if (rootPathSet.Contains(dependencyPath) ||
                        explicitAssets.Contains(dependencyPath) ||
                        IsPackableDependency(dependencyPath) is false)
                    {
                        continue;
                    }

                    if (owners.TryGetValue(dependencyPath, out var dependencyOwners) is false)
                    {
                        dependencyOwners = new Dictionary<ResourceEditorBundle, ResourceGroupPreview>();
                        owners.Add(dependencyPath, dependencyOwners);
                    }

                    dependencyOwners.TryAdd(pair.Key, resources[0]);
                }
            }

            foreach (var pair in owners.Where(pair => pair.Value.Count > 1).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var orderedOwners = pair.Value.Keys
                    .Select(bundle => new Owner(packages.TryGetValue(bundle, out var package) ? package : null, bundle))
                    .OrderBy(owner => owner.Package?.Name ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(owner => owner.Bundle.Name ?? string.Empty, StringComparer.Ordinal)
                    .ToArray();
                var ownerNames = string.Join(", ", orderedOwners.Select(owner =>
                    $"{owner.Package?.Name ?? "<unknown>"}/{owner.Bundle.Name}"));
                var size = Math.Max(0L, sizeResolver(pair.Key));
                var firstOwner = orderedOwners[0];
                issues.Add(new ResourceValidationIssue(
                    ResourceValidationSeverity.Warning,
                    IssueSource,
                    $"Implicit dependency is owned by {orderedOwners.Length} bundles: {pair.Key}. " +
                    $"Estimated duplicated source size: {size} bytes. Owners: {ownerNames}",
                    firstOwner.Package,
                    firstOwner.Bundle,
                    pair.Value[firstOwner.Bundle]));
            }
        }

        private static Dictionary<ResourceEditorBundle, ResourceEditorPackage> BuildPackageLookup(ResourceEditorSettings settings)
        {
            var result = new Dictionary<ResourceEditorBundle, ResourceEditorPackage>();
            foreach (var package in settings.Packages ?? Enumerable.Empty<ResourceEditorPackage>())
            {
                foreach (var bundle in package?.Bundles ?? Enumerable.Empty<ResourceEditorBundle>())
                {
                    if (bundle != null)
                    {
                        result[bundle] = package;
                    }
                }
            }

            return result;
        }

        private static bool IsPackableDependency(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                (assetPath.StartsWith("Assets/", StringComparison.Ordinal) is false &&
                 assetPath.StartsWith("Packages/", StringComparison.Ordinal) is false))
            {
                return false;
            }

            switch (Path.GetExtension(assetPath).ToLowerInvariant())
            {
                case ".cs":
                case ".dll":
                case ".asmdef":
                case ".asmref":
                    return false;
                default:
                    return true;
            }
        }

        private static long GetAssetSize(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            return IOFile.Exists(fullPath) ? new IOFileInfo(fullPath).Length : 0L;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private readonly struct Owner
        {
            public Owner(ResourceEditorPackage package, ResourceEditorBundle bundle)
            {
                Package = package;
                Bundle = bundle;
            }

            public ResourceEditorPackage Package { get; }

            public ResourceEditorBundle Bundle { get; }
        }
    }
}

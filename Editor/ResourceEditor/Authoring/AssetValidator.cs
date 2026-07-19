using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    internal static class AssetValidator
    {
        public static Dictionary<Bundle, List<ResourceGroupPreview>> ResolvePreviews(
            Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            ICollection<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            var previews = new Dictionary<Bundle, List<ResourceGroupPreview>>();
            var resolvedEntries = new List<ResolvedEntry>();
            var identityCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var package in settings.Packages ?? Enumerable.Empty<Package>())
            {
                if (package?.Bundles == null)
                {
                    continue;
                }

                foreach (var bundle in package.Bundles)
                {
                    if (bundle == null)
                    {
                        continue;
                    }

                    previews[bundle] = new List<ResourceGroupPreview>();
                    ResolveBundle(package, bundle, registry, resolvedEntries, identityCounts, issues);
                }
            }

            foreach (var resolvedEntry in resolvedEntries)
            {
                if (identityCounts[resolvedEntry.Guid] > 1)
                {
                    AddError(
                        issues,
                        $"Configured resource GUID has multiple active memberships: {resolvedEntry.Guid}",
                        resolvedEntry.Package,
                        resolvedEntry.Bundle,
                        resolvedEntry.Preview);
                    continue;
                }

                previews[resolvedEntry.Bundle].Add(resolvedEntry.Preview);
            }

            return previews;
        }

        private static void ResolveBundle(
            Package package,
            Bundle bundle,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            ICollection<ResolvedEntry> resolvedEntries,
            IDictionary<string, int> identityCounts,
            ICollection<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            if (bundle?.Entries == null)
            {
                return;
            }

            var filterRule = registry.GetFilterRule(bundle.FilterRuleId);
            if (filterRule == null || HasFilterRuleError(issues, package, bundle))
            {
                return;
            }

            foreach (var entry in bundle.Entries)
            {
                if (entry == null || entry.Excluded)
                {
                    continue;
                }

                if (TryResolveAsset(entry, out var guid, out var assetPath, out var error) is false)
                {
                    AddError(issues, error, package, bundle, CreateStoredPreview(bundle, entry));
                    continue;
                }

                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    AddError(
                        issues,
                        $"Configured resource is a folder, not an asset: {assetPath}",
                        package,
                        bundle,
                        CreateStoredPreview(bundle, entry, assetPath));
                    continue;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                var labels = asset == null ? Array.Empty<string>() : AssetDatabase.GetLabels(asset);
                var preview = new ResourceGroupPreview(
                    assetPath,
                    entry.Location,
                    type?.Name ?? string.Empty,
                    labels
                        .Where(label => string.IsNullOrWhiteSpace(label) is false)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    bundle.Name,
                    bundle.Group);
                bool isMatch;
                try
                {
                    isMatch = filterRule.Instance.IsMatch(package, bundle, preview);
                }
                catch (Exception exception)
                {
                    Service.AddFilterRuleError(issues, package, bundle, filterRule.Id, exception);
                    return;
                }

                if (isMatch is false)
                {
                    continue;
                }

                resolvedEntries.Add(new ResolvedEntry(package, bundle, guid, preview));
                identityCounts[guid] = identityCounts.TryGetValue(guid, out var count) ? count + 1 : 1;
            }
        }

        private static bool HasFilterRuleError(
            IEnumerable<GameDeveloperKit.ResourceEditor.Validation.Issue> issues,
            Package package,
            Bundle bundle)
        {
            return issues.Any(issue =>
                issue.Severity == GameDeveloperKit.ResourceEditor.Validation.Severity.Error &&
                string.Equals(issue.Source, "FilterRule", StringComparison.Ordinal) &&
                ReferenceEquals(issue.Package, package) &&
                ReferenceEquals(issue.Bundle, bundle));
        }

        private static bool TryResolveAsset(
            AssetEntry entry,
            out string guid,
            out string assetPath,
            out string error)
        {
            guid = entry.Guid;
            assetPath = string.Empty;
            error = null;
            if (string.IsNullOrWhiteSpace(guid))
            {
                error = "Configured resource GUID cannot be empty.";
                return false;
            }

            if (GUID.TryParse(guid, out _) is false)
            {
                error = $"Configured resource GUID is invalid: {guid}";
                return false;
            }

            guid = guid.ToLowerInvariant();
            assetPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = $"Configured resource GUID cannot be resolved: {guid}";
                return false;
            }

            return true;
        }

        private static ResourceGroupPreview CreateStoredPreview(
            Bundle bundle,
            AssetEntry entry,
            string assetPath = null)
        {
            return new ResourceGroupPreview(
                assetPath ?? entry.AssetPath,
                entry.Location,
                entry.TypeName,
                entry.Labels ?? (IReadOnlyList<string>)Array.Empty<string>(),
                bundle.Name,
                bundle.Group);
        }

        private static void AddError(
            ICollection<GameDeveloperKit.ResourceEditor.Validation.Issue> issues,
            string message,
            Package package,
            Bundle bundle,
            ResourceGroupPreview resource)
        {
            issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(
                GameDeveloperKit.ResourceEditor.Validation.Severity.Error,
                Service.IssueSource,
                message,
                package,
                bundle,
                resource));
        }

        private sealed class ResolvedEntry
        {
            public ResolvedEntry(
                Package package,
                Bundle bundle,
                string guid,
                ResourceGroupPreview preview)
            {
                Package = package;
                Bundle = bundle;
                Guid = guid;
                Preview = preview;
            }

            public Package Package { get; }

            public Bundle Bundle { get; }

            public string Guid { get; }

            public ResourceGroupPreview Preview { get; }
        }
    }
}

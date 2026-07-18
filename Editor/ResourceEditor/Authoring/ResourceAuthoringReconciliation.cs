using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor
{
    internal static class ResourceAuthoringReconciliation
    {
        private const string ExplicitCollectorId = "explicit-assets";

        public static bool Reconcile(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry,
            ResourceAssetChangeSet changes)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            if (changes.FullReconcile is false &&
                changes.ImportedAssets.Count == 0 &&
                changes.DeletedAssets.Count == 0 &&
                changes.MovedAssets.Count == 0)
            {
                return false;
            }

            var changed = false;
            var deletedPaths = new HashSet<string>(changes.DeletedAssets, StringComparer.Ordinal);
            foreach (var package in settings.Packages.Where(package => package != null))
            {
                foreach (var bundle in package.Bundles.Where(bundle => bundle != null))
                {
                    changed |= RemoveUnavailableEntries(bundle, deletedPaths, changes.FullReconcile);
                    var collectorId = ResolveCollectorId(package, bundle);
                    var collector = registry.GetCollector(collectorId);
                    if (string.Equals(collectorId, ExplicitCollectorId, StringComparison.Ordinal))
                    {
                        changed |= ReconcileExplicitBundle(bundle);
                        continue;
                    }

                    if (collector != null)
                    {
                        changed |= ReconcileRuleBundle(settings, package, bundle, collector);
                    }
                }
            }

            return changed;
        }

        private static bool RemoveUnavailableEntries(
            ResourceEditorBundle bundle,
            ISet<string> deletedPaths,
            bool fullReconcile)
        {
            var changed = false;
            foreach (var entry in bundle.Entries.Where(entry => entry != null).ToList())
            {
                if (deletedPaths.Contains(entry.AssetPath))
                {
                    bundle.Entries.Remove(entry);
                    changed = true;
                    continue;
                }

                if (fullReconcile is false ||
                    string.IsNullOrWhiteSpace(entry.Guid) ||
                    GUID.TryParse(entry.Guid, out _) is false)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(AssetDatabase.GUIDToAssetPath(entry.Guid)))
                {
                    bundle.Entries.Remove(entry);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool ReconcileExplicitBundle(ResourceEditorBundle bundle)
        {
            var changed = false;
            foreach (var entry in bundle.Entries.Where(entry => entry != null).ToList())
            {
                if (string.IsNullOrWhiteSpace(entry.Guid))
                {
                    continue;
                }

                var assetPath = AssetDatabase.GUIDToAssetPath(entry.Guid).Replace('\\', '/');
                changed |= RefreshEntryMetadata(bundle, entry, assetPath);
            }

            return changed;
        }

        private static bool ReconcileRuleBundle(
            ResourceEditorSettings settings,
            ResourceEditorPackage package,
            ResourceEditorBundle bundle,
            ResourceCollectorDescriptor collector)
        {
            var collected = collector.Instance.Collect(package, bundle)
                .Where(preview => preview != null && string.IsNullOrWhiteSpace(preview.AssetPath) is false)
                .OrderBy(preview => preview.AssetPath, StringComparer.Ordinal)
                .ToList();
            var collectedByGuid = new Dictionary<string, ResourceGroupPreview>(StringComparer.Ordinal);
            foreach (var preview in collected)
            {
                var guid = AssetDatabase.AssetPathToGUID(preview.AssetPath);
                if (string.IsNullOrWhiteSpace(guid) || AssetDatabase.IsValidFolder(preview.AssetPath))
                {
                    continue;
                }

                if (collectedByGuid.ContainsKey(guid) is false)
                {
                    collectedByGuid.Add(guid, preview);
                }
            }

            var changed = false;
            foreach (var entry in bundle.Entries.Where(entry => entry != null).ToList())
            {
                if (string.IsNullOrWhiteSpace(entry.Guid))
                {
                    continue;
                }

                var assetPath = AssetDatabase.GUIDToAssetPath(entry.Guid).Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                if (collectedByGuid.Remove(entry.Guid))
                {
                    changed |= RefreshEntryMetadata(bundle, entry, assetPath);
                    continue;
                }

                bundle.Entries.Remove(entry);
                changed = true;
            }

            foreach (var pair in collectedByGuid)
            {
                if (HasActiveMembership(settings, pair.Key))
                {
                    continue;
                }

                bundle.Entries.Add(CreateEntry(bundle, pair.Key, pair.Value));
                changed = true;
            }

            return changed;
        }

        private static bool RefreshEntryMetadata(
            ResourceEditorBundle bundle,
            ResourceEditorAssetEntry entry,
            string resolvedPath = null)
        {
            var assetPath = resolvedPath ?? AssetDatabase.GUIDToAssetPath(entry.Guid).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var typeName = AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.Name ?? string.Empty;
            var labels = (asset == null ? Array.Empty<string>() : AssetDatabase.GetLabels(asset))
                .Where(label => string.IsNullOrWhiteSpace(label) is false)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToArray();
            var location = ResourceProviderIds.IsResources(bundle.ProviderId)
                ? UnityResourcesCollector.ToResourcesLocation(assetPath)
                : entry.Location;
            var changed = string.Equals(entry.AssetPath, assetPath, StringComparison.Ordinal) is false ||
                          string.Equals(entry.TypeName, typeName, StringComparison.Ordinal) is false ||
                          string.Equals(entry.ProviderId, bundle.ProviderId, StringComparison.Ordinal) is false ||
                          string.Equals(entry.Location, location, StringComparison.Ordinal) is false ||
                          entry.Labels.SequenceEqual(labels) is false;
            if (changed is false)
            {
                return false;
            }

            entry.AssetPath = assetPath;
            entry.TypeName = typeName;
            entry.ProviderId = bundle.ProviderId;
            entry.Location = location;
            entry.Labels.Clear();
            entry.Labels.AddRange(labels);
            return true;
        }

        private static ResourceEditorAssetEntry CreateEntry(
            ResourceEditorBundle bundle,
            string guid,
            ResourceGroupPreview preview)
        {
            var entry = new ResourceEditorAssetEntry
            {
                Guid = guid,
                AssetPath = preview.AssetPath,
                Location = ResourceProviderIds.IsResources(bundle.ProviderId)
                    ? UnityResourcesCollector.ToResourcesLocation(preview.AssetPath)
                    : preview.Location,
                TypeName = preview.TypeName,
                ProviderId = bundle.ProviderId
            };
            entry.EnsureDefaults(bundle.ProviderId);
            entry.Labels.AddRange((preview.Labels ?? Array.Empty<string>())
                .Where(label => string.IsNullOrWhiteSpace(label) is false)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(label => label, StringComparer.Ordinal));
            return entry;
        }

        private static bool HasActiveMembership(ResourceEditorSettings settings, string guid)
        {
            return settings.Packages
                .Where(package => package != null)
                .SelectMany(package => package.Bundles.Where(bundle => bundle != null))
                .SelectMany(bundle => bundle.Entries.Where(entry => entry != null))
                .Any(entry => entry.Excluded is false && string.Equals(entry.Guid, guid, StringComparison.Ordinal));
        }

        private static string ResolveCollectorId(
            ResourceEditorPackage package,
            ResourceEditorBundle bundle)
        {
            if (ResourceProviderIds.IsResources(bundle.ProviderId))
            {
                return ResourceEditorBuiltinConstants.ResourcesCollectorId;
            }

            return string.IsNullOrWhiteSpace(bundle.CollectorId) ? package.CollectorId : bundle.CollectorId;
        }

    }
}

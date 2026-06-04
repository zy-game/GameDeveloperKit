using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.Config;
using UnityEditor;
using UnityEditorInternal;

namespace GameDeveloperKit.TagEditor
{
    internal static class TagCatalogImportService
    {
        public static int RefreshAssetLabels(TagCatalogAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var group = asset.EnsureGroup(TagCatalogAsset.AssetTagsGroupKey, TagCatalogAsset.AssetTagsDisplayName, true);
            return MergeTags(group, GetAllAssetLabels());
        }

        public static int RefreshUnityTags(TagCatalogAsset asset, out string error)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            error = null;
            try
            {
                var tags = InternalEditorUtility.tags ?? Array.Empty<string>();
                var group = asset.EnsureGroup(TagCatalogAsset.UnityTagsGroupKey, TagCatalogAsset.UnityTagsDisplayName, true);
                return MergeTags(group, tags);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return 0;
            }
        }

        public static int MergeTags(TagGroupDefinition group, IEnumerable<string> tags)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            var imported = 0;
            foreach (var tag in NormalizeTags(tags))
            {
                var existing = group.Tags.FirstOrDefault(x => string.Equals(x.Key, tag, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    group.Tags.Add(new TagDefinition
                    {
                        Key = tag,
                        DisplayName = tag
                    });
                    imported++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(existing.DisplayName))
                {
                    existing.DisplayName = tag;
                }
            }

            group.Tags.Sort((left, right) => string.Compare(left?.DisplayName, right?.DisplayName, StringComparison.OrdinalIgnoreCase));
            return imported;
        }

        private static IEnumerable<string> NormalizeTags(IEnumerable<string> tags)
        {
            return (tags ?? Array.Empty<string>())
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> GetAllAssetLabels()
        {
            var getAllLabels = typeof(AssetDatabase).GetMethod("GetAllLabels", BindingFlags.Public | BindingFlags.Static);
            if (getAllLabels?.Invoke(null, null) is string[] labels)
            {
                return labels;
            }

            return Array.Empty<string>();
        }
    }

    internal static class ResourceEditorTagCatalogProvider
    {
        public static IReadOnlyList<string> GetAssetTagKeys()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TagCatalogAsset>(TagCatalogAsset.AssetPath);
            if (catalog == null)
            {
                return Array.Empty<string>();
            }

            return catalog.Groups
                .Where(IsResourceTagGroup)
                .SelectMany(group => group.Tags)
                .Where(x => x != null && string.IsNullOrWhiteSpace(x.Key) is false)
                .Select(x => x.Key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool IsResourceTagGroup(TagGroupDefinition group)
        {
            return group != null
                && string.Equals(group.Key, TagCatalogAsset.UnityTagsGroupKey, StringComparison.OrdinalIgnoreCase) is false;
        }
    }
}

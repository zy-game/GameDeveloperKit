using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.Config;
using UnityEditor;
using UnityEditorInternal;

namespace GameDeveloperKit.TagEditor
{
    /// <summary>
    /// 定义 Tag Catalog Import Service 类型。
    /// </summary>
    internal static class TagCatalogImportService
    {
        /// <summary>
        /// 刷新 Asset Labels。
        /// </summary>
        /// <param name="asset">asset 参数。</param>
        /// <returns>执行结果。</returns>
        public static int RefreshAssetLabels(TagCatalogAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var group = asset.EnsureGroup(TagCatalogAsset.AssetTagsGroupKey, TagCatalogAsset.AssetTagsDisplayName, true);
            return MergeTags(group, GetAllAssetLabels());
        }

        /// <summary>
        /// 刷新 Unity Tags。
        /// </summary>
        /// <param name="asset">asset 参数。</param>
        /// <param name="error">error 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 Merge Tags。
        /// </summary>
        /// <param name="group">group 参数。</param>
        /// <param name="tags">tags 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 Normalize Tags。
        /// </summary>
        /// <param name="tags">tags 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<string> NormalizeTags(IEnumerable<string> tags)
        {
            return (tags ?? Array.Empty<string>())
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取 All Asset Labels。
        /// </summary>
        /// <returns>执行结果。</returns>
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

    /// <summary>
    /// 定义 Resource Editor Tag Catalog Provider 类型。
    /// </summary>
    internal static class ResourceEditorTagCatalogProvider
    {
        /// <summary>
        /// 获取 Asset Tag Keys。
        /// </summary>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 Is Resource Tag Group。
        /// </summary>
        /// <param name="group">group 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        private static bool IsResourceTagGroup(TagGroupDefinition group)
        {
            return group != null
                && string.Equals(group.Key, TagCatalogAsset.UnityTagsGroupKey, StringComparison.OrdinalIgnoreCase) is false;
        }
    }
}

using System;
using System.Collections.Generic;
using GameDeveloperKit.Config;

namespace GameDeveloperKit.TagEditor
{
    internal static class TagCatalogValidator
    {
        public static List<TagCatalogValidationIssue> Validate(TagCatalogAsset asset)
        {
            var issues = new List<TagCatalogValidationIssue>();
            if (asset == null)
            {
                issues.Add(new TagCatalogValidationIssue(TagCatalogValidationSeverity.Error, "Tag catalog asset is missing."));
                return issues;
            }

            var groupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasAssetTags = false;
            foreach (var group in asset.Groups)
            {
                if (group == null)
                {
                    issues.Add(new TagCatalogValidationIssue(TagCatalogValidationSeverity.Error, "Tag group is null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(group.Key))
                {
                    issues.Add(new TagCatalogValidationIssue(TagCatalogValidationSeverity.Error, "Tag group key cannot be empty."));
                    continue;
                }

                var groupKey = group.Key.Trim();
                if (!groupKeys.Add(groupKey))
                {
                    issues.Add(new TagCatalogValidationIssue(TagCatalogValidationSeverity.Error, $"Duplicate group key: {groupKey}."));
                }

                if (string.Equals(groupKey, TagCatalogAsset.AssetTagsGroupKey, StringComparison.OrdinalIgnoreCase))
                {
                    hasAssetTags = true;
                }

                if (string.IsNullOrWhiteSpace(group.DisplayName))
                {
                    issues.Add(new TagCatalogValidationIssue(TagCatalogValidationSeverity.Warning, $"Group '{groupKey}' has no display name."));
                }

                ValidateTags(group, groupKey, issues);
            }

            if (!hasAssetTags)
            {
                issues.Add(new TagCatalogValidationIssue(TagCatalogValidationSeverity.Error, $"Required group '{TagCatalogAsset.AssetTagsGroupKey}' is missing."));
            }

            return issues;
        }

        private static void ValidateTags(TagGroupDefinition group, string groupKey, List<TagCatalogValidationIssue> issues)
        {
            var tagKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in group.Tags)
            {
                if (tag == null)
                {
                    issues.Add(new TagCatalogValidationIssue(TagCatalogValidationSeverity.Error, $"Group '{groupKey}' contains a null tag."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(tag.Key))
                {
                    issues.Add(new TagCatalogValidationIssue(TagCatalogValidationSeverity.Error, $"Group '{groupKey}' contains an empty tag key."));
                    continue;
                }

                var tagKey = tag.Key.Trim();
                if (!tagKeys.Add(tagKey))
                {
                    issues.Add(new TagCatalogValidationIssue(TagCatalogValidationSeverity.Error, $"Duplicate tag key '{tagKey}' in group '{groupKey}'."));
                }

            }
        }
    }

    internal enum TagCatalogValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    internal sealed class TagCatalogValidationIssue
    {
        public TagCatalogValidationIssue(TagCatalogValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        public TagCatalogValidationSeverity Severity { get; }

        public string Message { get; }
    }
}

using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Config
{
    public sealed class TagCatalog
    {
        /// <summary>
        /// 初始化 Tag Catalog。
        /// </summary>
        private static readonly TagCatalog s_Empty = new TagCatalog(new List<TagGroup>());
        private readonly IReadOnlyList<TagGroup> m_Groups;
        private readonly Dictionary<string, TagGroup> m_GroupLookup;
        private readonly Dictionary<string, HashSet<string>> m_TagLookup;

        /// <summary>
        /// 初始化 Tag Catalog。
        /// </summary>
        private TagCatalog(List<TagGroup> groups)
        {
            m_Groups = groups.AsReadOnly();
            m_GroupLookup = new Dictionary<string, TagGroup>(StringComparer.OrdinalIgnoreCase);
            m_TagLookup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                m_GroupLookup.Add(group.Key, group);

                var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var tag in group.Tags)
                {
                    tags.Add(tag.Key);
                }

                m_TagLookup.Add(group.Key, tags);
            }
        }
        public static TagCatalog Empty => s_Empty;
        public IReadOnlyList<TagGroup> Groups => m_Groups;

        /// <summary>
        /// 执行 From Asset。
        /// </summary>
        public static TagCatalog FromAsset(TagCatalogAsset asset, string source)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            return Build(asset.Groups, source);
        }

        /// <summary>
        /// 构建 member。
        /// </summary>
        public static TagCatalog Build(IEnumerable<TagGroupDefinition> definitions, string source)
        {
            var groups = new List<TagGroup>();
            var groupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasAssetTags = false;

            foreach (var group in definitions ?? Array.Empty<TagGroupDefinition>())
            {
                if (group == null)
                {
                    continue;
                }

                var groupKey = NormalizeRequired(group.Key, "group key", source);
                if (!groupKeys.Add(groupKey))
                {
                    throw new GameException($"Tag catalog '{source}' contains duplicate group key '{groupKey}'.");
                }

                if (string.Equals(groupKey, TagCatalogAsset.AssetTagsGroupKey, StringComparison.OrdinalIgnoreCase))
                {
                    hasAssetTags = true;
                }

                var tagKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var tags = new List<TagDefinition>();
                foreach (var tag in group.Tags)
                {
                    if (tag == null)
                    {
                        continue;
                    }

                    var tagKey = NormalizeRequired(tag.Key, $"tag key in group '{groupKey}'", source);
                    if (!tagKeys.Add(tagKey))
                    {
                        throw new GameException($"Tag catalog '{source}' contains duplicate tag key '{tagKey}' in group '{groupKey}'.");
                    }

                    tags.Add(new TagDefinition
                    {
                        Key = tagKey,
                        DisplayName = string.IsNullOrWhiteSpace(tag.DisplayName) ? tagKey : tag.DisplayName.Trim(),
                        Description = tag.Description
                    });
                }

                groups.Add(new TagGroup(
                    groupKey,
                    string.IsNullOrWhiteSpace(group.DisplayName) ? groupKey : group.DisplayName.Trim(),
                    group.Fixed,
                    tags.AsReadOnly()));
            }

            if (!hasAssetTags && groups.Count > 0)
            {
                throw new GameException($"Tag catalog '{source}' does not contain required group '{TagCatalogAsset.AssetTagsGroupKey}'.");
            }

            return new TagCatalog(groups);
        }

        /// <summary>
        /// 尝试获取 Group。
        /// </summary>
        /// <param name="groupKey">group Key 参数。</param>
        public bool TryGetGroup(string groupKey, out TagGroup group)
        {
            ValidateKey(groupKey, nameof(groupKey));
            return m_GroupLookup.TryGetValue(groupKey.Trim(), out group);
        }

        /// <summary>
        /// 获取 Tags。
        /// </summary>
        /// <param name="groupKey">group Key 参数。</param>
        public IReadOnlyList<TagDefinition> GetTags(string groupKey)
        {
            if (!TryGetGroup(groupKey, out var group))
            {
                throw new GameException($"Tag group '{groupKey}' is not loaded.");
            }

            return group.Tags;
        }

        /// <summary>
        /// 查询是否存在 Tag。
        /// </summary>
        /// <param name="groupKey">group Key 参数。</param>
        /// <param name="tagKey">tag Key 参数。</param>
        public bool HasTag(string groupKey, string tagKey)
        {
            ValidateKey(groupKey, nameof(groupKey));
            ValidateKey(tagKey, nameof(tagKey));

            return m_TagLookup.TryGetValue(groupKey.Trim(), out var tags) && tags.Contains(tagKey.Trim());
        }

        /// <summary>
        /// 执行 Normalize Required。
        /// </summary>
        private static string NormalizeRequired(string value, string label, string source)
        {
            if (value == null)
            {
                throw new GameException($"Tag catalog '{source}' contains null {label}.");
            }

            var normalized = value.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new GameException($"Tag catalog '{source}' contains empty {label}.");
            }

            return normalized;
        }

        /// <summary>
        /// 校验 Key。
        /// </summary>
        /// <param name="parameterName">parameter Name 参数。</param>
        private static void ValidateKey(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Tag key cannot be empty.", parameterName);
            }
        }
    }
}

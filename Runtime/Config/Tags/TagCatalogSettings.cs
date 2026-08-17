using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// Tag Catalog 数据模型。运行时与编辑器共用，通过 GDKSetting.json 的 tagCatalog section 持久化。
    /// </summary>
    [Serializable]
    public sealed class TagCatalogSettings
    {
        public const string AssetTagsGroupKey = "asset-tags";
        public const string AssetTagsDisplayName = "Asset Tags";
        public const string UnityTagsGroupKey = "unity-tags";
        public const string UnityTagsDisplayName = "Unity Tags";

        [JsonProperty("groups")]
        private List<TagGroupDefinition> m_Groups = new List<TagGroupDefinition>();

        [JsonIgnore]
        public List<TagGroupDefinition> Groups
        {
            get
            {
                m_Groups ??= new List<TagGroupDefinition>();
                return m_Groups;
            }
            set => m_Groups = value ?? new List<TagGroupDefinition>();
        }

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public void EnsureDefaults()
        {
            EnsureGroup(AssetTagsGroupKey, AssetTagsDisplayName, true);
        }

        /// <summary>
        /// 确保 Group。
        /// </summary>
        /// <param name="displayName">display Name 参数。</param>
        /// <param name="isFixed">is Fixed 参数。</param>
        public TagGroupDefinition EnsureGroup(string key, string displayName, bool isFixed)
        {
            foreach (var group in Groups)
            {
                if (group != null && string.Equals(group.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    group.Key = key;
                    group.DisplayName = string.IsNullOrWhiteSpace(group.DisplayName) ? displayName : group.DisplayName;
                    group.Fixed = group.Fixed || isFixed;
                    return group;
                }
            }

            var definition = new TagGroupDefinition
            {
                Key = key,
                DisplayName = displayName,
                Fixed = isFixed
            };
            Groups.Add(definition);
            return definition;
        }
    }
}

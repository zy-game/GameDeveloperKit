using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 定义 Tag Catalog Asset 类型。
    /// </summary>
    public sealed class TagCatalogAsset : ScriptableObject
    {
        /// <summary>
        /// 定义 Resource Path 常量。
        /// </summary>
        public const string ResourcePath = "GameDeveloperKit/TagCatalog";
        /// <summary>
        /// 定义 Asset Path 常量。
        /// </summary>
        public const string AssetPath = "Assets/Resources/GameDeveloperKit/TagCatalog.asset";
        /// <summary>
        /// 定义 Asset Tags Group Key 常量。
        /// </summary>
        public const string AssetTagsGroupKey = "asset-tags";
        /// <summary>
        /// 定义 Asset Tags Display Name 常量。
        /// </summary>
        public const string AssetTagsDisplayName = "Asset Tags";
        /// <summary>
        /// 定义 Unity Tags Group Key 常量。
        /// </summary>
        public const string UnityTagsGroupKey = "unity-tags";
        /// <summary>
        /// 定义 Unity Tags Display Name 常量。
        /// </summary>
        public const string UnityTagsDisplayName = "Unity Tags";

        [SerializeField] private List<TagGroupDefinition> m_Groups = new List<TagGroupDefinition>();

        public List<TagGroupDefinition> Groups
        {
            get
            {
                m_Groups ??= new List<TagGroupDefinition>();
                return m_Groups;
            }
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
        /// <param name="key">key 参数。</param>
        /// <param name="displayName">display Name 参数。</param>
        /// <param name="isFixed">is Fixed 参数。</param>
        /// <returns>执行结果。</returns>
        public TagGroupDefinition EnsureGroup(string key, string displayName, bool isFixed)
        {
            foreach (var group in Groups)
            {
                if (group != null && string.Equals(group.Key, key, System.StringComparison.OrdinalIgnoreCase))
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

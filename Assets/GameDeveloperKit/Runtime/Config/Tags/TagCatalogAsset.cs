using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    public sealed class TagCatalogAsset : ScriptableObject
    {
        public const string ResourcePath = "GameDeveloperKit/TagCatalog";
        public const string AssetPath = "Assets/Resources/GameDeveloperKit/TagCatalog.asset";
        public const string AssetTagsGroupKey = "asset-tags";
        public const string AssetTagsDisplayName = "Asset Tags";
        public const string UnityTagsGroupKey = "unity-tags";
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

        public void EnsureDefaults()
        {
            EnsureGroup(AssetTagsGroupKey, AssetTagsDisplayName, true);
        }

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

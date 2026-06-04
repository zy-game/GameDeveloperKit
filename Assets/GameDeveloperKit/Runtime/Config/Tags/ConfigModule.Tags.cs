using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    public sealed partial class ConfigModule
    {
        private TagCatalog m_Tags = TagCatalog.Empty;

        public TagCatalog Tags => m_Tags;

        public bool TryGetTagGroup(string groupKey, out TagGroup group)
        {
            return m_Tags.TryGetGroup(groupKey, out group);
        }

        public IReadOnlyList<TagDefinition> GetTags(string groupKey)
        {
            return m_Tags.GetTags(groupKey);
        }

        public bool HasTag(string groupKey, string tagKey)
        {
            return m_Tags.HasTag(groupKey, tagKey);
        }

        private void LoadTagCatalog()
        {
            var asset = Resources.Load<TagCatalogAsset>(TagCatalogAsset.ResourcePath);
            m_Tags = asset == null
                ? TagCatalog.Empty
                : TagCatalog.FromAsset(asset, TagCatalogAsset.ResourcePath);
        }
    }
}

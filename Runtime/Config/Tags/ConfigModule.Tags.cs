using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    public sealed partial class ConfigModule
    {
        private TagCatalog m_Tags = TagCatalog.Empty;
        public TagCatalog Tags => m_Tags;

        /// <summary>
        /// 尝试获取 Tag Group。
        /// </summary>
        /// <param name="groupKey">group Key 参数。</param>
        public bool TryGetTagGroup(string groupKey, out TagGroup group)
        {
            return m_Tags.TryGetGroup(groupKey, out group);
        }

        /// <summary>
        /// 获取 Tags。
        /// </summary>
        /// <param name="groupKey">group Key 参数。</param>
        public IReadOnlyList<TagDefinition> GetTags(string groupKey)
        {
            return m_Tags.GetTags(groupKey);
        }

        /// <summary>
        /// 查询是否存在 Tag。
        /// </summary>
        /// <param name="groupKey">group Key 参数。</param>
        /// <param name="tagKey">tag Key 参数。</param>
        public bool HasTag(string groupKey, string tagKey)
        {
            return m_Tags.HasTag(groupKey, tagKey);
        }

        /// <summary>
        /// 加载 Tag Catalog。
        /// </summary>
        private void LoadTagCatalog()
        {
            var asset = Resources.Load<TagCatalogAsset>(TagCatalogAsset.ResourcePath);
            m_Tags = asset == null
                ? TagCatalog.Empty
                : TagCatalog.FromAsset(asset, TagCatalogAsset.ResourcePath);
        }
    }
}

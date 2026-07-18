using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Media;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class CatalogSessionCache
    {
        private readonly Dictionary<string, CatalogPage> m_Pages = new Dictionary<string, CatalogPage>(StringComparer.Ordinal);

        public bool TryGet(string scope, MediaKind kind, string query, string cursor, int limit, out CatalogPage page)
        {
            return m_Pages.TryGetValue(BuildKey(scope, kind, query, cursor, limit), out page);
        }

        public void Set(string scope, MediaKind kind, string query, string cursor, int limit, CatalogPage page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            m_Pages[BuildKey(scope, kind, query, cursor, limit)] = page;
        }

        public void Clear()
        {
            m_Pages.Clear();
        }

        private static string BuildKey(string scope, MediaKind kind, string query, string cursor, int limit)
        {
            return $"{scope?.Trim() ?? string.Empty}|{kind}|{query?.Trim() ?? string.Empty}|{cursor?.Trim() ?? string.Empty}|{limit}";
        }
    }
}

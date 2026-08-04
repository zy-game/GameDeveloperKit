using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Media;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class CatalogSessionCache
    {
        private readonly Dictionary<string, CatalogPage> m_Pages = new Dictionary<string, CatalogPage>(StringComparer.Ordinal);
        private readonly Dictionary<string, HlsCatalogDocument> m_Documents =
            new Dictionary<string, HlsCatalogDocument>(StringComparer.Ordinal);

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

        public bool TryGetDocument(string scope, out HlsCatalogDocument document)
        {
            return m_Documents.TryGetValue(NormalizeScope(scope), out document);
        }

        public void SetDocument(string scope, HlsCatalogDocument document)
        {
            m_Documents[NormalizeScope(scope)] = document ?? throw new ArgumentNullException(nameof(document));
        }

        public void Clear(string scope)
        {
            var normalizedScope = NormalizeScope(scope);
            m_Documents.Remove(normalizedScope);
            var prefix = normalizedScope + "|";
            var keys = new List<string>(m_Pages.Keys);
            foreach (var key in keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    m_Pages.Remove(key);
                }
            }
        }

        public void Clear()
        {
            m_Pages.Clear();
            m_Documents.Clear();
        }

        private static string BuildKey(string scope, MediaKind kind, string query, string cursor, int limit)
        {
            return $"{NormalizeScope(scope)}|{kind}|{query?.Trim() ?? string.Empty}|{cursor?.Trim() ?? string.Empty}|{limit}";
        }

        private static string NormalizeScope(string scope) => scope?.Trim().TrimEnd('/') ?? string.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Text;
using GameDeveloperKit.Localization;
using GameDeveloperKit.LocalizationEditor;

namespace GameDeveloperKit.StoryEditor.Media
{
    public enum CatalogErrorKind
    {
        InvalidSettings,
        RequestFailed,
        InvalidResponse,
        DuplicateMediaId,
        UnsupportedMediaKind,
        InvalidLocation,
        UnavailableReference
    }

    public sealed class CatalogException : Exception
    {
        public CatalogException(CatalogErrorKind kind, string message, Exception innerException = null)
            : base(message, innerException)
        {
            Kind = kind;
        }

        public CatalogErrorKind Kind { get; }
    }

    public sealed class CatalogPage
    {
        public CatalogPage(IReadOnlyList<CatalogItem> items, string nextCursor)
        {
            Items = items ?? Array.Empty<CatalogItem>();
            NextCursor = nextCursor ?? string.Empty;
        }

        public IReadOnlyList<CatalogItem> Items { get; }

        public string NextCursor { get; }
    }

    public sealed class CatalogItem
    {
        public CatalogItem(
            string mediaId,
            string name,
            MediaKind kind,
            string location,
            VideoFormat format,
            string thumbnailLocation,
            int width,
            int height,
            int bitrate,
            long durationMs,
            IReadOnlyList<CatalogRendition> renditions)
        {
            MediaId = mediaId;
            Name = name;
            Kind = kind;
            Location = location;
            Format = format;
            ThumbnailLocation = thumbnailLocation;
            Width = width;
            Height = height;
            Bitrate = bitrate;
            DurationMs = durationMs;
            Renditions = renditions ?? Array.Empty<CatalogRendition>();
        }

        public string MediaId { get; }

        public string Name { get; }

        public MediaKind Kind { get; }

        public string Location { get; }

        public VideoFormat Format { get; }

        public string ThumbnailLocation { get; }

        public int Width { get; }

        public int Height { get; }

        public int Bitrate { get; }

        public long DurationMs { get; }

        public IReadOnlyList<CatalogRendition> Renditions { get; }
    }

    public readonly struct CatalogRendition
    {
        public CatalogRendition(
            string label,
            string mediaId,
            string location,
            int width,
            int height,
            int bitrate,
            long durationMs)
        {
            Label = label;
            MediaId = mediaId;
            Location = location;
            Width = width;
            Height = height;
            Bitrate = bitrate;
            DurationMs = durationMs;
        }

        public string Label { get; }

        public string MediaId { get; }

        public string Location { get; }

        public int Width { get; }

        public int Height { get; }

        public int Bitrate { get; }

        public long DurationMs { get; }
    }

    internal sealed class LocalizationTextCatalog
    {
        private readonly Dictionary<string, string> m_Entries;

        private LocalizationTextCatalog(Dictionary<string, string> entries, string error, string previewLocale)
        {
            m_Entries = entries;
            Error = error;
            PreviewLocale = previewLocale;
        }

        public string Error { get; }

        public string PreviewLocale { get; }

        public IReadOnlyDictionary<string, string> Entries => m_Entries;

        public bool TryGetText(string key, out string text)
        {
            return m_Entries.TryGetValue(key, out text);
        }

        public IReadOnlyList<KeyValuePair<string, string>> Search(string query, int limit = 100)
        {
            if (limit <= 0)
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }

            query = query?.Trim() ?? string.Empty;
            var matches = new List<KeyValuePair<string, string>>();
            foreach (var pair in m_Entries)
            {
                if (string.IsNullOrWhiteSpace(query) || MatchRank(pair, query) < 5)
                {
                    matches.Add(pair);
                }
            }

            matches.Sort((left, right) =>
            {
                var rank = MatchRank(left, query).CompareTo(MatchRank(right, query));
                return rank != 0 ? rank : string.Compare(left.Key, right.Key, StringComparison.Ordinal);
            });
            if (matches.Count > limit)
            {
                matches.RemoveRange(limit, matches.Count - limit);
            }

            return matches;
        }

        public string Resolve(TextReference reference)
        {
            if (reference.Mode == TextMode.Literal)
            {
                return reference.Value;
            }

            return TryGetText(reference.Value, out var text) ? text : reference.Value;
        }

        public string Resolve(string value)
        {
            return TextReferenceCodec.TryDeserialize(value, out var reference, out _, out _)
                ? Resolve(reference)
                : value;
        }

        public static LocalizationTextCatalog Build()
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            var snapshot = LocalizationEditorCatalog.Shared.Refresh();
            foreach (var entry in snapshot.Entries.Values)
            {
                if (entry.TryGetText(snapshot.PreviewLocale, out var text))
                {
                    entries.Add(entry.Key, text);
                }
            }

            var errors = snapshot.Diagnostics
                .Where(diagnostic => diagnostic.Severity == LocalizationCatalogDiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.Message)
                .ToArray();
            var error = errors.Length > 0
                ? $"{snapshot.PreviewLocale} 本地化 Catalog 不可用：{Environment.NewLine}" +
                  string.Join(Environment.NewLine, errors)
                : entries.Count == 0 ? "本地化 Editor Catalog 没有文本条目。" : null;
            return new LocalizationTextCatalog(entries, error, snapshot.PreviewLocale);
        }

        internal static LocalizationTextCatalog Parse(string json)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var pack = LocalizationPack.Parse("zh-CN", json, "Story Editor preview");
                CopyEntries(pack, entries);
                return new LocalizationTextCatalog(
                    entries,
                    entries.Count == 0 ? "zh-CN 多语言包没有文本条目。" : null,
                    "zh-CN");
            }
            catch (Exception exception)
            {
                return new LocalizationTextCatalog(
                    entries,
                    $"解析 zh-CN 多语言包失败。{exception.Message}",
                    "zh-CN");
            }
        }

        public static bool IsExplicitLocalizationKey(string value, out TextReference reference, out string error)
        {
            reference = default;
            error = null;
            return value?.TrimStart().StartsWith("{", StringComparison.Ordinal) == true &&
                   TextReferenceCodec.TryDeserialize(value, out reference, out _, out error) &&
                   reference.Mode == TextMode.LocalizationKey;
        }

        private static int MatchRank(KeyValuePair<string, string> pair, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return 0;
            }

            if (string.Equals(pair.Key, query, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (pair.Key.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (pair.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2;
            }

            var text = pair.Value ?? string.Empty;
            return text.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 3 :
                text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ? 4 : 5;
        }

        private static void CopyEntries(LocalizationPack pack, Dictionary<string, string> entries)
        {
            foreach (var pair in pack.Entries)
            {
                entries[pair.Key] = pair.Value;
            }
        }
    }
}

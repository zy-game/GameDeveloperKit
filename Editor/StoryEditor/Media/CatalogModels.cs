using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Media;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using GameDeveloperKit.Story.Text;

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

        private LocalizationTextCatalog(Dictionary<string, string> entries, string error)
        {
            m_Entries = entries;
            Error = error;
        }

        public string Error { get; }

        public IReadOnlyDictionary<string, string> Entries => m_Entries;

        public bool TryGetText(string key, out string text)
        {
            return m_Entries.TryGetValue(key, out text);
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
            var guids = AssetDatabase.FindAssets("t:TextAsset");
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (IsChineseLocaleName(fileName) is false || string.Equals(Path.GetExtension(assetPath), ".json", StringComparison.OrdinalIgnoreCase) is false)
                {
                    continue;
                }

                try
                {
                    AddEntries(System.IO.File.ReadAllText(Path.GetFullPath(assetPath)), entries);
                }
                catch (Exception exception)
                {
                    return new LocalizationTextCatalog(entries, $"zh-CN pack parse failed: {assetPath}. {exception.Message}");
                }
            }

            return entries.Count == 0
                ? new LocalizationTextCatalog(entries, "zh-CN localization pack is missing or contains no entries.")
                : new LocalizationTextCatalog(entries, null);
        }

        internal static LocalizationTextCatalog Parse(string json)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                AddEntries(json, entries);
                return new LocalizationTextCatalog(entries, entries.Count == 0 ? "zh-CN localization pack contains no entries." : null);
            }
            catch (Exception exception)
            {
                return new LocalizationTextCatalog(entries, $"zh-CN pack parse failed. {exception.Message}");
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

        private static bool IsChineseLocaleName(string value)
        {
            return string.Equals(value, "zh-CN", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "zh_CN", StringComparison.OrdinalIgnoreCase) ||
                   value?.EndsWith(".zh-CN", StringComparison.OrdinalIgnoreCase) == true ||
                   value?.EndsWith("_zh-CN", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static void AddEntries(string json, Dictionary<string, string> entries)
        {
            var root = JObject.Parse(json);
            var source = root["entries"] as JObject ?? root;
            foreach (var property in source.Properties())
            {
                if (property.Value.Type != JTokenType.Object && property.Value.Type != JTokenType.Array)
                {
                    entries[property.Name] = property.Value.Type == JTokenType.Null ? string.Empty : property.Value.ToString();
                }
            }
        }
    }
}

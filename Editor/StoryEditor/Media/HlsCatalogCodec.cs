using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameDeveloperKit.Story.Media;
using Newtonsoft.Json;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal static class HlsCatalogCodec
    {
        public const int SchemaVersion = 1;

        public static HlsCatalogDocument ParseDocument(
            string json,
            string cdnBaseUrl,
            bool requireSchemaVersion)
        {
            CatalogDocumentData data;
            try
            {
                data = JsonConvert.DeserializeObject<CatalogDocumentData>(json);
            }
            catch (JsonException exception)
            {
                throw new CatalogException(
                    CatalogErrorKind.InvalidResponse,
                    "Catalog response JSON is invalid.",
                    exception);
            }

            if (data?.Items == null)
            {
                throw new CatalogException(
                    CatalogErrorKind.InvalidResponse,
                    "Catalog response must contain an items array.");
            }

            if ((requireSchemaVersion || data.SchemaVersion.HasValue) &&
                data.SchemaVersion != SchemaVersion)
            {
                throw new CatalogException(
                    CatalogErrorKind.UnsupportedSchema,
                    $"Catalog schema is unsupported. schemaVersion:{data.SchemaVersion?.ToString() ?? "missing"}");
            }

            if (data.Generation < 0)
            {
                throw new CatalogException(
                    CatalogErrorKind.InvalidResponse,
                    "Catalog generation cannot be negative.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            var items = new List<CatalogItem>(data.Items.Count);
            for (var index = 0; index < data.Items.Count; index++)
            {
                items.Add(ParseItem(data.Items[index], index, cdnBaseUrl, ids, hashes));
            }

            return new HlsCatalogDocument(
                data.SchemaVersion ?? SchemaVersion,
                data.Generation,
                ParseUtc(data.UpdatedAtUtc, "updatedAtUtc", false),
                items);
        }

        public static string SerializeDocument(HlsCatalogDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var payload = new
            {
                schemaVersion = SchemaVersion,
                generation = document.Generation,
                updatedAtUtc = FormatUtc(document.UpdatedAtUtc),
                items = document.Items
                    .OrderByDescending(item => item.UpdatedAtUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(item => item.MediaId, StringComparer.Ordinal)
                    .Select(item => new
                    {
                        mediaId = item.MediaId,
                        name = item.Name,
                        sourceFileName = EmptyToNull(item.SourceFileName),
                        sourceSha256 = EmptyToNull(item.SourceSha256),
                        uploader = EmptyToNull(item.Uploader),
                        createdAtUtc = FormatUtc(item.CreatedAtUtc),
                        updatedAtUtc = FormatUtc(item.UpdatedAtUtc),
                        kind = item.Kind == MediaKind.Video ? "video" : "audio",
                        format = item.Kind == MediaKind.Video
                            ? item.Format == VideoFormat.Hls ? "hls" : "mp4"
                            : null,
                        objectPrefix = EmptyToNull(item.ObjectPrefix),
                        location = item.Location,
                        thumbnail = EmptyToNull(item.ThumbnailLocation),
                        width = item.Width,
                        height = item.Height,
                        bitrate = item.Bitrate,
                        durationMs = item.DurationMs,
                        renditions = item.Renditions.Select(rendition => new
                        {
                            label = rendition.Label,
                            mediaId = EmptyToNull(rendition.MediaId),
                            location = rendition.Location,
                            width = rendition.Width,
                            height = rendition.Height,
                            bitrate = rendition.Bitrate,
                            durationMs = rendition.DurationMs
                        }).ToArray()
                    }).ToArray()
            };
            return JsonConvert.SerializeObject(payload, Formatting.Indented);
        }

        public static CatalogPage Search(
            HlsCatalogDocument document,
            MediaKind kind,
            string query,
            string cursor,
            int limit)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            var offset = ParseCursor(cursor);
            var normalizedQuery = query?.Trim() ?? string.Empty;
            var matches = document.Items
                .Where(item => item.Kind == kind && Matches(item, normalizedQuery))
                .ToArray();
            if (offset >= matches.Length)
            {
                return new CatalogPage(Array.Empty<CatalogItem>(), string.Empty);
            }

            var count = Math.Min(limit, matches.Length - offset);
            var page = new CatalogItem[count];
            Array.Copy(matches, offset, page, 0, count);
            var nextOffset = offset + count;
            return new CatalogPage(
                page,
                nextOffset < matches.Length
                    ? nextOffset.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);
        }

        private static CatalogItem ParseItem(
            CatalogItemData source,
            int index,
            string cdnBaseUrl,
            ISet<string> ids,
            ISet<string> hashes)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.MediaId))
            {
                throw new CatalogException(
                    CatalogErrorKind.InvalidResponse,
                    $"Catalog item at index {index} requires mediaId.");
            }

            var mediaId = source.MediaId.Trim();
            if (ids.Add(mediaId) is false)
            {
                throw new CatalogException(
                    CatalogErrorKind.DuplicateMediaId,
                    $"Catalog response contains duplicate mediaId:{mediaId}");
            }

            var sourceSha256 = source.SourceSha256?.Trim() ?? string.Empty;
            if (sourceSha256.Length > 0)
            {
                if (IsLowerHexSha256(sourceSha256) is false)
                {
                    throw new CatalogException(
                        CatalogErrorKind.InvalidResponse,
                        $"Catalog item has invalid sourceSha256. mediaId:{mediaId}");
                }

                if (hashes.Add(sourceSha256) is false)
                {
                    throw new CatalogException(
                        CatalogErrorKind.InvalidResponse,
                        $"Catalog response contains duplicate sourceSha256. mediaId:{mediaId}");
                }
            }

            if (TryParseKind(source.Kind, out var kind) is false)
            {
                throw new CatalogException(
                    CatalogErrorKind.UnsupportedMediaKind,
                    $"Catalog item has unsupported kind. mediaId:{mediaId}");
            }

            var format = default(VideoFormat);
            if (kind == MediaKind.Video && TryParseFormat(source.Format, out format) is false)
            {
                throw new CatalogException(
                    CatalogErrorKind.InvalidResponse,
                    $"Catalog item has invalid video format. mediaId:{mediaId}");
            }

            ValidateMetadata(source.Width, source.Height, source.Bitrate, source.DurationMs, mediaId);
            var renditions = ParseRenditions(source.Renditions, mediaId);
            var item = new CatalogItem(
                mediaId,
                string.IsNullOrWhiteSpace(source.Name) ? mediaId : source.Name.Trim(),
                kind,
                source.Location,
                format,
                source.ThumbnailLocation,
                source.Width,
                source.Height,
                source.Bitrate,
                source.DurationMs,
                renditions,
                source.SourceFileName?.Trim(),
                sourceSha256,
                source.Uploader?.Trim(),
                ParseUtc(source.CreatedAtUtc, "createdAtUtc", false),
                ParseUtc(source.UpdatedAtUtc, "updatedAtUtc", false),
                source.ObjectPrefix?.Trim());
            ValidateLocations(item, cdnBaseUrl);
            return item;
        }

        private static IReadOnlyList<CatalogRendition> ParseRenditions(
            IReadOnlyList<CatalogRenditionData> sources,
            string mediaId)
        {
            var renditions = new List<CatalogRendition>();
            for (var index = 0; index < (sources?.Count ?? 0); index++)
            {
                var source = sources[index];
                if (source == null)
                {
                    throw new CatalogException(
                        CatalogErrorKind.InvalidResponse,
                        $"Catalog rendition is null. mediaId:{mediaId}");
                }

                ValidateMetadata(
                    source.Width,
                    source.Height,
                    source.Bitrate,
                    source.DurationMs,
                    mediaId);
                renditions.Add(new CatalogRendition(
                    source.Label,
                    source.MediaId,
                    source.Location,
                    source.Width,
                    source.Height,
                    source.Bitrate,
                    source.DurationMs));
            }

            return renditions;
        }

        private static void ValidateLocations(CatalogItem item, string cdnBaseUrl)
        {
            if (item.Kind == MediaKind.Video)
            {
                ValidateRelativeVideoLocation(item.Location);
                for (var i = 0; i < (item.Renditions?.Count ?? 0); i++)
                {
                    ValidateRelativeVideoLocation(item.Renditions[i].Location);
                }
            }
            else
            {
                CatalogReferenceFactory.CreateAudioReference(item, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(item.ThumbnailLocation) is false)
            {
                CatalogReferenceFactory.ExpandHttpsLocation(cdnBaseUrl, item.ThumbnailLocation);
            }
        }

        private static void ValidateRelativeVideoLocation(string location)
        {
            try
            {
                _ = new GameDeveloperKit.Media.MediaPath(location);
            }
            catch (ArgumentException exception)
            {
                throw new CatalogException(CatalogErrorKind.InvalidLocation, exception.Message, exception);
            }
        }

        private static int ParseCursor(string cursor)
        {
            if (string.IsNullOrWhiteSpace(cursor))
            {
                return 0;
            }

            if (int.TryParse(cursor.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var offset) is false ||
                offset < 0)
            {
                throw new CatalogException(
                    CatalogErrorKind.InvalidCursor,
                    $"Catalog cursor is invalid. cursor:{cursor}");
            }

            return offset;
        }

        private static bool Matches(CatalogItem item, string query)
        {
            return query.Length == 0 ||
                   Contains(item.Name, query) ||
                   Contains(item.Uploader, query) ||
                   Contains(item.MediaId, query);
        }

        private static bool Contains(string value, string query)
        {
            return (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string FormatUtc(DateTimeOffset? value)
        {
            return value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        private static bool IsLowerHexSha256(string value)
        {
            return value.Length == 64 && value.All(character =>
                character >= '0' && character <= '9' || character >= 'a' && character <= 'f');
        }

        private static DateTimeOffset? ParseUtc(string value, string field, bool required)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required)
                {
                    throw new CatalogException(
                        CatalogErrorKind.InvalidResponse,
                        $"Catalog {field} is required.");
                }

                return null;
            }

            if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed) is false)
            {
                throw new CatalogException(
                    CatalogErrorKind.InvalidResponse,
                    $"Catalog {field} must be an ISO-8601 UTC timestamp.");
            }

            return parsed;
        }

        private static void ValidateMetadata(
            int width,
            int height,
            int bitrate,
            long durationMs,
            string mediaId)
        {
            if (width < 0 || height < 0 || bitrate < 0 || durationMs < 0)
            {
                throw new CatalogException(
                    CatalogErrorKind.InvalidResponse,
                    $"Catalog item contains negative media metadata. mediaId:{mediaId}");
            }
        }

        private static bool TryParseKind(string value, out MediaKind kind)
        {
            if (string.Equals(value, "video", StringComparison.Ordinal))
            {
                kind = MediaKind.Video;
                return true;
            }

            if (string.Equals(value, "audio", StringComparison.Ordinal))
            {
                kind = MediaKind.Audio;
                return true;
            }

            kind = default;
            return false;
        }

        private static bool TryParseFormat(string value, out VideoFormat format)
        {
            if (string.Equals(value, "hls", StringComparison.Ordinal))
            {
                format = VideoFormat.Hls;
                return true;
            }

            if (string.Equals(value, "mp4", StringComparison.Ordinal))
            {
                format = VideoFormat.Mp4;
                return true;
            }

            format = default;
            return false;
        }

        [Serializable]
        private sealed class CatalogDocumentData
        {
            [JsonProperty("schemaVersion")]
            public int? SchemaVersion { get; set; }

            [JsonProperty("generation")]
            public long Generation { get; set; }

            [JsonProperty("updatedAtUtc")]
            public string UpdatedAtUtc { get; set; }

            [JsonProperty("items")]
            public List<CatalogItemData> Items { get; set; }
        }

        [Serializable]
        private sealed class CatalogItemData
        {
            [JsonProperty("mediaId")]
            public string MediaId { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("sourceFileName")]
            public string SourceFileName { get; set; }

            [JsonProperty("sourceSha256")]
            public string SourceSha256 { get; set; }

            [JsonProperty("uploader")]
            public string Uploader { get; set; }

            [JsonProperty("createdAtUtc")]
            public string CreatedAtUtc { get; set; }

            [JsonProperty("updatedAtUtc")]
            public string UpdatedAtUtc { get; set; }

            [JsonProperty("kind")]
            public string Kind { get; set; }

            [JsonProperty("format")]
            public string Format { get; set; }

            [JsonProperty("objectPrefix")]
            public string ObjectPrefix { get; set; }

            [JsonProperty("location")]
            public string Location { get; set; }

            [JsonProperty("thumbnail")]
            public string ThumbnailLocation { get; set; }

            [JsonProperty("width")]
            public int Width { get; set; }

            [JsonProperty("height")]
            public int Height { get; set; }

            [JsonProperty("bitrate")]
            public int Bitrate { get; set; }

            [JsonProperty("durationMs")]
            public long DurationMs { get; set; }

            [JsonProperty("renditions")]
            public List<CatalogRenditionData> Renditions { get; set; }
        }

        [Serializable]
        private sealed class CatalogRenditionData
        {
            [JsonProperty("label")]
            public string Label { get; set; }

            [JsonProperty("mediaId")]
            public string MediaId { get; set; }

            [JsonProperty("location")]
            public string Location { get; set; }

            [JsonProperty("width")]
            public int Width { get; set; }

            [JsonProperty("height")]
            public int Height { get; set; }

            [JsonProperty("bitrate")]
            public int Bitrate { get; set; }

            [JsonProperty("durationMs")]
            public long DurationMs { get; set; }
        }
    }
}

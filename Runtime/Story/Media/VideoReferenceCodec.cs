using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;

namespace GameDeveloperKit.Story.Media
{
    public static class VideoReferenceCodec
    {
        private static readonly JsonSerializerSettings s_Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public static string Serialize(VideoReference reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            return JsonConvert.SerializeObject(ToData(reference), s_Settings);
        }

        public static bool TryDeserialize(string json, out VideoReference reference, out string error)
        {
            reference = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Video reference JSON cannot be empty.";
                return false;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<VideoReferenceData>(json, s_Settings);
                if (data == null)
                {
                    error = "Video reference JSON is empty.";
                    return false;
                }

                if (data.Version != VideoReference.CurrentVersion)
                {
                    error = $"Video reference version is unsupported. version:{data.Version}";
                    return false;
                }

                if (TryParseMediaKind(data.Primary?.Kind, out var kind) is false ||
                    TryParseMediaSource(data.Primary?.Source, out var source) is false ||
                    TryParseVideoFormat(data.Format, out var format) is false)
                {
                    error = "Video reference contains an invalid kind, source, or format.";
                    return false;
                }

                var primary = new MediaReference(kind, source, data.Primary.MediaId, data.Primary.Location);
                var renditions = new List<VideoRendition>();
                for (var i = 0; i < (data.Renditions?.Count ?? 0); i++)
                {
                    var item = data.Renditions[i];
                    if (item == null)
                    {
                        error = $"Video reference rendition at index {i} is null.";
                        return false;
                    }

                    renditions.Add(new VideoRendition(
                        item.Label,
                        item.MediaId,
                        item.Location,
                        item.Width,
                        item.Height,
                        item.Bitrate,
                        item.DurationMs));
                }

                reference = new VideoReference(primary, format, renditions);
                return true;
            }
            catch (Exception exception) when (
                exception is JsonException ||
                exception is ArgumentException ||
                exception is ArgumentOutOfRangeException)
            {
                error = exception.Message;
                return false;
            }
        }

        public static string SerializeRenditions(IReadOnlyList<VideoRendition> renditions)
        {
            var data = new VideoRenditionCollectionData
            {
                Version = VideoReference.CurrentVersion,
                Items = new List<VideoRenditionData>()
            };
            for (var i = 0; i < (renditions?.Count ?? 0); i++)
            {
                var item = renditions[i];
                data.Items.Add(ToData(item));
            }

            return JsonConvert.SerializeObject(data, s_Settings);
        }

        public static bool TryDeserializeRenditions(
            string json,
            out IReadOnlyList<VideoRendition> renditions,
            out string error)
        {
            renditions = null;
            error = null;
            try
            {
                var data = JsonConvert.DeserializeObject<VideoRenditionCollectionData>(json, s_Settings);
                if (data == null || data.Version != VideoReference.CurrentVersion || data.Items == null)
                {
                    error = "Video rendition metadata is invalid or unsupported.";
                    return false;
                }

                var result = new List<VideoRendition>(data.Items.Count);
                for (var i = 0; i < data.Items.Count; i++)
                {
                    var item = data.Items[i];
                    if (item == null)
                    {
                        error = $"Video rendition metadata item at index {i} is null.";
                        return false;
                    }

                    result.Add(ToRendition(item));
                }

                renditions = result;
                return true;
            }
            catch (Exception exception) when (
                exception is JsonException ||
                exception is ArgumentException ||
                exception is ArgumentOutOfRangeException)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryDeserializeCommand(
            ArgumentBag arguments,
            out VideoReference reference,
            out bool legacy,
            out string error)
        {
            reference = null;
            legacy = false;
            error = null;
            if (arguments == null)
            {
                error = "Video command arguments are missing.";
                return false;
            }

            var sourceText = arguments.GetString(MediaCommandNames.MediaSourceArgument);
            var location = arguments.GetString(MediaCommandNames.ClipArgument);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                var legacySource = arguments.GetString(MediaCommandNames.VideoSourceArgument);
                if (string.Equals(legacySource, MediaCommandNames.VideoSourceStreamingAssets, StringComparison.Ordinal) is false)
                {
                    error = $"Video media source is missing or unsupported. source:{legacySource ?? "missing"}";
                    return false;
                }

                if (TryInferFormat(location, out var legacyFormat) is false)
                {
                    error = "Legacy StreamingAssets video must end with .m3u8 or .mp4.";
                    return false;
                }

                if (LocationRules.TryNormalizeLegacyStreamingAssets(location, out var normalizedLocation, out error) is false)
                {
                    return false;
                }

                try
                {
                    reference = new VideoReference(
                        new MediaReference(MediaKind.Video, MediaSource.StreamingAssets, string.Empty, normalizedLocation),
                        legacyFormat);
                    legacy = true;
                    return true;
                }
                catch (ArgumentException exception)
                {
                    error = exception.Message;
                    return false;
                }
            }

            if (TryParseMediaSource(sourceText, out var source) is false || source == MediaSource.Resource)
            {
                error = $"Video media source is unsupported. source:{sourceText}";
                return false;
            }

            if (TryParseVideoFormat(arguments.GetString(MediaCommandNames.VideoFormatArgument), out var format) is false)
            {
                error = "Video format is missing or invalid.";
                return false;
            }

            if (TryDeserializeRenditions(
                    arguments.GetString(MediaCommandNames.VideoRenditionsArgument),
                    out var renditions,
                    out _) is false)
            {
                renditions = Array.Empty<VideoRendition>();
            }

            try
            {
                var primary = new MediaReference(
                    MediaKind.Video,
                    source,
                    arguments.GetString(MediaCommandNames.MediaIdArgument),
                    location);
                try
                {
                    reference = new VideoReference(primary, format, renditions);
                }
                catch (ArgumentException)
                {
                    reference = new VideoReference(primary, format);
                }

                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static VideoReferenceData ToData(VideoReference reference)
        {
            var data = new VideoReferenceData
            {
                Version = VideoReference.CurrentVersion,
                Primary = new MediaReferenceData
                {
                    Kind = ToText(reference.Primary.Kind),
                    Source = ToText(reference.Primary.Source),
                    MediaId = reference.Primary.MediaId,
                    Location = reference.Primary.Location
                },
                Format = ToText(reference.Format),
                Renditions = new List<VideoRenditionData>(reference.Renditions.Count)
            };

            for (var i = 0; i < reference.Renditions.Count; i++)
            {
                var item = reference.Renditions[i];
                data.Renditions.Add(new VideoRenditionData
                {
                    Label = item.Label,
                    MediaId = item.MediaId,
                    Location = item.Location,
                    Width = item.Width,
                    Height = item.Height,
                    Bitrate = item.Bitrate,
                    DurationMs = item.DurationMs
                });
            }

            return data;
        }

        private static VideoRenditionData ToData(VideoRendition item)
        {
            return new VideoRenditionData
            {
                Label = item.Label,
                MediaId = item.MediaId,
                Location = item.Location,
                Width = item.Width,
                Height = item.Height,
                Bitrate = item.Bitrate,
                DurationMs = item.DurationMs
            };
        }

        private static VideoRendition ToRendition(VideoRenditionData item)
        {
            return new VideoRendition(
                item.Label,
                item.MediaId,
                item.Location,
                item.Width,
                item.Height,
                item.Bitrate,
                item.DurationMs);
        }

        private static bool TryInferFormat(string location, out VideoFormat format)
        {
            var path = location?.Trim();
            if (path?.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) == true)
            {
                format = VideoFormat.Hls;
                return true;
            }

            if (path?.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) == true)
            {
                format = VideoFormat.Mp4;
                return true;
            }

            format = default;
            return false;
        }

        private static string ToText(MediaKind kind)
        {
            return kind == MediaKind.Video ? "video" : "audio";
        }

        private static string ToText(MediaSource source)
        {
            switch (source)
            {
                case MediaSource.Cdn:
                    return "cdn";
                case MediaSource.StreamingAssets:
                    return "streaming_assets";
                default:
                    return "resource";
            }
        }

        private static string ToText(VideoFormat format)
        {
            return format == VideoFormat.Hls ? "hls" : "mp4";
        }

        private static bool TryParseMediaKind(string value, out MediaKind kind)
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

        private static bool TryParseMediaSource(string value, out MediaSource source)
        {
            switch (value)
            {
                case "cdn":
                    source = MediaSource.Cdn;
                    return true;
                case "streaming_assets":
                    source = MediaSource.StreamingAssets;
                    return true;
                case "resource":
                    source = MediaSource.Resource;
                    return true;
                default:
                    source = default;
                    return false;
            }
        }

        private static bool TryParseVideoFormat(string value, out VideoFormat format)
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
        private sealed class VideoReferenceData
        {
            [JsonProperty("version", Order = 0)]
            public int Version { get; set; }

            [JsonProperty("primary", Order = 1)]
            public MediaReferenceData Primary { get; set; }

            [JsonProperty("format", Order = 2)]
            public string Format { get; set; }

            [JsonProperty("renditions", Order = 3)]
            public List<VideoRenditionData> Renditions { get; set; }
        }

        [Serializable]
        private sealed class MediaReferenceData
        {
            [JsonProperty("kind", Order = 0)]
            public string Kind { get; set; }

            [JsonProperty("source", Order = 1)]
            public string Source { get; set; }

            [JsonProperty("mediaId", Order = 2)]
            public string MediaId { get; set; }

            [JsonProperty("location", Order = 3)]
            public string Location { get; set; }
        }

        [Serializable]
        private sealed class VideoRenditionData
        {
            [JsonProperty("label", Order = 0)]
            public string Label { get; set; }

            [JsonProperty("mediaId", Order = 1)]
            public string MediaId { get; set; }

            [JsonProperty("location", Order = 2)]
            public string Location { get; set; }

            [JsonProperty("width", Order = 3)]
            public int Width { get; set; }

            [JsonProperty("height", Order = 4)]
            public int Height { get; set; }

            [JsonProperty("bitrate", Order = 5)]
            public int Bitrate { get; set; }

            [JsonProperty("durationMs", Order = 6)]
            public long DurationMs { get; set; }
        }

        [Serializable]
        private sealed class VideoRenditionCollectionData
        {
            [JsonProperty("version", Order = 0)]
            public int Version { get; set; }

            [JsonProperty("items", Order = 1)]
            public List<VideoRenditionData> Items { get; set; }
        }
    }
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;

namespace GameDeveloperKit.Story.Media
{
    public enum MediaKind
    {
        Video = 0,
        Audio = 1
    }

    public enum MediaSource
    {
        Cdn = 0,
        StreamingAssets = 1,
        Resource = 2
    }

    public enum VideoFormat
    {
        Hls = 0,
        Mp4 = 1
    }

    public readonly struct MediaReference
    {
        public MediaReference(MediaKind kind, MediaSource source, string mediaId, string location)
        {
            LocationRules.Validate(kind, source, mediaId, location);
            Kind = kind;
            Source = source;
            MediaId = NormalizeOptional(mediaId);
            Location = LocationRules.Normalize(source, location);
        }

        public MediaKind Kind { get; }

        public MediaSource Source { get; }

        public string MediaId { get; }

        public string Location { get; }

        private static string NormalizeOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public readonly struct VideoRendition
    {
        public VideoRendition(
            string label,
            string mediaId,
            string location,
            int width,
            int height,
            int bitrate,
            long durationMs)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Video rendition location cannot be empty.", nameof(location));
            }

            if (width < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (bitrate < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bitrate));
            }

            if (durationMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationMs));
            }

            Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
            MediaId = string.IsNullOrWhiteSpace(mediaId) ? string.Empty : mediaId.Trim();
            Location = location.Trim();
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

    public sealed class VideoReference
    {
        public const int CurrentVersion = 1;

        public VideoReference(
            MediaReference primary,
            VideoFormat format,
            IReadOnlyList<VideoRendition> renditions = null)
        {
            if (primary.Kind != MediaKind.Video)
            {
                throw new ArgumentException("Video reference primary media kind must be Video.", nameof(primary));
            }

            if (primary.Source == MediaSource.Resource)
            {
                throw new ArgumentException("Video reference does not support Resource source.", nameof(primary));
            }

            LocationRules.ValidateVideoFormat(primary.Source, primary.Location, format, nameof(primary));

            var copy = renditions == null || renditions.Count == 0
                ? Array.Empty<VideoRendition>()
                : new VideoRendition[renditions.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                var rendition = renditions[i];
                LocationRules.ValidateVideoRendition(primary.Source, rendition, format, i);
                copy[i] = new VideoRendition(
                    rendition.Label,
                    rendition.MediaId,
                    LocationRules.Normalize(primary.Source, rendition.Location),
                    rendition.Width,
                    rendition.Height,
                    rendition.Bitrate,
                    rendition.DurationMs);
            }

            ValidateDistinctPositiveHeights(copy);

            if (format == VideoFormat.Mp4 && copy.Length > 0)
            {
                ValidateMp4Renditions(primary, copy);
            }

            Primary = primary;
            Format = format;
            Renditions = copy;
        }

        public MediaReference Primary { get; }

        public VideoFormat Format { get; }

        public IReadOnlyList<VideoRendition> Renditions { get; }

        private static void ValidateDistinctPositiveHeights(IReadOnlyList<VideoRendition> renditions)
        {
            var heights = new HashSet<int>();
            for (var i = 0; i < renditions.Count; i++)
            {
                var height = renditions[i].Height;
                if (height > 0 && heights.Add(height) is false)
                {
                    throw new ArgumentException($"Video rendition height is duplicated. height:{height}", nameof(renditions));
                }
            }
        }

        private static void ValidateMp4Renditions(MediaReference primary, IReadOnlyList<VideoRendition> renditions)
        {
            var primaryRendition = renditions[0];
            if (string.Equals(primary.Location, primaryRendition.Location, StringComparison.Ordinal) is false ||
                string.Equals(primary.MediaId, primaryRendition.MediaId, StringComparison.Ordinal) is false)
            {
                throw new ArgumentException("MP4 rendition list must start with the primary clip metadata.", nameof(renditions));
            }

            if (primaryRendition.Width <= 0 || primaryRendition.Height <= 0 || primaryRendition.DurationMs <= 0)
            {
                throw new ArgumentException("MP4 primary rendition requires positive width, height, and duration.", nameof(renditions));
            }

            for (var i = 0; i < renditions.Count; i++)
            {
                var rendition = renditions[i];
                if (rendition.Width <= 0 || rendition.Height <= 0 || rendition.DurationMs <= 0)
                {
                    throw new ArgumentException($"MP4 rendition at index {i} requires positive width, height, and duration.", nameof(renditions));
                }

                var primaryAspect = (double)primaryRendition.Width / primaryRendition.Height;
                var renditionAspect = (double)rendition.Width / rendition.Height;
                if (Math.Abs(primaryAspect - renditionAspect) > 0.01d)
                {
                    throw new ArgumentException($"MP4 rendition aspect ratio differs from primary. index:{i}", nameof(renditions));
                }

                if (Math.Abs(rendition.DurationMs - primaryRendition.DurationMs) > 500L)
                {
                    throw new ArgumentException($"MP4 rendition duration differs from primary by more than 500 ms. index:{i}", nameof(renditions));
                }
            }
        }
    }

    internal static class LocationRules
    {
        private const string AssetsPrefix = "Assets/";
        private const string AssetsStreamingAssetsPrefix = "Assets/StreamingAssets/";
        private const string StreamingAssetsPrefix = "StreamingAssets/";

        public static void Validate(MediaKind kind, MediaSource source, string mediaId, string location)
        {
            if (Enum.IsDefined(typeof(MediaKind), kind) is false)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (Enum.IsDefined(typeof(MediaSource), source) is false)
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Media location cannot be empty.", nameof(location));
            }

            switch (source)
            {
                case MediaSource.Cdn:
                    if (string.IsNullOrWhiteSpace(mediaId))
                    {
                        throw new ArgumentException("CDN media reference requires a stable media ID.", nameof(mediaId));
                    }

                    if (TryGetHttpsUri(location, out _) is false)
                    {
                        throw new ArgumentException("CDN media location must be an absolute HTTPS URL.", nameof(location));
                    }

                    break;
                case MediaSource.StreamingAssets:
                    if (TryNormalizeStreamingAssets(location, true, out _, out var error) is false)
                    {
                        throw new ArgumentException(error, nameof(location));
                    }

                    break;
                case MediaSource.Resource:
                    if (kind == MediaKind.Video)
                    {
                        throw new ArgumentException("Video media does not support Resource source.", nameof(source));
                    }

                    if (IsAbsoluteOrScheme(location))
                    {
                        throw new ArgumentException("Resource media location must be a resource location, not a path or URL.", nameof(location));
                    }

                    break;
            }
        }

        public static string Normalize(MediaSource source, string location)
        {
            var trimmed = location.Trim();
            if (source != MediaSource.StreamingAssets)
            {
                return trimmed;
            }

            TryNormalizeStreamingAssets(trimmed, true, out var normalized, out _);
            return normalized;
        }

        public static bool TryNormalizeLegacyStreamingAssets(string location, out string normalized, out string error)
        {
            return TryNormalizeStreamingAssets(location, false, out normalized, out error);
        }

        public static bool IsAbsoluteHttps(string location)
        {
            return TryGetHttpsUri(location, out _);
        }

        public static void ValidateVideoFormat(MediaSource source, string location, VideoFormat format, string parameterName)
        {
            if (Enum.IsDefined(typeof(VideoFormat), format) is false)
            {
                throw new ArgumentOutOfRangeException(nameof(format));
            }

            var path = source == MediaSource.Cdn && TryGetHttpsUri(location, out var uri)
                ? uri.AbsolutePath
                : location;
            var expectedExtension = format == VideoFormat.Hls ? ".m3u8" : ".mp4";
            if (path.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new ArgumentException($"Video location must end with {expectedExtension} for format {format}.", parameterName);
            }
        }

        public static void ValidateVideoRendition(
            MediaSource source,
            VideoRendition rendition,
            VideoFormat format,
            int index)
        {
            if (source == MediaSource.Cdn)
            {
                if (string.IsNullOrWhiteSpace(rendition.MediaId))
                {
                    throw new ArgumentException($"CDN video rendition at index {index} requires a media ID.", nameof(rendition));
                }

                if (TryGetHttpsUri(rendition.Location, out _) is false)
                {
                    throw new ArgumentException($"CDN video rendition at index {index} must use an absolute HTTPS URL.", nameof(rendition));
                }
            }
            else if (TryNormalizeStreamingAssets(rendition.Location, true, out _, out var error) is false)
            {
                throw new ArgumentException($"StreamingAssets video rendition at index {index} is invalid. {error}", nameof(rendition));
            }

            ValidateVideoFormat(source, rendition.Location, format, nameof(rendition));
        }

        private static bool TryNormalizeStreamingAssets(
            string location,
            bool requireNormalizedInput,
            out string normalized,
            out string error)
        {
            normalized = null;
            error = null;
            if (string.IsNullOrWhiteSpace(location))
            {
                error = "StreamingAssets media location cannot be empty.";
                return false;
            }

            var value = location.Trim().Replace('\\', '/');
            if (IsAbsoluteOrScheme(value))
            {
                error = "StreamingAssets media location must be relative.";
                return false;
            }

            if (requireNormalizedInput)
            {
                if (value.StartsWith(AssetsPrefix, StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith(StreamingAssetsPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    error = "StreamingAssets media location must not contain an Assets/StreamingAssets prefix.";
                    return false;
                }
            }
            else if (value.StartsWith(AssetsStreamingAssetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(AssetsStreamingAssetsPrefix.Length);
            }
            else if (value.StartsWith(StreamingAssetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(StreamingAssetsPrefix.Length);
            }
            else if (value.StartsWith(AssetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "Media under Assets must be located inside StreamingAssets.";
                return false;
            }

            var parts = value.Split('/');
            for (var i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]) ||
                    string.Equals(parts[i], ".", StringComparison.Ordinal) ||
                    string.Equals(parts[i], "..", StringComparison.Ordinal))
                {
                    error = "StreamingAssets media location contains an empty or unsafe path segment.";
                    return false;
                }
            }

            normalized = string.Join("/", parts);
            return true;
        }

        private static bool TryGetHttpsUri(string value, out Uri uri)
        {
            uri = null;
            if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed) is false ||
                string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false ||
                string.IsNullOrWhiteSpace(parsed.Host) ||
                string.IsNullOrWhiteSpace(parsed.UserInfo) is false)
            {
                return false;
            }

            uri = parsed;
            return true;
        }

        private static bool IsAbsoluteOrScheme(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.StartsWith("/", StringComparison.Ordinal) ||
                value.StartsWith("\\", StringComparison.Ordinal) ||
                value.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return value.Length >= 3 &&
                   char.IsLetter(value[0]) &&
                   value[1] == ':' &&
                   value[2] == '/';
        }
    }

    public static class AudioReferenceCodec
    {
        private const int CurrentVersion = 1;

        public static string Serialize(MediaReference reference)
        {
            if (reference.Kind != MediaKind.Audio)
            {
                throw new ArgumentException("Audio reference kind must be Audio.", nameof(reference));
            }

            return JsonConvert.SerializeObject(new AudioReferenceData
            {
                Version = CurrentVersion,
                Source = ToText(reference.Source),
                MediaId = reference.MediaId,
                Location = reference.Location
            });
        }

        public static bool TryDeserialize(string json, out MediaReference reference, out string error)
        {
            reference = default;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Audio reference JSON cannot be empty.";
                return false;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<AudioReferenceData>(json);
                if (data == null || data.Version != CurrentVersion || TryParseSource(data.Source, out var source) is false)
                {
                    error = "Audio reference is invalid or unsupported.";
                    return false;
                }

                reference = new MediaReference(MediaKind.Audio, source, data.MediaId, data.Location);
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryDeserializeCommand(ArgumentBag arguments, out MediaReference reference, out bool legacy, out string error)
        {
            reference = default;
            legacy = false;
            error = null;
            if (arguments == null)
            {
                error = "Audio command arguments are missing.";
                return false;
            }

            var location = arguments.GetString(MediaCommandNames.ClipArgument);
            var sourceText = arguments.GetString(MediaCommandNames.MediaSourceArgument);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                try
                {
                    reference = new MediaReference(MediaKind.Audio, MediaSource.Resource, string.Empty, location);
                    legacy = true;
                    return true;
                }
                catch (ArgumentException exception)
                {
                    error = exception.Message;
                    return false;
                }
            }

            if (TryParseSource(sourceText, out var source) is false)
            {
                error = $"Audio media source is unsupported. source:{sourceText}";
                return false;
            }

            try
            {
                reference = new MediaReference(
                    MediaKind.Audio,
                    source,
                    arguments.GetString(MediaCommandNames.MediaIdArgument),
                    location);
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static string ToText(MediaSource source)
        {
            switch (source)
            {
                case MediaSource.Cdn: return MediaCommandNames.MediaSourceCdn;
                case MediaSource.StreamingAssets: return MediaCommandNames.MediaSourceStreamingAssets;
                default: return MediaCommandNames.MediaSourceResource;
            }
        }

        private static bool TryParseSource(string value, out MediaSource source)
        {
            switch (value)
            {
                case MediaCommandNames.MediaSourceCdn: source = MediaSource.Cdn; return true;
                case MediaCommandNames.MediaSourceStreamingAssets: source = MediaSource.StreamingAssets; return true;
                case MediaCommandNames.MediaSourceResource: source = MediaSource.Resource; return true;
                default: source = default; return false;
            }
        }

        [Serializable]
        private sealed class AudioReferenceData
        {
            [JsonProperty("version", Order = 0)] public int Version { get; set; }
            [JsonProperty("source", Order = 1)] public string Source { get; set; }
            [JsonProperty("mediaId", Order = 2)] public string MediaId { get; set; }
            [JsonProperty("location", Order = 3)] public string Location { get; set; }
        }
    }
}

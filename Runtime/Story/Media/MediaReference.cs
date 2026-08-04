using System;
using System.Collections.Generic;
using GameDeveloperKit.Media;
using Newtonsoft.Json;

namespace GameDeveloperKit.Story.Media
{
    public enum MediaKind
    {
        Video = 0,
        Audio = 1
    }

    public enum VideoFormat
    {
        Hls = 0,
        Mp4 = 1
    }

    public sealed class AudioReference
    {
        public const int CurrentVersion = 2;

        public AudioReference(MediaPath path)
        {
            if (string.IsNullOrWhiteSpace(path.Value))
            {
                throw new ArgumentException("Audio path is not initialized.", nameof(path));
            }

            Path = path;
        }

        public MediaPath Path { get; }
    }

    public readonly struct VideoRendition
    {
        public VideoRendition(
            string label,
            MediaPath path,
            int width,
            int height,
            int bitrate,
            long durationMs)
        {
            if (string.IsNullOrWhiteSpace(path.Value))
            {
                throw new ArgumentException("Video rendition path is not initialized.", nameof(path));
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
            Path = path;
            Width = width;
            Height = height;
            Bitrate = bitrate;
            DurationMs = durationMs;
        }

        public string Label { get; }

        public MediaPath Path { get; }

        public int Width { get; }

        public int Height { get; }

        public int Bitrate { get; }

        public long DurationMs { get; }
    }

    public sealed class VideoReference
    {
        public const int CurrentVersion = 2;

        public VideoReference(
            MediaPath primary,
            VideoFormat format,
            IReadOnlyList<VideoRendition> renditions = null)
        {
            ValidateFormat(primary, format, nameof(primary));

            var copy = renditions == null || renditions.Count == 0
                ? Array.Empty<VideoRendition>()
                : new VideoRendition[renditions.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                var rendition = renditions[i];
                ValidateFormat(rendition.Path, format, $"{nameof(renditions)}[{i}]");
                copy[i] = new VideoRendition(
                    rendition.Label,
                    rendition.Path,
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
            Renditions = copy.Length == 0
                ? Array.Empty<VideoRendition>()
                : new List<VideoRendition>(copy).AsReadOnly();
        }

        public MediaPath Primary { get; }

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

        private static void ValidateMp4Renditions(MediaPath primary, IReadOnlyList<VideoRendition> renditions)
        {
            var primaryRendition = renditions[0];
            if (primary.Equals(primaryRendition.Path) is false)
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

        private static void ValidateFormat(MediaPath path, VideoFormat format, string parameterName)
        {
            if (Enum.IsDefined(typeof(VideoFormat), format) is false)
            {
                throw new ArgumentOutOfRangeException(nameof(format));
            }

            var extension = format == VideoFormat.Hls ? ".m3u8" : ".mp4";
            if (path.Value.EndsWith(extension, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new ArgumentException(
                    $"Video path must end with {extension} for format {format}.",
                    parameterName);
            }
        }
    }

    public static class AudioReferenceCodec
    {
        public static string Serialize(AudioReference reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            return JsonConvert.SerializeObject(new AudioReferenceData
            {
                Version = AudioReference.CurrentVersion,
                Path = reference.Path.Value
            });
        }

        public static bool TryDeserialize(string json, out AudioReference reference, out string error)
        {
            reference = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Audio reference JSON cannot be empty.";
                return false;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<AudioReferenceData>(json);
                if (data == null || data.Version != AudioReference.CurrentVersion)
                {
                    error = "Audio reference is invalid or unsupported.";
                    return false;
                }

                reference = new AudioReference(new MediaPath(data.Path));
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        [Serializable]
        private sealed class AudioReferenceData
        {
            [JsonProperty("version", Order = 0)] public int Version { get; set; }
            [JsonProperty("path", Order = 1)] public string Path { get; set; }
        }
    }
}

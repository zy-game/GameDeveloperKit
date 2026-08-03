using System;
using System.Collections.Generic;
using GameDeveloperKit.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using Newtonsoft.Json;

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

                if (TryParseVideoFormat(data.Format, out var format) is false)
                {
                    error = "Video reference format is invalid.";
                    return false;
                }

                var primary = new MediaPath(data.PrimaryPath);
                var renditions = ParseRenditions(data.Renditions);
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
                data.Items.Add(ToData(renditions[i]));
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
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Video rendition metadata cannot be empty.";
                return false;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<VideoRenditionCollectionData>(json, s_Settings);
                if (data == null ||
                    data.Version != VideoReference.CurrentVersion ||
                    data.Items == null)
                {
                    error = "Video rendition metadata is invalid or unsupported.";
                    return false;
                }

                renditions = ParseRenditions(data.Items);
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
            out string error)
        {
            reference = null;
            error = null;
            if (arguments == null)
            {
                error = "Video command arguments are missing.";
                return false;
            }

            if (TryParseVideoFormat(
                    arguments.GetString(MediaCommandNames.VideoFormatArgument),
                    out var format) is false)
            {
                error = "Video format is missing or invalid.";
                return false;
            }

            if (TryDeserializeRenditions(
                    arguments.GetString(MediaCommandNames.VideoRenditionsArgument),
                    out var renditions,
                    out error) is false)
            {
                return false;
            }

            try
            {
                reference = new VideoReference(
                    new MediaPath(arguments.GetString(MediaCommandNames.ClipArgument)),
                    format,
                    renditions);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is ArgumentOutOfRangeException)
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
                PrimaryPath = reference.Primary.Value,
                Format = ToText(reference.Format),
                Renditions = new List<VideoRenditionData>(reference.Renditions.Count)
            };
            for (var i = 0; i < reference.Renditions.Count; i++)
            {
                data.Renditions.Add(ToData(reference.Renditions[i]));
            }

            return data;
        }

        private static VideoRenditionData ToData(VideoRendition rendition)
        {
            return new VideoRenditionData
            {
                Label = rendition.Label,
                Path = rendition.Path.Value,
                Width = rendition.Width,
                Height = rendition.Height,
                Bitrate = rendition.Bitrate,
                DurationMs = rendition.DurationMs
            };
        }

        private static IReadOnlyList<VideoRendition> ParseRenditions(
            IReadOnlyList<VideoRenditionData> items)
        {
            var result = new List<VideoRendition>(items?.Count ?? 0);
            for (var i = 0; i < (items?.Count ?? 0); i++)
            {
                var item = items[i];
                if (item == null)
                {
                    throw new ArgumentException($"Video rendition at index {i} is null.", nameof(items));
                }

                result.Add(new VideoRendition(
                    item.Label,
                    new MediaPath(item.Path),
                    item.Width,
                    item.Height,
                    item.Bitrate,
                    item.DurationMs));
            }

            return result;
        }

        private static string ToText(VideoFormat format)
        {
            return format == VideoFormat.Hls ? "hls" : "mp4";
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
            [JsonProperty("version", Order = 0)] public int Version { get; set; }
            [JsonProperty("primaryPath", Order = 1)] public string PrimaryPath { get; set; }
            [JsonProperty("format", Order = 2)] public string Format { get; set; }
            [JsonProperty("renditions", Order = 3)] public List<VideoRenditionData> Renditions { get; set; }
        }

        [Serializable]
        private sealed class VideoRenditionData
        {
            [JsonProperty("label", Order = 0)] public string Label { get; set; }
            [JsonProperty("path", Order = 1)] public string Path { get; set; }
            [JsonProperty("width", Order = 2)] public int Width { get; set; }
            [JsonProperty("height", Order = 3)] public int Height { get; set; }
            [JsonProperty("bitrate", Order = 4)] public int Bitrate { get; set; }
            [JsonProperty("durationMs", Order = 5)] public long DurationMs { get; set; }
        }

        [Serializable]
        private sealed class VideoRenditionCollectionData
        {
            [JsonProperty("version", Order = 0)] public int Version { get; set; }
            [JsonProperty("items", Order = 1)] public List<VideoRenditionData> Items { get; set; }
        }
    }
}

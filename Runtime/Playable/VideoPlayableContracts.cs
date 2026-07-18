using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Playable
{
    public enum VideoQualityMode
    {
        Auto = 0,
        FixedHeight = 1
    }

    public readonly struct VideoQualitySelection : IEquatable<VideoQualitySelection>
    {
        public VideoQualitySelection(VideoQualityMode mode, int height = 0)
        {
            if (Enum.IsDefined(typeof(VideoQualityMode), mode) is false)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (mode == VideoQualityMode.FixedHeight && height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Mode = mode;
            Height = mode == VideoQualityMode.Auto ? 0 : height;
        }

        public VideoQualityMode Mode { get; }

        public int Height { get; }

        public bool Equals(VideoQualitySelection other)
        {
            return Mode == other.Mode && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is VideoQualitySelection other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Mode * 397) ^ Height;
        }
    }

    public readonly struct VideoQualityOption
    {
        public VideoQualityOption(string label, int width, int height, int bitrate, string location)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (bitrate < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bitrate));
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Video quality location cannot be empty.", nameof(location));
            }

            Label = FormatLabel(height);
            Width = width;
            Height = height;
            Bitrate = bitrate;
            Location = location.Trim();
        }

        public string Label { get; }

        public int Width { get; }

        public int Height { get; }

        public int Bitrate { get; }

        public string Location { get; }

        private static string FormatLabel(int height)
        {
            switch (height)
            {
                case 720: return "720p";
                case 1080: return "1080p";
                case 1440: return "2K";
                case 2160: return "4K";
                default: return $"{height}p";
            }
        }
    }

    public sealed class VideoPlayableOptions
    {
        public bool Loop { get; set; }

        public bool Seekable { get; set; }

        public Transform Parent { get; set; }

        public bool DontDestroyOnLoad { get; set; } = true;

        public bool SupportsAutoQuality { get; set; }

        public VideoQualitySelection InitialQuality { get; set; }

        public IReadOnlyList<VideoQualityOption> QualityOptions { get; set; } = Array.Empty<VideoQualityOption>();
    }

    public sealed class VideoPlayableRequest
    {
        public VideoPlayableRequest(string path, VideoPlayableOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty.", nameof(path));
            }

            Path = path;
            Options = options ?? new VideoPlayableOptions();
        }

        public string Path { get; }

        public VideoPlayableOptions Options { get; }
    }
}

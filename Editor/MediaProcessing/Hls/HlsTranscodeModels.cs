using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameDeveloperKit.MediaEditor
{
    public sealed class HlsRenditionPreset
    {
        public HlsRenditionPreset(
            string label,
            int height,
            int videoBitrate,
            int audioBitrate)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Rendition label cannot be empty.", nameof(label));
            }

            label = label.Trim();
            for (var i = 0; i < label.Length; i++)
            {
                var character = label[i];
                if (char.IsLetterOrDigit(character) is false && character != '-' && character != '_')
                {
                    throw new ArgumentException(
                        "Rendition label may contain only letters, digits, hyphens, and underscores.",
                        nameof(label));
                }
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (videoBitrate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(videoBitrate));
            }

            if (audioBitrate < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(audioBitrate));
            }

            Label = label;
            Height = height;
            VideoBitrate = videoBitrate;
            AudioBitrate = audioBitrate;
        }

        public string Label { get; }
        public int Height { get; }
        public int VideoBitrate { get; }
        public int AudioBitrate { get; }
    }

    public static class HlsRenditionPresets
    {
        private static readonly IReadOnlyList<HlsRenditionPreset> s_Default =
            new ReadOnlyCollection<HlsRenditionPreset>(new[]
            {
                new HlsRenditionPreset("4K", 2160, 16000000, 192000),
                new HlsRenditionPreset("2K", 1440, 6500000, 192000),
                new HlsRenditionPreset("1080P", 1080, 4000000, 128000),
                new HlsRenditionPreset("720P", 720, 2000000, 128000),
                new HlsRenditionPreset("480P", 480, 1000000, 96000)
            });

        public static IReadOnlyList<HlsRenditionPreset> Default => s_Default;
    }

    public sealed class HlsTranscodeRequest
    {
        public HlsTranscodeRequest(
            string inputMp4Path,
            string packageName,
            IReadOnlyList<HlsRenditionPreset> renditions,
            int segmentDurationSeconds = 2,
            bool overwriteExisting = false)
        {
            InputMp4Path = string.IsNullOrWhiteSpace(inputMp4Path)
                ? throw new ArgumentException("Input MP4 path cannot be empty.", nameof(inputMp4Path))
                : inputMp4Path.Trim();
            PackageName = string.IsNullOrWhiteSpace(packageName)
                ? throw new ArgumentException("Package name cannot be empty.", nameof(packageName))
                : packageName.Trim();
            if (renditions == null || renditions.Count == 0)
            {
                throw new ArgumentException("At least one rendition preset is required.", nameof(renditions));
            }

            if (segmentDurationSeconds < 2 || segmentDurationSeconds > 10)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(segmentDurationSeconds),
                    "Segment duration must be between 2 and 10 seconds.");
            }

            Renditions = new ReadOnlyCollection<HlsRenditionPreset>(
                new List<HlsRenditionPreset>(renditions));
            SegmentDurationSeconds = segmentDurationSeconds;
            OverwriteExisting = overwriteExisting;
        }

        public string InputMp4Path { get; }
        public string PackageName { get; }
        public IReadOnlyList<HlsRenditionPreset> Renditions { get; }
        public int SegmentDurationSeconds { get; }
        public bool OverwriteExisting { get; }
    }

    public sealed class MediaProbeInfo
    {
        public MediaProbeInfo(
            int width,
            int height,
            double durationSeconds,
            double frameRate,
            bool hasAudio)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Video dimensions must be positive.");
            }

            if (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            }

            Width = width;
            Height = height;
            DurationSeconds = durationSeconds;
            FrameRate = frameRate;
            HasAudio = hasAudio;
        }

        public int Width { get; }
        public int Height { get; }
        public double DurationSeconds { get; }
        public double FrameRate { get; }
        public bool HasAudio { get; }
    }

    public sealed class HlsRenditionPlan
    {
        internal HlsRenditionPlan(
            string label,
            int width,
            int height,
            int videoBitrate,
            int audioBitrate)
        {
            Label = label;
            Width = width;
            Height = height;
            VideoBitrate = videoBitrate;
            AudioBitrate = audioBitrate;
        }

        public string Label { get; }
        public int Width { get; }
        public int Height { get; }
        public int VideoBitrate { get; }
        public int AudioBitrate { get; }
        public string PlaylistRelativePath => Label + "/index.m3u8";
        public string SegmentRelativePattern => Label + "/segment_%05d.ts";
    }

    public sealed class HlsTranscodePlan
    {
        internal HlsTranscodePlan(
            HlsTranscodeRequest request,
            MediaProbeInfo source,
            string outputDirectory,
            IReadOnlyList<HlsRenditionPlan> renditions)
        {
            Request = request;
            Source = source;
            OutputDirectory = outputDirectory;
            Renditions = renditions;
        }

        public HlsTranscodeRequest Request { get; }
        public MediaProbeInfo Source { get; }
        public string OutputDirectory { get; }
        public string MasterPlaylistPath => System.IO.Path.Combine(OutputDirectory, "master.m3u8");
        public IReadOnlyList<HlsRenditionPlan> Renditions { get; }
    }

    public sealed class HlsRenditionInfo
    {
        public HlsRenditionInfo(string label, int width, int height, int bitrate, string playlistPath)
        {
            Label = label;
            Width = width;
            Height = height;
            Bitrate = bitrate;
            PlaylistPath = playlistPath;
        }

        public string Label { get; }
        public int Width { get; }
        public int Height { get; }
        public int Bitrate { get; }
        public string PlaylistPath { get; }
    }

    public sealed class HlsTranscodeResult
    {
        public HlsTranscodeResult(
            string packageDirectory,
            string masterPlaylistPath,
            IReadOnlyList<HlsRenditionInfo> renditions,
            string standardOutput,
            string standardError)
        {
            PackageDirectory = packageDirectory;
            MasterPlaylistPath = masterPlaylistPath;
            Renditions = renditions;
            StandardOutput = standardOutput ?? string.Empty;
            StandardError = standardError ?? string.Empty;
        }

        public string PackageDirectory { get; }
        public string MasterPlaylistPath { get; }
        public IReadOnlyList<HlsRenditionInfo> Renditions { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
    }

    public enum HlsTranscodeStage
    {
        Probing = 0,
        Planning = 1,
        Encoding = 2,
        Verifying = 3,
        Committing = 4,
        Completed = 5
    }

    public readonly struct HlsTranscodeProgress
    {
        public HlsTranscodeProgress(
            HlsTranscodeStage stage,
            float progress,
            string message,
            string logLine = null)
        {
            Stage = stage;
            Progress = Math.Max(0f, Math.Min(1f, progress));
            Message = message ?? string.Empty;
            LogLine = logLine ?? string.Empty;
        }

        public HlsTranscodeStage Stage { get; }
        public float Progress { get; }
        public string Message { get; }
        public string LogLine { get; }
    }
}

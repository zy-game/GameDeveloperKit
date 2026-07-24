using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using IOFile = System.IO.File;

namespace GameDeveloperKit.MediaEditor
{
    public static class HlsTranscodePlanner
    {
        public const string StreamingAssetsVideoRelativePath = "Assets/StreamingAssets/videos";

        public static HlsTranscodePlan Create(
            HlsTranscodeRequest request,
            MediaProbeInfo source,
            string projectRoot)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var outputDirectory = ValidateRequest(request, projectRoot);

            var renditions = CreateRenditions(request.Renditions, source);
            return new HlsTranscodePlan(
                request,
                source,
                outputDirectory.Replace('\\', '/'),
                new ReadOnlyCollection<HlsRenditionPlan>(renditions));
        }

        internal static string ValidateRequest(HlsTranscodeRequest request, string projectRoot)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root cannot be empty.", nameof(projectRoot));
            }

            var inputPath = Path.GetFullPath(request.InputMp4Path);
            if (IOFile.Exists(inputPath) is false)
            {
                throw new FileNotFoundException("Input MP4 does not exist.", inputPath);
            }

            if (string.Equals(Path.GetExtension(inputPath), ".mp4", StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new ArgumentException("Only MP4 input is supported.", nameof(request));
            }

            ValidatePackageName(request.PackageName);
            var outputRoot = Path.GetFullPath(Path.Combine(projectRoot, StreamingAssetsVideoRelativePath));
            var outputDirectory = Path.GetFullPath(Path.Combine(outputRoot, request.PackageName));
            var rootWithSeparator = outputRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (outputDirectory.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new ArgumentException("HLS output escapes StreamingAssets/videos.", nameof(request));
            }

            return outputDirectory.Replace('\\', '/');
        }

        private static List<HlsRenditionPlan> CreateRenditions(
            IReadOnlyList<HlsRenditionPreset> presets,
            MediaProbeInfo source)
        {
            var selected = presets
                .Where(preset => preset != null && preset.Height <= source.Height)
                .GroupBy(preset => preset.Height)
                .Select(group => group.First())
                .OrderByDescending(preset => preset.Height)
                .ToList();
            if (selected.Count == 0)
            {
                selected.Add(new HlsRenditionPreset(
                    source.Height + "P",
                    source.Height,
                    Math.Max(500000, (int)Math.Round(1000000d * source.Height / 480d)),
                    96000));
            }

            if (selected.Select(preset => preset.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                selected.Count)
            {
                throw new ArgumentException("Rendition labels must be unique.", nameof(presets));
            }

            var result = new List<HlsRenditionPlan>(selected.Count);
            for (var i = 0; i < selected.Count; i++)
            {
                var preset = selected[i];
                var width = CalculateEvenWidth(source.Width, source.Height, preset.Height);
                result.Add(new HlsRenditionPlan(
                    preset.Label,
                    width,
                    preset.Height,
                    preset.VideoBitrate,
                    source.HasAudio ? preset.AudioBitrate : 0));
            }

            return result;
        }

        private static int CalculateEvenWidth(int sourceWidth, int sourceHeight, int targetHeight)
        {
            var scaled = sourceWidth * (double)targetHeight / sourceHeight;
            return Math.Max(2, (int)Math.Round(scaled / 2d) * 2);
        }

        private static void ValidatePackageName(string packageName)
        {
            if (packageName == "." || packageName == ".." ||
                packageName.IndexOf('/') >= 0 ||
                packageName.IndexOf('\\') >= 0 ||
                packageName.IndexOf(':') >= 0 ||
                packageName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    "Package name must be one safe directory segment.",
                    nameof(packageName));
            }
        }
    }
}

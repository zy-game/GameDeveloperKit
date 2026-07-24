using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GameDeveloperKit.Story.Media;
using IOFile = System.IO.File;
using UnityEditor;
using UnityEngine;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class StreamingAssetsVideoScanner
    {
        public IReadOnlyList<VideoReference> Scan(string streamingAssetsPath)
        {
            if (string.IsNullOrWhiteSpace(streamingAssetsPath))
            {
                throw new ArgumentException("StreamingAssets path cannot be empty.", nameof(streamingAssetsPath));
            }

            var root = Path.GetFullPath(streamingAssetsPath);
            if (Directory.Exists(root) is false)
            {
                return Array.Empty<VideoReference>();
            }

            var references = new List<VideoReference>();
            foreach (var filePath in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(filePath);
                if (string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    references.Add(CreateReference(root, filePath, VideoFormat.Mp4, Array.Empty<VideoRendition>()));
                }
                else if (string.Equals(extension, ".m3u8", StringComparison.OrdinalIgnoreCase) &&
                         TryReadVodMaster(root, filePath, out var renditions))
                {
                    references.Add(CreateReference(root, filePath, VideoFormat.Hls, renditions));
                }
            }

            references.Sort((left, right) => string.Compare(left.Primary.Location, right.Primary.Location, StringComparison.Ordinal));
            return references;
        }

        private static VideoReference CreateReference(
            string root,
            string filePath,
            VideoFormat format,
            IReadOnlyList<VideoRendition> renditions)
        {
            var relative = ToRelativeLocation(root, filePath);
            return new VideoReference(
                new MediaReference(MediaKind.Video, MediaSource.StreamingAssets, string.Empty, relative),
                format,
                renditions);
        }

        private static bool TryReadVodMaster(string root, string masterPath, out IReadOnlyList<VideoRendition> renditions)
        {
            renditions = Array.Empty<VideoRendition>();
            string[] lines;
            try
            {
                lines = IOFile.ReadAllLines(masterPath);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            var result = new List<VideoRendition>();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal) is false)
                {
                    continue;
                }

                var locationIndex = i + 1;
                while (locationIndex < lines.Length &&
                       (string.IsNullOrWhiteSpace(lines[locationIndex]) || lines[locationIndex].TrimStart().StartsWith("#", StringComparison.Ordinal)))
                {
                    locationIndex++;
                }

                if (locationIndex >= lines.Length)
                {
                    return false;
                }

                var childLocation = lines[locationIndex].Trim().Replace('\\', '/');
                if (Uri.TryCreate(childLocation, UriKind.Absolute, out _) || childLocation.StartsWith("/", StringComparison.Ordinal))
                {
                    return false;
                }

                var childPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(masterPath) ?? root, childLocation.Replace('/', Path.DirectorySeparatorChar)));
                if (IsInside(root, childPath) is false || IOFile.Exists(childPath) is false || IsVodPlaylist(childPath) is false)
                {
                    return false;
                }

                ParseStreamInfo(line, out var width, out var height, out var bitrate);
                result.Add(new VideoRendition(
                    FormatResolutionLabel(height, childPath),
                    string.Empty,
                    ToRelativeLocation(root, childPath),
                    width,
                    height,
                    bitrate,
                    0));
                i = locationIndex;
            }

            if (result.Count == 0)
            {
                return false;
            }

            renditions = result;
            return true;
        }

        private static string FormatResolutionLabel(int height, string childPath)
        {
            switch (height)
            {
                case 2160: return "4K";
                case 1440: return "2K";
                default:
                    return height > 0
                        ? height.ToString(CultureInfo.InvariantCulture) + "P"
                        : Path.GetFileNameWithoutExtension(childPath);
            }
        }

        private static bool IsVodPlaylist(string path)
        {
            try
            {
                foreach (var line in IOFile.ReadLines(path))
                {
                    if (string.Equals(line.Trim(), "#EXT-X-ENDLIST", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return false;
        }

        private static void ParseStreamInfo(string line, out int width, out int height, out int bitrate)
        {
            width = 0;
            height = 0;
            bitrate = 0;
            var attributes = line.Substring("#EXT-X-STREAM-INF:".Length).Split(',');
            for (var i = 0; i < attributes.Length; i++)
            {
                var pair = attributes[i].Split(new[] { '=' }, 2);
                if (pair.Length != 2)
                {
                    continue;
                }

                if (string.Equals(pair[0].Trim(), "BANDWIDTH", StringComparison.Ordinal))
                {
                    int.TryParse(pair[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out bitrate);
                }
                else if (string.Equals(pair[0].Trim(), "RESOLUTION", StringComparison.Ordinal))
                {
                    var resolution = pair[1].Trim().Split('x');
                    if (resolution.Length == 2)
                    {
                        int.TryParse(resolution[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width);
                        int.TryParse(resolution[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
                    }
                }
            }
        }

        private static string ToRelativeLocation(string root, string filePath)
        {
            var rootUri = new Uri(EnsureTrailingSeparator(Path.GetFullPath(root)), UriKind.Absolute);
            var fileUri = new Uri(Path.GetFullPath(filePath), UriKind.Absolute);
            var relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('\\', '/');
            if (relative.StartsWith("../", StringComparison.Ordinal) || relative.IndexOf("/../", StringComparison.Ordinal) >= 0)
            {
                throw new CatalogException(CatalogErrorKind.InvalidLocation, "Local video is outside StreamingAssets.");
            }

            return relative;
        }

        private static bool IsInside(string root, string path)
        {
            var prefix = EnsureTrailingSeparator(Path.GetFullPath(root));
            return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }

    internal static class AudioReferenceSources
    {
        private static readonly HashSet<string> s_Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".ogg", ".wav", ".aif", ".aiff"
        };

        public static IReadOnlyList<MediaReference> ScanStreamingAssets(string streamingAssetsPath)
        {
            if (string.IsNullOrWhiteSpace(streamingAssetsPath))
            {
                throw new ArgumentException("StreamingAssets path cannot be empty.", nameof(streamingAssetsPath));
            }

            var root = Path.GetFullPath(streamingAssetsPath);
            if (Directory.Exists(root) is false)
            {
                return Array.Empty<MediaReference>();
            }

            var result = new List<MediaReference>();
            foreach (var path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                if (s_Extensions.Contains(Path.GetExtension(path)) is false)
                {
                    continue;
                }

                var relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
                result.Add(new MediaReference(MediaKind.Audio, MediaSource.StreamingAssets, string.Empty, relative));
            }

            result.Sort((left, right) => string.Compare(left.Location, right.Location, StringComparison.Ordinal));
            return result;
        }

        public static IReadOnlyList<MediaReference> ReadResourceSnapshot()
        {
            var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot();
            var result = new List<MediaReference>();
            foreach (var previews in snapshot.Previews.Values)
            {
                foreach (var preview in previews)
                {
                    if (preview == null ||
                        string.IsNullOrWhiteSpace(preview.Location) ||
                        string.IsNullOrWhiteSpace(preview.AssetPath))
                    {
                        continue;
                    }

                    var type = AssetDatabase.GetMainAssetTypeAtPath(preview.AssetPath);
                    if (type != null && typeof(AudioClip).IsAssignableFrom(type))
                    {
                        result.Add(new MediaReference(MediaKind.Audio, MediaSource.Resource, string.Empty, preview.Location));
                    }
                }
            }

            result.Sort((left, right) => string.Compare(left.Location, right.Location, StringComparison.Ordinal));
            return result;
        }
    }
}

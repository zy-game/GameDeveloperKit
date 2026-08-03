using System;
using System.Collections.Generic;
using System.IO;
using GameDeveloperKit.Resource;
using GameDeveloperKit.ResourceEditor.Authoring;
using GameDeveloperKit.Story.Media;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Media
{
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

                var relative = path.Substring(root.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                result.Add(new MediaReference(MediaKind.Audio, MediaSource.StreamingAssets, string.Empty, relative));
            }

            result.Sort((left, right) => string.Compare(left.Location, right.Location, StringComparison.Ordinal));
            return result;
        }

        public static IReadOnlyList<MediaReference> ReadResourceSnapshot()
        {
            var snapshot = Service.BuildSnapshot();
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

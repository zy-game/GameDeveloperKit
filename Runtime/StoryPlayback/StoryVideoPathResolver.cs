using System;
using System.IO;
using UnityEngine;

namespace GameDeveloperKit.Story
{
    public static class StoryVideoPathResolver
    {
        private const string AssetsPrefix = "Assets/";
        private const string AssetsStreamingAssetsPrefix = "Assets/StreamingAssets/";
        private const string StreamingAssetsPrefix = "StreamingAssets/";

        public static string Resolve(string source, string clip)
        {
            return TryResolve(source, clip, out var resolvedPath, out _)
                ? resolvedPath
                : null;
        }

        public static bool TryResolve(string source, string clip, out string resolvedPath, out string errorMessage)
        {
            resolvedPath = null;
            errorMessage = null;

            source = TrimToNull(source);
            clip = TrimToNull(clip);
            if (string.IsNullOrWhiteSpace(source))
            {
                errorMessage = "Video source is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(clip))
            {
                errorMessage = "Video clip path is missing.";
                return false;
            }

            if (IsGuidReference(clip))
            {
                errorMessage = "Video clip cannot use guid reference.";
                return false;
            }

            switch (source)
            {
                case StoryMediaCommandNames.VideoSourceStreamingAssets:
                    return TryResolveStreamingAssets(clip, out resolvedPath, out errorMessage);
                case StoryMediaCommandNames.VideoSourcePersistentDataPath:
                    return TryResolvePersistentDataPath(clip, out resolvedPath, out errorMessage);
                case StoryMediaCommandNames.VideoSourceNetworkStream:
                    return TryResolveNetworkStream(clip, out resolvedPath, out errorMessage);
                default:
                    errorMessage = $"Video source is invalid. source:{source}";
                    return false;
            }
        }

        private static bool TryResolveStreamingAssets(string clip, out string resolvedPath, out string errorMessage)
        {
            resolvedPath = null;
            errorMessage = null;

            if (IsNetworkUrl(clip))
            {
                errorMessage = "StreamingAssets video clip must be a local relative path.";
                return false;
            }

            var normalized = NormalizeSeparators(clip);
            if (IsLocalAbsolutePath(clip, normalized))
            {
                errorMessage = "Video clip cannot be a local absolute path.";
                return false;
            }

            if (normalized.StartsWith(AssetsStreamingAssetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(AssetsStreamingAssetsPrefix.Length);
            }
            else if (normalized.StartsWith(StreamingAssetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(StreamingAssetsPrefix.Length);
            }
            else if (normalized.StartsWith(AssetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Video clip under Assets must be placed in StreamingAssets.";
                return false;
            }

            if (IsSafeRelativePath(normalized) is false)
            {
                errorMessage = "Video clip must be a relative path inside StreamingAssets.";
                return false;
            }

            resolvedPath = NormalizeSeparators(Path.Combine(Application.streamingAssetsPath, normalized));
            return true;
        }

        private static bool TryResolvePersistentDataPath(string clip, out string resolvedPath, out string errorMessage)
        {
            resolvedPath = null;
            errorMessage = null;

            if (IsNetworkUrl(clip))
            {
                errorMessage = "Persistent data video clip must be a local relative path.";
                return false;
            }

            var normalized = NormalizeSeparators(clip);
            if (IsLocalAbsolutePath(clip, normalized))
            {
                errorMessage = "Video clip cannot be a local absolute path.";
                return false;
            }

            if (normalized.StartsWith(AssetsPrefix, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(StreamingAssetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Persistent data video clip must be relative to persistentDataPath.";
                return false;
            }

            if (IsSafeRelativePath(normalized) is false)
            {
                errorMessage = "Video clip must be a relative path inside persistentDataPath.";
                return false;
            }

            resolvedPath = NormalizeSeparators(Path.Combine(Application.persistentDataPath, normalized));
            return true;
        }

        private static bool TryResolveNetworkStream(string clip, out string resolvedPath, out string errorMessage)
        {
            resolvedPath = null;
            errorMessage = null;

            if (IsNetworkUrl(clip) is false)
            {
                errorMessage = "Network stream video clip must be a URL.";
                return false;
            }

            resolvedPath = clip;
            return true;
        }

        private static bool IsGuidReference(string value)
        {
            return value.Length > 4 &&
                   value[4] == ':' &&
                   value.StartsWith("guid", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNetworkUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.IndexOf("://", StringComparison.Ordinal) < 0 ||
                Uri.TryCreate(value, UriKind.Absolute, out var uri) is false)
            {
                return false;
            }

            return string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase) is false;
        }

        private static bool IsLocalAbsolutePath(string original, string normalized)
        {
            if (Path.IsPathRooted(original))
            {
                return true;
            }

            return normalized.Length >= 3 &&
                   char.IsLetter(normalized[0]) &&
                   normalized[1] == ':' &&
                   normalized[2] == '/';
        }

        private static bool IsSafeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.StartsWith("/", StringComparison.Ordinal) ||
                value.StartsWith("\\", StringComparison.Ordinal))
            {
                return false;
            }

            var parts = NormalizeSeparators(value).Split('/');
            for (var i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i], "..", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeSeparators(string value)
        {
            return value?.Replace('\\', '/');
        }

        private static string TrimToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

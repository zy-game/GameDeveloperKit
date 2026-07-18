using System;
using System.IO;
using UnityEngine;
using GameDeveloperKit.Story.Protocol;

namespace GameDeveloperKit.Story.Playback
{
    public static class VideoPathResolver
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
                case MediaCommandNames.VideoSourceCdn:
                    return TryResolveCdn(clip, out resolvedPath, out errorMessage);
                case MediaCommandNames.VideoSourceStreamingAssets:
                    return TryResolveStreamingAssets(clip, out resolvedPath, out errorMessage);
                default:
                    errorMessage = $"Video source is invalid. source:{source}";
                    return false;
            }
        }

        private static bool TryResolveCdn(string clip, out string resolvedPath, out string errorMessage)
        {
            resolvedPath = null;
            errorMessage = null;
            if (Uri.TryCreate(clip, UriKind.Absolute, out var uri) is false ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                string.IsNullOrWhiteSpace(uri.UserInfo) is false)
            {
                errorMessage = "CDN video clip must be an absolute HTTPS URL.";
                return false;
            }

            resolvedPath = clip;
            return true;
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
                if (string.IsNullOrWhiteSpace(parts[i]) ||
                    string.Equals(parts[i], ".", StringComparison.Ordinal) ||
                    string.Equals(parts[i], "..", StringComparison.Ordinal))
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

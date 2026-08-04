using System;
using System.IO;
using System.Text;

namespace GameDeveloperKit.DesignImporter
{
    internal static class DesignPathUtility
    {
        public static string SanitizeFileName(string value, string fallback = "Unnamed")
        {
            var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(source.Length);
            foreach (var character in source)
            {
                if (Array.IndexOf(invalid, character) >= 0 || character == '/' || character == '\\')
                {
                    builder.Append('_');
                }
                else
                {
                    builder.Append(character);
                }
            }

            var result = builder.ToString().Trim().Trim('.');
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        public static string NormalizeImageExtension(string extension)
        {
            var value = (extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
            return value switch
            {
                "jpg" => "jpg",
                "jpeg" => "jpg",
                "webp" => "webp",
                "tga" => "tga",
                _ => "png"
            };
        }

        public static string EnsureAssetsPath(string path)
        {
            var normalized = (path ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');
            if (!string.Equals(normalized, "Assets", StringComparison.Ordinal) &&
                !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException("输出目录必须位于 Assets 下。", nameof(path));
            }

            var segments = normalized.Split('/');
            var invalidCharacters = Path.GetInvalidFileNameChars();
            for (var i = 1; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (string.IsNullOrWhiteSpace(segment) ||
                    string.Equals(segment, ".", StringComparison.Ordinal) ||
                    string.Equals(segment, "..", StringComparison.Ordinal) ||
                    segment.IndexOfAny(invalidCharacters) >= 0)
                {
                    throw new ArgumentException("输出目录包含无效的路径片段。", nameof(path));
                }
            }

            return normalized;
        }
    }
}

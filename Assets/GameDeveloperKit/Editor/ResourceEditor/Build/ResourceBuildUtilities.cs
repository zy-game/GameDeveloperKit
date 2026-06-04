using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GameDeveloperKit.ResourceEditor
{
    public static class ResourceBuildUtilities
    {
        public static string NormalizePath(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        public static string SanitizeSegment(string value, string fallback)
        {
            var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            var builder = new StringBuilder(source.Length);
            foreach (var ch in source)
            {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
                {
                    builder.Append(ch);
                }
                else
                {
                    builder.Append('-');
                }
            }

            var result = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        public static string CombineRemoteKey(params string[] segments)
        {
            return string.Join("/", segments
                .Select(NormalizePath)
                .Where(x => string.IsNullOrWhiteSpace(x) is false));
        }

        public static string ComputeHash(string path)
        {
            using (var stream = System.IO.File.OpenRead(path))
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static string ComputeHashFromText(string value)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static string ProjectRelativeOrAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var normalized = path.Replace('\\', '/');
            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            return Path.GetFullPath(normalized).Replace('\\', '/');
        }
    }
}

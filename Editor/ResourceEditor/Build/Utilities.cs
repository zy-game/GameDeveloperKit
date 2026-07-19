using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameDeveloperKit.ChannelBuild;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceEditor.Build
{
    /// <summary>
    /// 定义 Resource Build Utilities 类型。
    /// </summary>
    public static class Utilities
    {
        /// <summary>
        /// 获取 Resource Editor 与 Channel Build profile 中声明的渠道名称。
        /// </summary>
        /// <param name="settings">当前资源构建设置。</param>
        /// <param name="projectRoot">Unity 项目根目录；为空时使用当前目录。</param>
        /// <returns>按序排列且去重的渠道名称。</returns>
        public static IReadOnlyList<string> GetConfiguredChannelNames(
            Settings settings,
            string projectRoot = null)
        {
            var channels = new HashSet<string>(StringComparer.Ordinal)
            {
                ResourceSettings.DEFAULT_CHANNEL_NAME
            };

            if (settings != null)
            {
                foreach (var channel in settings.Channels)
                {
                    channels.Add(channel);
                }
            }

            var root = string.IsNullOrWhiteSpace(projectRoot)
                ? Directory.GetCurrentDirectory()
                : projectRoot;
            var catalogPath = Path.Combine(root, ChannelProfileSource.DefaultRelativePath);
            if (System.IO.File.Exists(catalogPath))
            {
                foreach (var profile in ChannelProfileSource.Load(root).Profiles)
                {
                    channels.Add(profile.Channel);
                }
            }

            return channels.OrderBy(channel => channel, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// 执行 Normalize Path。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <returns>执行结果。</returns>
        public static string NormalizePath(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// 执行 Sanitize Segment。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <param name="fallback">fallback 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 Combine Remote Key。
        /// </summary>
        /// <param name="segments">segments 参数。</param>
        /// <returns>执行结果。</returns>
        public static string CombineRemoteKey(params string[] segments)
        {
            return string.Join("/", segments
                .Select(NormalizePath)
                .Where(x => string.IsNullOrWhiteSpace(x) is false));
        }

        /// <summary>
        /// 执行 Compute Hash。
        /// </summary>
        /// <param name="path">path 参数。</param>
        /// <returns>执行结果。</returns>
        public static string ComputeHash(string path)
        {
            using (var stream = System.IO.File.OpenRead(path))
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>
        /// 执行 Compute Hash From Text。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <returns>执行结果。</returns>
        public static string ComputeHashFromText(string value)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>
        /// 执行 Project Relative Or Absolute Path。
        /// </summary>
        /// <param name="path">path 参数。</param>
        /// <returns>执行结果。</returns>
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

        public static string ProjectRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var fullPath = Path.GetFullPath(path).Replace('\\', '/');
            var projectPath = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/');
            var projectPrefix = projectPath + "/";
            return fullPath.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(projectPrefix.Length)
                : string.Empty;
        }
    }
}

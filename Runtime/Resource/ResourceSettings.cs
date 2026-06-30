using System;
using System.Text;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源设置类，用于存储和管理资源相关的设置和配置，包括资源加载模式、默认资源包列表和资源服务器URL等信息。
    /// </summary>
    [Serializable]
    public sealed class ResourceSettings
    {
        public const string MANIFEST_NAME = "manifest.json";

        /// <summary>
        /// 资源模式，表示资源加载的模式，包括编辑器模拟、离线、在线和Web等模式。这些模式定义了资源加载和使用的不同方式，开发者可以根据需要选择适合的模式来控制资源的加载和管理行为，从而提高游戏的性能和用户体验。
        /// </summary>
        public ResourceMode Mode;

        /// <summary>
        /// 默认资源包列表，表示在资源加载过程中默认使用的资源包列表。这些资源包包含了游戏中需要加载和使用的各种资源，如纹理、模型、音频等。通过指定默认资源包列表，开发者可以确保在资源加载过程中能够正确地找到和加载所需的资源，从而提高游戏的性能和用户体验。
        /// </summary>
        public string[] DefaultPackages = Array.Empty<string>();

        /// <summary>
        /// Publisher 渠道ID。
        /// </summary>
        public string ChannelId;

        /// <summary>
        /// Publisher 渠道名称。
        /// </summary>
        public string ChannelName;

        /// <summary>
        /// 资源服务器URL。
        /// </summary>
        public string ServerUrl;

        /// <summary>
        /// 资源清单文件名或完整路径。
        /// </summary>
        public string ManifestName = MANIFEST_NAME;

        /// <summary>
        /// 资源缓存路径。
        /// </summary>
        public string CachePath;

        /// <summary>
        /// 获取 Publish Address。
        /// </summary>
        public string GetPublishAddress()
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                return ResolveManifestName();
            }

            return CombineAddress(ServerUrl, ResolveChannelSegment(), ResolvePlatformSegment(), "publish.json");
        }

        /// <summary>
        /// 获取 Manifest Address。
        /// </summary>
        public string GetManifestAddress(string version)
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                return ResolveManifestName();
            }

            ValidateVersion(version);
            var versionSegment = ResolveVersionSegment(version);
            return CombineAddress(ServerUrl, ResolveChannelSegment(), ResolvePlatformSegment(), versionSegment, ResolveManifestName());
        }

        /// <summary>
        /// 获取 Asset Address。
        /// </summary>
        public string GetAssetAddress(string name, string version)
        {
            if (name == null)
            {
                throw new System.ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new System.ArgumentException("Bundle name cannot be empty.", nameof(name));
            }

            ValidateVersion(version);
            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                return name;
            }

            var channelSegment = ResolveChannelSegment();
            var platformSegment = ResolvePlatformSegment();
            var versionSegment = ResolveVersionSegment(version);
            return CombineAddress(ServerUrl, channelSegment, platformSegment, versionSegment, NormalizeBundleName(name, channelSegment, platformSegment, versionSegment));
        }

        /// <summary>
        /// 解析 Manifest Name。
        /// </summary>
        private string ResolveManifestName()
        {
            return string.IsNullOrWhiteSpace(ManifestName) ? MANIFEST_NAME : ManifestName;
        }

        /// <summary>
        /// 校验 Version。
        /// </summary>
        private static void ValidateVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new System.ArgumentException("Version cannot be empty.", nameof(version));
            }
        }

        /// <summary>
        /// 解析远端渠道目录。
        /// </summary>
        private string ResolveChannelSegment()
        {
            return SanitizeSegment(ChannelName, "dev");
        }

        /// <summary>
        /// 解析远端平台目录。
        /// </summary>
        private static string ResolvePlatformSegment()
        {
            return SanitizeSegment(GetRuntimePlatform(), "platform");
        }

        /// <summary>
        /// 解析远端版本目录。
        /// </summary>
        private static string ResolveVersionSegment(string version)
        {
            return SanitizeSegment(version, "version");
        }

        /// <summary>
        /// 执行 Normalize Bundle Name。
        /// </summary>
        private static string NormalizeBundleName(string name, string channel, string platform, string version)
        {
            var normalized = name.Replace('\\', '/').TrimStart('/');
            var channelPrefix = $"{channel}/{platform}/{version}/";
            if (normalized.StartsWith(channelPrefix, System.StringComparison.Ordinal))
            {
                return normalized.Substring(channelPrefix.Length);
            }

            var platformPrefix = $"{platform}/{version}/";
            if (normalized.StartsWith(platformPrefix, System.StringComparison.Ordinal))
            {
                return normalized.Substring(platformPrefix.Length);
            }

            var segments = normalized.Split('/');
            if (segments.Length >= 5 &&
                string.Equals(segments[1], channel, System.StringComparison.Ordinal) &&
                string.Equals(segments[2], platform, System.StringComparison.Ordinal) &&
                string.Equals(segments[3], version, System.StringComparison.Ordinal))
            {
                return string.Join("/", segments, 4, segments.Length - 4);
            }

            if (segments.Length >= 4 &&
                string.Equals(segments[1], platform, System.StringComparison.Ordinal) &&
                string.Equals(segments[2], version, System.StringComparison.Ordinal))
            {
                return string.Join("/", segments, 3, segments.Length - 3);
            }

            return normalized;
        }

        /// <summary>
        /// 清理远端路径片段。
        /// </summary>
        private static string SanitizeSegment(string value, string fallback)
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
        /// 执行 Combine Address。
        /// </summary>
        private static string CombineAddress(params string[] segments)
        {
            return string.Join("/", System.Linq.Enumerable.Where(
                System.Linq.Enumerable.Select(segments, x => (x ?? string.Empty).Replace('\\', '/').Trim('/')),
                x => string.IsNullOrWhiteSpace(x) is false));
        }

        /// <summary>
        /// 获取 Runtime Platform。
        /// </summary>
        private static string GetRuntimePlatform()
        {
#if UNITY_STANDALONE_WIN
            return "StandaloneWindows64";
#elif UNITY_STANDALONE_OSX
            return "StandaloneOSX";
#elif UNITY_STANDALONE_LINUX
            return "StandaloneLinux64";
#elif UNITY_ANDROID
            return "Android";
#elif UNITY_IOS
            return "iOS";
#elif UNITY_WEBGL
            return "WebGL";
#else
            return Application.platform.ToString();
#endif
        }
    }
}

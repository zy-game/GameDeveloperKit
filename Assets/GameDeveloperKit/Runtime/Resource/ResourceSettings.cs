using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源设置类，继承自ScriptableObject，用于存储和管理资源相关的设置和配置，包括资源加载模式、默认资源包列表和资源服务器URL等信息。这种设计模式有助于在Unity编辑器中方便地创建和编辑资源设置实例，并且可以通过脚本访问和使用这些设置来控制资源的加载和管理行为。通过使用ResourceSettings类，开发者可以更好地组织和优化资源的使用，提高游戏的性能和用户体验。 --- IGNORE ---
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceSettings", menuName = "GameDeveloperKit/ResourceSettings")]
    public sealed class ResourceSettings : ScriptableObject
    {
        /// <summary>
        /// 定义 MANIFEST NAME 常量。
        /// </summary>
        public const string MANIFEST_NAME = "manifest.json";

        /// <summary>
        /// 资源模式，表示资源加载的模式，包括编辑器模拟、离线、在线和Web等模式。这些模式定义了资源加载和使用的不同方式，开发者可以根据需要选择适合的模式来控制资源的加载和管理行为，从而提高游戏的性能和用户体验。
        /// </summary>
        public ResourceMode Mode;

        /// <summary>
        /// 默认资源包列表，表示在资源加载过程中默认使用的资源包列表。这些资源包包含了游戏中需要加载和使用的各种资源，如纹理、模型、音频等。通过指定默认资源包列表，开发者可以确保在资源加载过程中能够正确地找到和加载所需的资源，从而提高游戏的性能和用户体验。
        /// </summary>
        public string[] DefaultPackages;

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
        /// <returns>执行结果。</returns>
        public string GetPublishAddress()
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                return ResolveManifestName();
            }

            return CombineAddress(ServerUrl, GetRuntimePlatform(), "publish.json");
        }

        /// <summary>
        /// 获取 Manifest Address。
        /// </summary>
        /// <param name="version">version 参数。</param>
        /// <returns>执行结果。</returns>
        public string GetManifestAddress(string version)
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                return ResolveManifestName();
            }

            ValidateVersion(version);
            return CombineAddress(ServerUrl, GetRuntimePlatform(), version, ResolveManifestName());
        }

        /// <summary>
        /// 获取 Asset Address。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <param name="version">version 参数。</param>
        /// <returns>执行结果。</returns>
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

            return CombineAddress(ServerUrl, GetRuntimePlatform(), version, NormalizeBundleName(name, version));
        }

        /// <summary>
        /// 解析 Manifest Name。
        /// </summary>
        /// <returns>执行结果。</returns>
        private string ResolveManifestName()
        {
            return string.IsNullOrWhiteSpace(ManifestName) ? MANIFEST_NAME : ManifestName;
        }

        /// <summary>
        /// 校验 Version。
        /// </summary>
        /// <param name="version">version 参数。</param>
        private static void ValidateVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new System.ArgumentException("Version cannot be empty.", nameof(version));
            }
        }

        /// <summary>
        /// 执行 Normalize Bundle Name。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <param name="version">version 参数。</param>
        /// <returns>执行结果。</returns>
        private static string NormalizeBundleName(string name, string version)
        {
            var normalized = name.Replace('\\', '/').TrimStart('/');
            var prefix = $"{GetRuntimePlatform()}/{version}/";
            if (normalized.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return normalized.Substring(prefix.Length);
            }

            var segments = normalized.Split('/');
            if (segments.Length >= 4 && string.Equals(segments[1], GetRuntimePlatform(), System.StringComparison.Ordinal) && string.Equals(segments[2], version, System.StringComparison.Ordinal))
            {
                return string.Join("/", segments, 3, segments.Length - 3);
            }

            return normalized;
        }

        /// <summary>
        /// 执行 Combine Address。
        /// </summary>
        /// <param name="segments">segments 参数。</param>
        /// <returns>执行结果。</returns>
        private static string CombineAddress(params string[] segments)
        {
            return string.Join("/", System.Linq.Enumerable.Where(
                System.Linq.Enumerable.Select(segments, x => (x ?? string.Empty).Replace('\\', '/').Trim('/')),
                x => string.IsNullOrWhiteSpace(x) is false));
        }

        /// <summary>
        /// 获取 Runtime Platform。
        /// </summary>
        /// <returns>执行结果。</returns>
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

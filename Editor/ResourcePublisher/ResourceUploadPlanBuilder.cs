using System;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using GameDeveloperKit.ResourceEditor;
using Newtonsoft.Json;

namespace GameDeveloperKit.ResourcePublisher
{
    /// <summary>
    /// 定义 Resource Upload Plan Builder 类型。
    /// </summary>
    public static class ResourceUploadPlanBuilder
    {
        /// <summary>
        /// 构建 member。
        /// </summary>
        /// <param name="versionRoot">version Root 参数。</param>
        /// <param name="channel">channel 参数。</param>
        /// <returns>执行结果。</returns>
        public static ResourceUploadPlan Build(string versionRoot, PublisherChannel channel)
        {
            if (string.IsNullOrWhiteSpace(versionRoot))
            {
                throw new ArgumentException("Version root cannot be empty.", nameof(versionRoot));
            }

            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            if (Directory.Exists(versionRoot) is false)
            {
                throw new DirectoryNotFoundException(versionRoot);
            }

            var manifestPath = Path.Combine(versionRoot, ResourceSettings.MANIFEST_NAME).Replace('\\', '/');
            if (System.IO.File.Exists(manifestPath) is false)
            {
                throw new FileNotFoundException($"Manifest not found: {manifestPath}", manifestPath);
            }

            var version = ResolveVersion(manifestPath, versionRoot);
            var plan = new ResourceUploadPlan
            {
                Version = version,
                BuildTarget = PlatformSegment(channel),
                Channel = ChannelSegment(channel),
                IndexKey = IndexKey(channel)
            };

            foreach (var path in Directory.EnumerateFiles(versionRoot, "*", SearchOption.TopDirectoryOnly)
                         .Where(IsUploadFile)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                var fileName = Path.GetFileName(path);
                var remoteKey = CombineRemoteKey(ChannelSegment(channel), PlatformSegment(channel), VersionSegment(version), fileName);
                var item = new StorageUploadItem
                {
                    LocalPath = path.Replace('\\', '/'),
                    RemoteKey = remoteKey,
                    Hash = GameDeveloperKit.ResourceEditor.Build.Utilities.ComputeHash(path),
                    Size = new FileInfo(path).Length
                };
                plan.Items.Add(item);

                if (string.Equals(fileName, ResourceSettings.MANIFEST_NAME, StringComparison.OrdinalIgnoreCase))
                {
                    plan.ManifestKey = remoteKey;
                }
            }

            return plan;
        }

        /// <summary>
        /// 执行 Index Key。
        /// </summary>
        /// <param name="channel">channel 参数。</param>
        /// <returns>执行结果。</returns>
        public static string IndexKey(PublisherChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            return CombineRemoteKey(ChannelSegment(channel), PlatformSegment(channel), "publish.json");
        }

        /// <summary>
        /// 执行 Version Prefix。
        /// </summary>
        /// <param name="channel">channel 参数。</param>
        /// <param name="version">version 参数。</param>
        /// <returns>执行结果。</returns>
        public static string VersionPrefix(PublisherChannel channel, string version)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            return CombineRemoteKey(ChannelSegment(channel), PlatformSegment(channel), VersionSegment(version)) + "/";
        }

        /// <summary>
        /// 构建 Root。
        /// </summary>
        /// <param name="channel">channel 参数。</param>
        /// <returns>执行结果。</returns>
        public static string BuildRoot(PublisherChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            return CombineRemoteKey(ChannelSegment(channel), PlatformSegment(channel)) + "/";
        }

        /// <summary>
        /// 解析 Version。
        /// </summary>
        /// <param name="manifestPath">manifest Path 参数。</param>
        /// <param name="versionRoot">version Root 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ResolveVersion(string manifestPath, string versionRoot)
        {
            var manifest = JsonConvert.DeserializeObject<ManifestInfo>(System.IO.File.ReadAllText(manifestPath));
            if (string.IsNullOrWhiteSpace(manifest?.Version) is false)
            {
                return manifest.Version;
            }

            return Path.GetFileName(versionRoot.TrimEnd('/', '\\'));
        }

        /// <summary>
        /// 执行 Is Upload File。
        /// </summary>
        /// <param name="path">path 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        private static bool IsUploadFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return string.Equals(fileName, ResourceSettings.MANIFEST_NAME, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Path.GetExtension(path), ".bundle", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 执行 Channel Segment。
        /// </summary>
        /// <param name="channel">channel 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ChannelSegment(PublisherChannel channel)
        {
            return GameDeveloperKit.ResourceEditor.Build.Utilities.SanitizeSegment(channel.ChannelName, ResourcePublisherSettings.DeveloperChannelName);
        }

        /// <summary>
        /// 执行 Platform Segment。
        /// </summary>
        /// <param name="channel">channel 参数。</param>
        /// <returns>执行结果。</returns>
        private static string PlatformSegment(PublisherChannel channel)
        {
            return GameDeveloperKit.ResourceEditor.Build.Utilities.SanitizeSegment(channel.BuildTarget, "platform");
        }

        /// <summary>
        /// 执行 Version Segment。
        /// </summary>
        /// <param name="version">version 参数。</param>
        /// <returns>执行结果。</returns>
        private static string VersionSegment(string version)
        {
            return GameDeveloperKit.ResourceEditor.Build.Utilities.SanitizeSegment(version, "version");
        }

        /// <summary>
        /// 执行 Combine Remote Key。
        /// </summary>
        /// <param name="segments">segments 参数。</param>
        /// <returns>执行结果。</returns>
        private static string CombineRemoteKey(params string[] segments)
        {
            return string.Join("/", segments
                .Select(x => (x ?? string.Empty).Replace('\\', '/').Trim('/'))
                .Where(x => string.IsNullOrWhiteSpace(x) is false));
        }
    }
}

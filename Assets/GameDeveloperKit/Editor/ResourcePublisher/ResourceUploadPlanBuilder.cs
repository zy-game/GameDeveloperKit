using System;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using GameDeveloperKit.ResourceEditor;
using Newtonsoft.Json;

namespace GameDeveloperKit.ResourcePublisher
{
    public static class ResourceUploadPlanBuilder
    {
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
                    Hash = ResourceBuildUtilities.ComputeHash(path),
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

        public static string IndexKey(PublisherChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            return CombineRemoteKey(ChannelSegment(channel), PlatformSegment(channel), "publish.json");
        }

        public static string VersionPrefix(PublisherChannel channel, string version)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            return CombineRemoteKey(ChannelSegment(channel), PlatformSegment(channel), VersionSegment(version)) + "/";
        }

        public static string BuildRoot(PublisherChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            return CombineRemoteKey(ChannelSegment(channel), PlatformSegment(channel)) + "/";
        }

        private static string ResolveVersion(string manifestPath, string versionRoot)
        {
            var manifest = JsonConvert.DeserializeObject<ManifestInfo>(System.IO.File.ReadAllText(manifestPath));
            if (string.IsNullOrWhiteSpace(manifest?.Version) is false)
            {
                return manifest.Version;
            }

            return Path.GetFileName(versionRoot.TrimEnd('/', '\\'));
        }

        private static bool IsUploadFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return string.Equals(fileName, ResourceSettings.MANIFEST_NAME, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Path.GetExtension(path), ".bundle", StringComparison.OrdinalIgnoreCase);
        }

        private static string ChannelSegment(PublisherChannel channel)
        {
            return ResourceBuildUtilities.SanitizeSegment(channel.ChannelName, "dev");
        }

        private static string PlatformSegment(PublisherChannel channel)
        {
            return ResourceBuildUtilities.SanitizeSegment(channel.BuildTarget, "platform");
        }

        private static string VersionSegment(string version)
        {
            return ResourceBuildUtilities.SanitizeSegment(version, "version");
        }

        private static string CombineRemoteKey(params string[] segments)
        {
            return string.Join("/", segments
                .Select(x => (x ?? string.Empty).Replace('\\', '/').Trim('/'))
                .Where(x => string.IsNullOrWhiteSpace(x) is false));
        }
    }
}

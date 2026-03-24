using System;
using System.IO;

namespace GameDeveloperKit.Runtime
{
    public sealed class ResourcePackageContext
    {
        public ResourcePackageContext(ResourcePlayMode playMode, ResourcePackageDefinition definition)
        {
            PlayMode = playMode;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public ResourcePlayMode PlayMode { get; }

        public ResourcePackageDefinition Definition { get; }

        public string PackageName => Definition.PackageName;

        public ResourcePackageRole Role => Definition.Role;

        public string StreamingAssetsRoot => Definition.StreamingAssetsRoot;

        public string PersistentRoot => Definition.PersistentRoot;

        public string RemoteBaseUrl => Definition.RemoteBaseUrl;

        public string ManifestRelativePath => Definition.ManifestRelativePath;

        public ResourceUpdateReport LastUpdateReport { get; internal set; } = new ResourceUpdateReport
        {
            State = ResourceUpdateState.Idle
        };

        public string ResolveStreamingAssetsPath(string relativePath)
        {
            return Combine(StreamingAssetsRoot, relativePath);
        }

        public string ResolvePersistentPath(string relativePath)
        {
            return Combine(PersistentRoot, relativePath);
        }

        public string ResolveRemoteUrl(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(RemoteBaseUrl))
            {
                return relativePath ?? string.Empty;
            }

            return $"{RemoteBaseUrl.TrimEnd('/')}/{relativePath?.Replace('\\', '/').TrimStart('/')}";
        }

        private static string Combine(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return root ?? string.Empty;
            }

            if (Path.IsPathRooted(relativePath))
            {
                return relativePath;
            }

            return string.IsNullOrWhiteSpace(root) ? relativePath : Path.Combine(root, relativePath);
        }
    }
}

using System.IO;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public sealed class WebResourceRuntime : ResourceRuntimeBase
    {
        public override async UniTask EnsurePackageReadyAsync(ResourcePackageContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(context.ManifestRelativePath))
            {
                return;
            }

            var localManifestPath = context.ResolvePersistentPath(context.ManifestRelativePath);
            if (!File.Exists(localManifestPath))
            {
                var request = new DownloadRequest
                {
                    Urls = new[] { context.ResolveRemoteUrl(context.ManifestRelativePath) },
                    SavePath = localManifestPath
                };

                var result = await Game.Download.DownloadAsync(request, cancellationToken);
                if (result.Status != DownloadStatus.Succeeded)
                {
                    throw new IOException($"Failed to prepare web manifest for '{context.PackageName}': {result.ErrorMessage}");
                }
            }
        }

        protected override string ResolvePathByMode(ResourcePackageContext context, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return context.ResolveRemoteUrl(path);
        }

        protected override string ResolveManifestPath(ResourcePackageContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.ManifestRelativePath))
            {
                return string.Empty;
            }

            return context.ResolvePersistentPath(context.ManifestRelativePath);
        }

        protected override List<ResourceEntry> LoadManifestEntries(ResourcePackageContext context)
        {
            return base.LoadManifestEntries(context);
        }
    }
}

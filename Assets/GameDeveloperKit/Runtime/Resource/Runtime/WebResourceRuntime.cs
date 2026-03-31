using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// Web资源运行时，用于Web平台上的资源管理。
    /// </summary>
    public sealed class WebResourceRuntime : AssetBundleResourceRuntime
    {
        /// <summary>
        /// 异步确保Web资源包准备就绪。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        /// <exception cref="IOException">当下载清单失败时抛出。</exception>
        public override async UniTask EnsurePackageReadyAsync(ResourcePackageContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(context.ManifestRelativePath))
            {
                await base.EnsurePackageReadyAsync(context, cancellationToken);
                return;
            }

            var localManifestPath = context.ResolvePersistentPath(context.ManifestRelativePath);
            var tempManifestPath = $"{localManifestPath}.tmp";
            var request = new DownloadRequest
            {
                Urls = new[] { context.ResolveRemoteUrl(context.ManifestRelativePath) },
                SavePath = tempManifestPath
            };

            context.ResetUpdateReport(ResourceUpdateState.Checking, "Preparing", message: "Preparing web resource manifest.");
            var result = await Game.Download.DownloadAsync(request, cancellationToken);
            if (result.Status != DownloadStatus.Succeeded)
            {
                if (!File.Exists(localManifestPath))
                {
                    context.TransitionUpdateState(ResourceUpdateState.Failed, "Failed", result.ErrorMessage, result.Error ?? GameFrameworkException.Create("WebResourcePrepareFailed", result.ErrorMessage, "Resource", true, context.PackageName));
                    context.LastUpdateReport.ErrorMessage = result.ErrorMessage;
                    context.LastUpdateReport.FailureKind = string.IsNullOrWhiteSpace(result.FailureKind) ? "DownloadFailed" : result.FailureKind;
                    throw new IOException($"Failed to prepare web manifest for '{context.PackageName}': {result.ErrorMessage}");
                }

                return;
            }

            EnsureParentDirectory(localManifestPath);
            File.Copy(tempManifestPath, localManifestPath, true);
            DeleteFileIfExists(tempManifestPath);
            await DownloadPackageBundlesAsync(context, localManifestPath, cancellationToken);
            await base.EnsurePackageReadyAsync(context, cancellationToken);
            context.TransitionUpdateState(ResourceUpdateState.Completed, "Completed", "Web resource manifest prepared.");
        }

        /// <summary>
        /// 根据Web模式解析路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="path">要解析的路径。</param>
        /// <returns>解析后的远程URL。</returns>
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

            return context.ResolvePersistentPath(path);
        }

        /// <summary>
        /// 解析清单路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <returns>清单路径。</returns>
        protected override string ResolveManifestPath(ResourcePackageContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.ManifestRelativePath))
            {
                return string.Empty;
            }

            return context.ResolvePersistentPath(context.ManifestRelativePath);
        }

        /// <summary>
        /// 从清单加载资源条目。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <returns>资源条目列表。</returns>
        protected override List<ResourceEntry> LoadManifestEntries(ResourcePackageContext context)
        {
            return base.LoadManifestEntries(context);
        }

        private static async UniTask DownloadPackageBundlesAsync(ResourcePackageContext context, string localManifestPath, CancellationToken cancellationToken)
        {
            var manifest = ResourceManifestUtility.LoadFromFile(localManifestPath);
            var entries = ResourceManifestUtility.ResolveEntries(manifest, context?.PackageName);
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var requests = new List<DownloadRequest>();
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.BundleName))
                {
                    continue;
                }

                var bundleFile = entry.BundleName.Replace('\\', '/');
                if (!seen.Add(bundleFile))
                {
                    continue;
                }

                var relativePath = Path.Combine("bundles", bundleFile).Replace('\\', '/');
                requests.Add(new DownloadRequest
                {
                    Urls = new[] { context.ResolveRemoteUrl(relativePath) },
                    SavePath = context.ResolvePersistentPath(relativePath)
                });
            }

            if (requests.Count == 0)
            {
                return;
            }

            var result = await Game.Download.DownloadBatchAsync(requests, cancellationToken);
            if (result.Status != DownloadStatus.Succeeded)
            {
                throw new IOException($"Failed to prepare web bundles for '{context?.PackageName}': {result.ErrorMessage}");
            }
        }

        /// <summary>
        /// 确保父目录存在。
        /// </summary>
        /// <param name="path">文件路径。</param>
        private static void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// 如果文件存在则删除。
        /// </summary>
        /// <param name="path">文件路径。</param>
        private static void DeleteFileIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}




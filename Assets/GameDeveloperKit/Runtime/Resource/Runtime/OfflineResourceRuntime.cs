using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源运行时基类，用于离线模式的资源管理。
    /// </summary>
    public class OfflineResourceRuntime : AssetBundleResourceRuntime
    {
        /// <summary>
        /// 异步初始化资源包。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public override UniTask InitializePackageAsync(ResourcePackageContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyBuiltinToPersistent(context);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 将内置资源复制到持久化存储路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        private static void CopyBuiltinToPersistent(ResourcePackageContext context)
        {
            if (context == null || context.Role != ResourcePackageRole.Builtin)
            {
                return;
            }

            var streamingManifestPath = context.ResolveStreamingAssetsPath(context.ManifestRelativePath);
            var persistentManifestPath = context.ResolvePersistentPath(context.ManifestRelativePath);
            CopyFileIfExists(streamingManifestPath, persistentManifestPath);
            CopyBundleDirectory(context);

            var copiedFromManifest = CopyEntriesFromManifest(context, streamingManifestPath);
            if (copiedFromManifest)
            {
                return;
            }

            var entries = context.Definition?.Entries;
            if (entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    continue;
                }

                CopyFileIfExists(context.ResolveStreamingAssetsPath(entry.FullPath), context.ResolvePersistentPath(entry.FullPath));
            }
        }

        private static bool CopyEntriesFromManifest(ResourcePackageContext context, string streamingManifestPath)
        {
            var manifest = ResourceManifestUtility.LoadFromFile(streamingManifestPath);
            var entries = ResourceManifestUtility.ToEntries(manifest, context?.PackageName);
            if (entries == null || entries.Count == 0)
            {
                return false;
            }

            var copiedAny = false;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    continue;
                }

                CopyFileIfExists(context.ResolveStreamingAssetsPath(entry.FullPath), context.ResolvePersistentPath(entry.FullPath));
                if (!string.IsNullOrWhiteSpace(entry.BundleName))
                {
                    CopyFileIfExists(
                        context.ResolveStreamingAssetsPath(Path.Combine("bundles", entry.BundleName).Replace('\\', '/')),
                        context.ResolvePersistentPath(Path.Combine("bundles", entry.BundleName).Replace('\\', '/')));
                }
                copiedAny = true;
            }

            return copiedAny;
        }

        protected static void EnsureBuiltinResources(ResourcePackageContext context)
        {
            if (context == null || context.Role != ResourcePackageRole.Builtin)
            {
                return;
            }

            var streamingManifestPath = context.ResolveStreamingAssetsPath(context.ManifestRelativePath);
            var persistentManifestPath = context.ResolvePersistentPath(context.ManifestRelativePath);
            CopyFileIfExists(streamingManifestPath, persistentManifestPath);
            CopyBundleDirectory(context);

            var manifest = ResourceManifestUtility.LoadFromFile(persistentManifestPath);
            var entries = ResourceManifestUtility.ToEntries(manifest, context.PackageName);
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                var hasBundle = !string.IsNullOrWhiteSpace(entry.BundleName);
                var persistentBundlePath = hasBundle
                    ? context.ResolvePersistentPath(Path.Combine("bundles", entry.BundleName).Replace('\\', '/'))
                    : string.Empty;
                if (hasBundle && File.Exists(persistentBundlePath))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    continue;
                }

                var persistentPath = context.ResolvePersistentPath(entry.FullPath);
                if (File.Exists(persistentPath))
                {
                    continue;
                }

                CopyFileIfExists(context.ResolveStreamingAssetsPath(entry.FullPath), persistentPath);
                if (!string.IsNullOrWhiteSpace(entry.BundleName))
                {
                    if (!File.Exists(persistentBundlePath))
                    {
                        CopyFileIfExists(
                            context.ResolveStreamingAssetsPath(Path.Combine("bundles", entry.BundleName).Replace('\\', '/')),
                            persistentBundlePath);
                    }
                }
            }
        }

        private static void CopyBundleDirectory(ResourcePackageContext context)
        {
            if (context == null)
            {
                return;
            }

            var sourceDir = context.ResolveStreamingAssetsPath("bundles");
            var targetDir = context.ResolvePersistentPath("bundles");
            if (!Directory.Exists(sourceDir))
            {
                return;
            }

            Directory.CreateDirectory(targetDir);
            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                var relative = Path.GetRelativePath(sourceDir, files[i]);
                var target = Path.Combine(targetDir, relative);
                var dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(files[i], target, true);
            }
        }

        /// <summary>
        /// 如果源文件存在，则复制到目标路径。
        /// </summary>
        /// <param name="sourcePath">源文件路径。</param>
        /// <param name="targetPath">目标文件路径。</param>
        private static void CopyFileIfExists(string sourcePath, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(targetPath) || !File.Exists(sourcePath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourcePath, targetPath, true);
        }
    }
}

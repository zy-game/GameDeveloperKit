using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源运行时基类，用于离线模式的资源管理。
    /// </summary>
    public class OfflineResourceRuntime : ResourceRuntimeBase
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

            CopyFileIfExists(context.ResolveStreamingAssetsPath(context.ManifestRelativePath), context.ResolvePersistentPath(context.ManifestRelativePath));

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

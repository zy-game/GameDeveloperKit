using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public class OfflineResourceRuntime : ResourceRuntimeBase
    {
        public override UniTask InitializePackageAsync(ResourcePackageContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyBuiltinToPersistent(context);
            return UniTask.CompletedTask;
        }

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

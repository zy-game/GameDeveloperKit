using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.DesignImporter
{
    internal sealed class DesignImportPipeline
    {
        public async Task<DesignImportReport> ImportAsync(
            DesignDocument document,
            DesignImportOptions options,
            IProgress<DesignImportProgress> progress,
            CancellationToken cancellationToken)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var stopwatch = Stopwatch.StartNew();
            var outputRoot = DesignPathUtility.EnsureAssetsPath(options.OutputRoot);
            var pages = document.Pages.Where(x => x != null && x.Selected).ToArray();
            if (pages.Length == 0)
            {
                throw new InvalidOperationException("至少选择一个页面。" );
            }

            var assetsById = document.Assets
                .Where(x => x != null)
                .ToDictionary(x => x.Id, StringComparer.Ordinal);
            var references = CollectReferences(pages, assetsById);
            var downloads = await DownloadAssetsAsync(references, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var report = new DesignImportReport { DownloadedAssetCount = downloads.Count };
            var nodeAssetPaths = ImportSprites(
                outputRoot,
                pages,
                references,
                downloads,
                options,
                report,
                progress,
                cancellationToken);

            var pageNames = MakeUniquePageNames(pages);
            var previewFallbackPaths = ImportPreviewFallbacks(
                document.Source,
                outputRoot,
                pages,
                pageNames,
                assetsById,
                options,
                report,
                cancellationToken);
            for (var i = 0; i < pages.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = pages[i];
                var pageName = pageNames[page.Id];
                var prefabPath = $"{outputRoot}/Screens/{pageName}/{pageName}.prefab";
                progress?.Report(new DesignImportProgress(
                    0.75f + 0.25f * i / Mathf.Max(1, pages.Length),
                    $"正在生成 Prefab：{page.Name}"));
                report.PrefabPaths.Add(DesignPrefabBuilder.Build(
                    page,
                    prefabPath,
                    options,
                    nodeAssetPaths[page.Id],
                    previewFallbackPaths[page.Id]));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            stopwatch.Stop();
            report.Duration = stopwatch.Elapsed;
            progress?.Report(new DesignImportProgress(1f, $"已生成 {report.PrefabPaths.Count} 个 Prefab。"));
            return report;
        }

        private static List<NodeAssetReference> CollectReferences(
            IEnumerable<DesignPage> pages,
            IReadOnlyDictionary<string, DesignAsset> assetsById)
        {
            var result = new List<NodeAssetReference>();
            foreach (var page in pages)
            {
                foreach (var node in page.Root.DescendantsAndSelf())
                {
                    if (node.Kind != DesignNodeKind.Image || !node.Visible)
                    {
                        continue;
                    }

                    if (!assetsById.TryGetValue(node.AssetId, out var asset))
                    {
                        throw new InvalidOperationException($"节点 {node.Name} 引用了不存在的资源 {node.AssetId}。" );
                    }

                    result.Add(new NodeAssetReference(page, node, asset));
                }
            }

            return result;
        }

        private static async Task<Dictionary<string, DownloadedDesignAsset>> DownloadAssetsAsync(
            IReadOnlyCollection<NodeAssetReference> references,
            IProgress<DesignImportProgress> progress,
            CancellationToken cancellationToken)
        {
            var assets = references.Select(x => x.Asset).GroupBy(x => x.Id, StringComparer.Ordinal).Select(x => x.First()).ToArray();
            var result = new Dictionary<string, DownloadedDesignAsset>(StringComparer.Ordinal);
            using var client = new DesignAssetDownloadClient();
            for (var i = 0; i < assets.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var asset = assets[i];
                progress?.Report(new DesignImportProgress(
                    0.5f * i / Mathf.Max(1, assets.Length),
                    $"正在读取切图 {i + 1}/{assets.Length}：{asset.Name}"));
                result[asset.Id] = DesignAssetDownloadClient.ReadCached(asset)
                    ?? await client.DownloadAsync(asset, cancellationToken);
            }

            return result;
        }

        private static Dictionary<string, IReadOnlyDictionary<string, string>> ImportSprites(
            string outputRoot,
            IReadOnlyCollection<DesignPage> pages,
            IReadOnlyCollection<NodeAssetReference> references,
            IReadOnlyDictionary<string, DownloadedDesignAsset> downloads,
            DesignImportOptions options,
            DesignImportReport report,
            IProgress<DesignImportProgress> progress,
            CancellationToken cancellationToken)
        {
            var result = pages
                .Select(x => x.Id)
                .ToDictionary(
                    x => x,
                    _ => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            var mutable = result.ToDictionary(
                x => x.Key,
                x => (Dictionary<string, string>)x.Value,
                StringComparer.Ordinal);
            var pageNames = MakeUniquePageNames(pages);
            var groups = references.GroupBy(reference => AssetVariantKey.Create(
                reference,
                downloads[reference.Asset.Id],
                options.ExtractSharedAssets ? string.Empty : reference.Page.Id));
            var variants = groups.ToArray();

            for (var i = 0; i < variants.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var group = variants[i].ToArray();
                var first = group[0];
                var download = downloads[first.Asset.Id];
                var pageCount = group.Select(x => x.Page.Id).Distinct(StringComparer.Ordinal).Count();
                var shared = options.ExtractSharedAssets &&
                             (pageCount > 1 || group.Any(x => x.Asset.Shared || x.Node.Shared));
                var folder = shared
                    ? $"{outputRoot}/Common"
                    : $"{outputRoot}/Screens/{pageNames[first.Page.Id]}/Sprites";
                EnsureFolder(folder);
                var suffix = variants[i].Key.FileSuffix();
                var assetName = DesignPathUtility.SanitizeFileName(first.Asset.Name, "Sprite");
                var assetPath = $"{folder}/{assetName}_{suffix}.{download.Extension}";
                progress?.Report(new DesignImportProgress(
                    0.5f + 0.25f * i / Mathf.Max(1, variants.Length),
                    $"正在导入 Sprite {i + 1}/{variants.Length}：{first.Asset.Name}"));

                var absolutePath = ToAbsoluteAssetPath(assetPath);
                var existed = System.IO.File.Exists(absolutePath);
                if (!existed || !System.IO.File.ReadAllBytes(absolutePath).SequenceEqual(download.Bytes))
                {
                    System.IO.File.WriteAllBytes(absolutePath, download.Bytes);
                }
                else
                {
                    report.ReusedAssetCount++;
                }

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureTexture(assetPath, first, options.MaxTextureSize);
                report.AssetPaths.Add(assetPath);
                if (shared)
                {
                    report.SharedAssetCount++;
                }

                foreach (var reference in group)
                {
                    mutable[reference.Page.Id][reference.Node.Id] = assetPath;
                }
            }

            return mutable.ToDictionary(
                x => x.Key,
                x => (IReadOnlyDictionary<string, string>)x.Value,
                StringComparer.Ordinal);
        }

        private static void ConfigureTexture(string assetPath, NodeAssetReference reference, int maxTextureSize)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter
                ?? throw new InvalidOperationException("无法获取 TextureImporter：" + assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.Clamp(Mathf.ClosestPowerOfTwo(maxTextureSize), 32, 8192);
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = reference.Node.Border.ToVector4(reference.Asset.PixelScale);
            importer.SaveAndReimport();
        }

        private static Dictionary<string, string> ImportPreviewFallbacks(
            DesignSourceKind source,
            string outputRoot,
            IReadOnlyCollection<DesignPage> pages,
            IReadOnlyDictionary<string, string> pageNames,
            IReadOnlyDictionary<string, DesignAsset> assetsById,
            DesignImportOptions options,
            DesignImportReport report,
            CancellationToken cancellationToken)
        {
            var result = pages.ToDictionary(page => page.Id, _ => string.Empty, StringComparer.Ordinal);
            if (source != DesignSourceKind.Lanhu)
            {
                return result;
            }

            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HasFullPageVisual(page) ||
                    string.IsNullOrWhiteSpace(page.CachedPreviewPath) ||
                    !System.IO.File.Exists(page.CachedPreviewPath))
                {
                    continue;
                }

                var extension = Path.GetExtension(page.CachedPreviewPath).TrimStart('.').ToLowerInvariant();
                if (extension != "png" && extension != "jpg" && extension != "jpeg" && extension != "webp")
                {
                    extension = "png";
                }

                var folder = $"{outputRoot}/Screens/{pageNames[page.Id]}/Sprites";
                EnsureFolder(folder);
                var assetPath = $"{folder}/__PreviewFallback.{extension}";
                var absolutePath = ToAbsoluteAssetPath(assetPath);
                byte[] bytes;
                try
                {
                    bytes = DesignPreviewFallbackComposer.Compose(page, assetsById);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogWarning($"页面 {page.Name} 无法生成残差背景，已使用完整预览兜底：{exception.Message}");
                    bytes = System.IO.File.ReadAllBytes(page.CachedPreviewPath);
                }
                if (!System.IO.File.Exists(absolutePath) ||
                    !System.IO.File.ReadAllBytes(absolutePath).SequenceEqual(bytes))
                {
                    System.IO.File.WriteAllBytes(absolutePath, bytes);
                }
                else
                {
                    report.ReusedAssetCount++;
                }

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigurePreviewTexture(assetPath, options.MaxTextureSize);
                report.AssetPaths.Add(assetPath);
                result[page.Id] = assetPath;
            }

            return result;
        }

        private static bool HasFullPageVisual(DesignPage page)
        {
            return HasFullPageVisual(page.Root, 0f, 0f, page.Width, page.Height);
        }

        private static bool HasFullPageVisual(
            DesignNode node,
            float parentX,
            float parentY,
            float pageWidth,
            float pageHeight)
        {
            if (node == null || !node.Visible || node.Opacity <= 0f)
            {
                return false;
            }

            var x = parentX + node.X;
            var y = parentY + node.Y;
            var toleranceX = pageWidth * 0.05f;
            var toleranceY = pageHeight * 0.05f;
            var coversPage = x <= toleranceX && y <= toleranceY &&
                             x + node.Width >= pageWidth - toleranceX &&
                             y + node.Height >= pageHeight - toleranceY;
            if (coversPage &&
                (node.Kind == DesignNodeKind.Image || !string.IsNullOrWhiteSpace(node.BackgroundColor)))
            {
                return true;
            }

            return node.Children.Any(child =>
                HasFullPageVisual(child, x, y, pageWidth, pageHeight));
        }

        private static void ConfigurePreviewTexture(string assetPath, int maxTextureSize)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter
                ?? throw new InvalidOperationException("无法获取背景兜底图 TextureImporter：" + assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.Clamp(Mathf.ClosestPowerOfTwo(maxTextureSize), 32, 8192);
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = Vector4.zero;
            importer.SaveAndReimport();
        }

        private static Dictionary<string, string> MakeUniquePageNames(IEnumerable<DesignPage> pages)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var page in pages)
            {
                if (result.ContainsKey(page.Id))
                {
                    continue;
                }

                var baseName = DesignPathUtility.SanitizeFileName(page.Name, "Screen");
                var candidate = baseName;
                var index = 2;
                while (!used.Add(candidate))
                {
                    candidate = baseName + "_" + index++;
                }

                result[page.Id] = candidate;
            }

            return result;
        }

        private static void EnsureFolder(string assetPath)
        {
            var segments = assetPath.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("无法确定 Unity 项目根目录。" );
            }

            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private sealed class NodeAssetReference
        {
            public NodeAssetReference(DesignPage page, DesignNode node, DesignAsset asset)
            {
                Page = page;
                Node = node;
                Asset = asset;
            }

            public DesignPage Page { get; }
            public DesignNode Node { get; }
            public DesignAsset Asset { get; }
        }

        private readonly struct AssetVariantKey : IEquatable<AssetVariantKey>
        {
            private AssetVariantKey(string hash, string border, string scope)
            {
                Hash = hash;
                Border = border;
                Scope = scope;
            }

            private string Hash { get; }
            private string Border { get; }
            private string Scope { get; }

            public static AssetVariantKey Create(
                NodeAssetReference reference,
                DownloadedDesignAsset download,
                string scope)
            {
                var border = reference.Node.NineSlice || reference.Node.Border.HasValue
                    ? $"{reference.Node.Border.Left:0.###},{reference.Node.Border.Bottom:0.###}," +
                      $"{reference.Node.Border.Right:0.###},{reference.Node.Border.Top:0.###}@{reference.Asset.PixelScale:0.###}"
                    : "0,0,0,0";
                return new AssetVariantKey(download.Hash, border, scope ?? string.Empty);
            }

            public bool Equals(AssetVariantKey other)
            {
                return string.Equals(Hash, other.Hash, StringComparison.Ordinal) &&
                       string.Equals(Border, other.Border, StringComparison.Ordinal) &&
                       string.Equals(Scope, other.Scope, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is AssetVariantKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Hash != null ? StringComparer.Ordinal.GetHashCode(Hash) : 0;
                    hashCode = (hashCode * 397) ^ (Border != null ? StringComparer.Ordinal.GetHashCode(Border) : 0);
                    return (hashCode * 397) ^ (Scope != null ? StringComparer.Ordinal.GetHashCode(Scope) : 0);
                }
            }

            public string FileSuffix()
            {
                if (string.Equals(Border, "0,0,0,0", StringComparison.Ordinal))
                {
                    return Hash.Substring(0, 10);
                }

                unchecked
                {
                    uint borderHash = 2166136261;
                    foreach (var character in Border)
                    {
                        borderHash ^= character;
                        borderHash *= 16777619;
                    }

                    return Hash.Substring(0, 10) + "_b" + borderHash.ToString("x8");
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using IOFile = System.IO.File;

namespace GameDeveloperKit.DesignImporter
{
    internal sealed class DesignSyncSnapshot
    {
        public DesignDocument Document;
        public DesignVersionDiffResult Diff;
        public string ProjectCacheRoot = string.Empty;
        public string Revision = string.Empty;
        public string PreviousRevision = string.Empty;
        public DateTime SyncedAtUtc;
    }

    [Serializable]
    internal sealed class DesignCacheProjectState
    {
        public string ProjectId = string.Empty;
        public string TeamId = string.Empty;
        public string ProjectName = string.Empty;
        public string SourceUrl = string.Empty;
        public string LatestRevision = string.Empty;
        public string LatestManifest = string.Empty;
        public string PreviousRevision = string.Empty;
        public string PreviousManifest = string.Empty;
        public string SyncedAtUtc = string.Empty;
    }

    [Serializable]
    internal sealed class DesignAssetCacheIndex
    {
        public List<DesignAssetCacheEntry> Entries = new List<DesignAssetCacheEntry>();
    }

    [Serializable]
    internal sealed class DesignAssetCacheEntry
    {
        public string Url = string.Empty;
        public string Hash = string.Empty;
        public string Extension = string.Empty;
        public string RelativePath = string.Empty;
    }

    internal sealed class DesignCacheStore
    {
        public static string CacheRoot
        {
            get
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                    ?? throw new InvalidOperationException("无法确定 Unity 项目目录。");
                return Path.Combine(projectRoot, "Library", "GameDeveloperKit", "DesignCache", "Lanhu");
            }
        }

        public async Task<DesignSyncSnapshot> SaveSyncAsync(
            DesignDocument document,
            string sourceUrl,
            IProgress<DesignImportProgress> progress,
            CancellationToken cancellationToken)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            document.Normalize();
            var address = LanhuProjectAddress.Parse(sourceUrl);
            if (string.IsNullOrWhiteSpace(document.Id))
            {
                document.Id = address.ProjectId;
            }

            document.TeamId = address.TeamId;
            document.SourceLocation = sourceUrl;
            var root = ProjectRoot(address.ProjectId);
            Directory.CreateDirectory(root);
            var oldState = ReadState(root);
            var previous = ReadManifest(root, oldState?.LatestManifest);
            var diff = DesignVersionDiff.Compare(previous, document);
            var revision = diff.CurrentRevision;
            var relativeManifest = Path.Combine("versions", revision, "manifest.json");
            var manifestPath = Path.Combine(root, relativeManifest);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? root);
            if (!IOFile.Exists(manifestPath))
            {
                IOFile.WriteAllText(manifestPath, DesignManifestCodec.Serialize(document));
            }

            progress?.Report(new DesignImportProgress(0.05f, "正在缓存蓝湖切图..."));
            await CacheAssetsAsync(root, document, progress, cancellationToken);
            await CachePreviewsAsync(root, document, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var changedRevision = oldState == null ||
                                  !string.Equals(oldState.LatestRevision, revision, StringComparison.Ordinal);
            var state = new DesignCacheProjectState
            {
                ProjectId = address.ProjectId,
                TeamId = address.TeamId,
                ProjectName = document.Name,
                SourceUrl = sourceUrl,
                LatestRevision = revision,
                LatestManifest = relativeManifest,
                PreviousRevision = changedRevision ? oldState?.LatestRevision ?? string.Empty : oldState?.PreviousRevision ?? string.Empty,
                PreviousManifest = changedRevision ? oldState?.LatestManifest ?? string.Empty : oldState?.PreviousManifest ?? string.Empty,
                SyncedAtUtc = DateTime.UtcNow.ToString("O")
            };
            IOFile.WriteAllText(StatePath(root), JsonConvert.SerializeObject(state, Formatting.Indented));
            ApplyMappings(root, document);
            return CreateSnapshot(document, diff, root, state);
        }

        public DesignSyncSnapshot LoadLatest(string sourceUrl)
        {
            var address = LanhuProjectAddress.Parse(sourceUrl);
            var root = ProjectRoot(address.ProjectId);
            var state = ReadState(root) ?? throw new FileNotFoundException("该蓝湖项目还没有本地同步缓存。");
            var document = ReadManifest(root, state.LatestManifest)
                ?? throw new FileNotFoundException("蓝湖最新缓存清单不存在。");
            var previous = ReadManifest(root, state.PreviousManifest);
            HydrateAssets(root, document);
            ApplyMappings(root, document);
            return CreateSnapshot(document, DesignVersionDiff.Compare(previous, document), root, state);
        }

        public static string ProjectRoot(string projectId)
        {
            return Path.Combine(CacheRoot, SafeSegment(projectId));
        }

        private static DesignSyncSnapshot CreateSnapshot(
            DesignDocument document,
            DesignVersionDiffResult diff,
            string root,
            DesignCacheProjectState state)
        {
            DateTime.TryParse(state.SyncedAtUtc, out var syncedAt);
            return new DesignSyncSnapshot
            {
                Document = document,
                Diff = diff,
                ProjectCacheRoot = root,
                Revision = state.LatestRevision,
                PreviousRevision = state.PreviousRevision,
                SyncedAtUtc = syncedAt.ToUniversalTime()
            };
        }

        private static async Task CacheAssetsAsync(
            string root,
            DesignDocument document,
            IProgress<DesignImportProgress> progress,
            CancellationToken cancellationToken)
        {
            var index = ReadAssetIndex(root);
            var entries = index.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Url))
                .GroupBy(entry => entry.Url, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
            var items = document.Assets.Where(asset => asset != null && !string.IsNullOrWhiteSpace(asset.Url)).ToArray();
            var gate = new object();
            var completed = 0;
            using var client = new DesignAssetDownloadClient();
            using var limiter = new SemaphoreSlim(8, 8);
            var tasks = items.Select(async asset =>
            {
                await limiter.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DesignAssetCacheEntry cached;
                    lock (gate)
                    {
                        entries.TryGetValue(asset.Url, out cached);
                    }

                    if (cached != null)
                    {
                        var path = Path.Combine(root, cached.RelativePath);
                        if (IOFile.Exists(path))
                        {
                            asset.CachedFilePath = path;
                            asset.CachedHash = cached.Hash;
                            return;
                        }
                    }

                    var download = await client.DownloadAsync(asset, cancellationToken);
                    var relative = Path.Combine("assets", download.Hash + "." + download.Extension);
                    var absolute = Path.Combine(root, relative);
                    lock (gate)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? root);
                        if (!IOFile.Exists(absolute))
                        {
                            IOFile.WriteAllBytes(absolute, download.Bytes);
                        }

                        entries[asset.Url] = new DesignAssetCacheEntry
                        {
                            Url = asset.Url,
                            Hash = download.Hash,
                            Extension = download.Extension,
                            RelativePath = relative
                        };
                    }

                    asset.CachedFilePath = absolute;
                    asset.CachedHash = download.Hash;
                }
                finally
                {
                    limiter.Release();
                    var count = Interlocked.Increment(ref completed);
                    progress?.Report(new DesignImportProgress(
                        0.05f + 0.9f * count / Math.Max(1, items.Length),
                        $"正在缓存切图 {count}/{items.Length}"));
                }
            }).ToArray();
            await Task.WhenAll(tasks);
            index.Entries = entries.Values.OrderBy(entry => entry.Url, StringComparer.Ordinal).ToList();
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            IOFile.WriteAllText(AssetIndexPath(root), JsonConvert.SerializeObject(index, Formatting.Indented));
        }

        private static void HydrateAssets(string root, DesignDocument document)
        {
            var index = ReadAssetIndex(root).Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Url))
                .GroupBy(entry => entry.Url, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
            foreach (var asset in document.Assets.Where(asset => asset != null))
            {
                if (!index.TryGetValue(asset.Url, out var entry))
                {
                    continue;
                }

                var path = Path.Combine(root, entry.RelativePath);
                if (IOFile.Exists(path))
                {
                    asset.CachedFilePath = path;
                    asset.CachedHash = entry.Hash;
                }
            }

            var previewIndex = ReadPreviewIndex(root).Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Url))
                .GroupBy(entry => entry.Url, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
            var previewRoot = Path.Combine(root, "previews");
            foreach (var page in document.Pages.Where(page => page != null))
            {
                if (previewIndex.TryGetValue(page.PreviewUrl ?? string.Empty, out var entry))
                {
                    var path = Path.Combine(root, entry.RelativePath);
                    if (IOFile.Exists(path))
                    {
                        page.CachedPreviewPath = path;
                        continue;
                    }
                }

                if (Directory.Exists(previewRoot))
                {
                    page.CachedPreviewPath = Directory
                        .GetFiles(previewRoot, SafeSegment(page.Id) + ".*")
                        .FirstOrDefault() ?? string.Empty;
                }
            }
        }

        private static async Task CachePreviewsAsync(
            string root,
            DesignDocument document,
            IProgress<DesignImportProgress> progress,
            CancellationToken cancellationToken)
        {
            var index = ReadPreviewIndex(root);
            var entries = index.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Url))
                .GroupBy(entry => entry.Url, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
            var pages = document.Pages
                .Where(page => page != null && !string.IsNullOrWhiteSpace(page.PreviewUrl))
                .ToArray();
            var gate = new object();
            var completed = 0;
            using var client = new DesignAssetDownloadClient();
            using var limiter = new SemaphoreSlim(4, 4);
            var tasks = pages.Select(async page =>
            {
                await limiter.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DesignAssetCacheEntry cached;
                    lock (gate)
                    {
                        entries.TryGetValue(page.PreviewUrl, out cached);
                    }

                    if (cached != null)
                    {
                        var cachedPath = Path.Combine(root, cached.RelativePath);
                        if (IOFile.Exists(cachedPath))
                        {
                            page.CachedPreviewPath = cachedPath;
                            return;
                        }
                    }

                    var source = new DesignAsset
                    {
                        Id = page.Id,
                        Name = page.Name,
                        Url = page.PreviewUrl,
                        Format = "png"
                    };
                    var download = await client.DownloadAsync(source, cancellationToken);
                    var relative = Path.Combine("previews", download.Hash + "." + download.Extension);
                    var absolute = Path.Combine(root, relative);
                    lock (gate)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? root);
                        if (!IOFile.Exists(absolute))
                        {
                            IOFile.WriteAllBytes(absolute, download.Bytes);
                        }

                        entries[page.PreviewUrl] = new DesignAssetCacheEntry
                        {
                            Url = page.PreviewUrl,
                            Hash = download.Hash,
                            Extension = download.Extension,
                            RelativePath = relative
                        };
                    }

                    page.CachedPreviewPath = absolute;
                }
                finally
                {
                    limiter.Release();
                    var count = Interlocked.Increment(ref completed);
                    progress?.Report(new DesignImportProgress(
                        0.95f + 0.05f * count / Math.Max(1, pages.Length),
                        $"正在缓存设计稿预览 {count}/{pages.Length}"));
                }
            }).ToArray();
            await Task.WhenAll(tasks);
            index.Entries = entries.Values.OrderBy(entry => entry.Url, StringComparer.Ordinal).ToList();
            Directory.CreateDirectory(Path.Combine(root, "previews"));
            IOFile.WriteAllText(PreviewIndexPath(root), JsonConvert.SerializeObject(index, Formatting.Indented));
        }

        private static void ApplyMappings(string root, DesignDocument document)
        {
            foreach (var page in document.Pages.Where(page => page != null))
            {
                DesignMappingStore.Apply(root, page);
            }
        }

        private static DesignCacheProjectState ReadState(string root)
        {
            var path = StatePath(root);
            return IOFile.Exists(path)
                ? JsonConvert.DeserializeObject<DesignCacheProjectState>(IOFile.ReadAllText(path))
                : null;
        }

        private static DesignDocument ReadManifest(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            var path = Path.Combine(root, relativePath);
            return IOFile.Exists(path) ? DesignManifestCodec.ReadFile(path) : null;
        }

        private static DesignAssetCacheIndex ReadAssetIndex(string root)
        {
            var path = AssetIndexPath(root);
            return IOFile.Exists(path)
                ? JsonConvert.DeserializeObject<DesignAssetCacheIndex>(IOFile.ReadAllText(path)) ?? new DesignAssetCacheIndex()
                : new DesignAssetCacheIndex();
        }

        private static DesignAssetCacheIndex ReadPreviewIndex(string root)
        {
            var path = PreviewIndexPath(root);
            return IOFile.Exists(path)
                ? JsonConvert.DeserializeObject<DesignAssetCacheIndex>(IOFile.ReadAllText(path)) ?? new DesignAssetCacheIndex()
                : new DesignAssetCacheIndex();
        }

        private static string StatePath(string root) => Path.Combine(root, "project.json");
        private static string AssetIndexPath(string root) => Path.Combine(root, "assets", "index.json");
        private static string PreviewIndexPath(string root) => Path.Combine(root, "previews", "index.json");

        private static string SafeSegment(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string((value ?? "unknown").Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class HlsCatalogCommitResult
    {
        public HlsCatalogCommitResult(HlsCatalogDocument document, CatalogItem item)
        {
            Document = document;
            Item = item;
        }

        public HlsCatalogDocument Document { get; }

        public CatalogItem Item { get; }
    }

    internal sealed class HlsCatalogRemoveResult
    {
        public HlsCatalogRemoveResult(HlsCatalogDocument document, CatalogItem removedItem)
        {
            Document = document;
            RemovedItem = removedItem;
        }

        public HlsCatalogDocument Document { get; }

        public CatalogItem RemovedItem { get; }
    }

    internal sealed class HlsCatalogOriginRepository
    {
        private const int MaximumCommitAttempts = 5;
        private readonly CloudService m_CloudService;
        private readonly Func<CloudProjectConfig> m_CloudConfigProvider;
        private readonly Func<string> m_CdnBaseUrlProvider;
        private readonly Func<DateTimeOffset> m_UtcNow;

        public HlsCatalogOriginRepository()
            : this(
                CloudService.Shared,
                () => EditorGlobalConfig.LoadOrCreate().Cloud,
                () => EditorGlobalConfig.LoadOrCreate().StoryMedia.CdnBaseUrl,
                () => DateTimeOffset.UtcNow)
        {
        }

        internal HlsCatalogOriginRepository(
            CloudService cloudService,
            Func<CloudProjectConfig> cloudConfigProvider,
            Func<string> cdnBaseUrlProvider,
            Func<DateTimeOffset> utcNow)
        {
            m_CloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
            m_CloudConfigProvider = cloudConfigProvider ?? throw new ArgumentNullException(nameof(cloudConfigProvider));
            m_CdnBaseUrlProvider = cdnBaseUrlProvider ?? throw new ArgumentNullException(nameof(cdnBaseUrlProvider));
            m_UtcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public async UniTask<HlsCatalogCommitResult> UpsertAsync(
            CatalogItem proposed,
            bool overwrite,
            DateTimeOffset? expectedUpdatedAtUtc,
            CancellationToken cancellationToken)
        {
            if (proposed == null)
            {
                throw new ArgumentNullException(nameof(proposed));
            }

            for (var attempt = 1; attempt <= MaximumCommitAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var origin = await LoadOriginAsync(cancellationToken);
                var items = new List<CatalogItem>(origin.Document.Items);
                ValidateMutation(items, proposed, overwrite, expectedUpdatedAtUtc);
                var existingIndex = items.FindIndex(item =>
                    string.Equals(item.MediaId, proposed.MediaId, StringComparison.Ordinal));
                if (existingIndex >= 0)
                {
                    items[existingIndex] = proposed;
                }
                else
                {
                    items.Add(proposed);
                }

                var document = new HlsCatalogDocument(
                    HlsCatalogCodec.SchemaVersion,
                    origin.Document.Generation + 1,
                    m_UtcNow(),
                    items);
                try
                {
                    await UploadAsync(document, origin.ETag, cancellationToken);
                    return new HlsCatalogCommitResult(document, proposed);
                }
                catch (CloudException exception) when (
                    exception.Kind == CloudFailureKind.PreconditionFailed)
                {
                    Debug.LogWarning(
                        $"Catalog 在第 {attempt} 次提交时发生并发修改，正在重新读取并合并：{exception.Message}");
                }
            }

            throw new CatalogException(
                CatalogErrorKind.Conflict,
                "Catalog was modified repeatedly; refresh and retry the operation.");
        }

        public async UniTask<HlsCatalogCommitResult> RenameAsync(
            string mediaId,
            DateTimeOffset? expectedUpdatedAtUtc,
            string newName,
            string editor,
            CancellationToken cancellationToken)
        {
            var normalizedName = newName?.Trim() ?? string.Empty;
            if (normalizedName.Length == 0 || normalizedName.Length > 200)
            {
                throw new CatalogException(
                    CatalogErrorKind.InvalidResponse,
                    "Catalog display name must contain 1 to 200 characters.");
            }

            for (var attempt = 1; attempt <= MaximumCommitAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var origin = await LoadOriginAsync(cancellationToken);
                var items = new List<CatalogItem>(origin.Document.Items);
                var index = FindExpectedItem(items, mediaId, expectedUpdatedAtUtc);
                var existing = items[index];
                var renamed = CopyWithName(
                    existing,
                    normalizedName,
                    editor,
                    m_UtcNow());
                items[index] = renamed;
                var document = NextDocument(origin.Document, items);
                try
                {
                    await UploadAsync(document, origin.ETag, cancellationToken);
                    return new HlsCatalogCommitResult(document, renamed);
                }
                catch (CloudException exception) when (
                    exception.Kind == CloudFailureKind.PreconditionFailed)
                {
                    Debug.LogWarning(
                        $"Catalog 重命名在第 {attempt} 次提交时发生并发修改，正在重新读取并合并：{exception.Message}");
                }
            }

            throw RepeatedConflict();
        }

        public async UniTask<HlsCatalogRemoveResult> RemoveAsync(
            string mediaId,
            DateTimeOffset? expectedUpdatedAtUtc,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= MaximumCommitAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var origin = await LoadOriginAsync(cancellationToken);
                var items = new List<CatalogItem>(origin.Document.Items);
                var index = FindExpectedItem(items, mediaId, expectedUpdatedAtUtc);
                var removed = items[index];
                items.RemoveAt(index);
                var document = NextDocument(origin.Document, items);
                try
                {
                    await UploadAsync(document, origin.ETag, cancellationToken);
                    return new HlsCatalogRemoveResult(document, removed);
                }
                catch (CloudException exception) when (
                    exception.Kind == CloudFailureKind.PreconditionFailed)
                {
                    Debug.LogWarning(
                        $"Catalog 删除在第 {attempt} 次提交时发生并发修改，正在重新读取并合并：{exception.Message}");
                }
            }

            throw RepeatedConflict();
        }

        internal async UniTask<OriginSnapshot> LoadOriginAsync(CancellationToken cancellationToken)
        {
            var objectKey = CatalogObjectKey();
            try
            {
                var result = await m_CloudService.GetObjectAsync(
                    new CloudObjectGetRequest(objectKey),
                    cancellationToken);
                return new OriginSnapshot(
                    HlsCatalogCodec.ParseDocument(
                        result.Content,
                        m_CdnBaseUrlProvider(),
                        true),
                    result.ETag);
            }
            catch (CloudException exception) when (exception.Kind == CloudFailureKind.NotFound)
            {
                return new OriginSnapshot(
                    new HlsCatalogDocument(
                        HlsCatalogCodec.SchemaVersion,
                        0,
                        null,
                        Array.Empty<CatalogItem>()),
                    string.Empty);
            }
        }

        private async UniTask UploadAsync(
            HlsCatalogDocument document,
            string expectedETag,
            CancellationToken cancellationToken)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "gdk-hls-catalog-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                System.IO.File.WriteAllText(
                    path,
                    HlsCatalogCodec.SerializeDocument(document),
                    new UTF8Encoding(false));
                var condition = string.IsNullOrWhiteSpace(expectedETag)
                    ? CloudWriteCondition.IfAbsent
                    : CloudWriteCondition.IfMatch(expectedETag);
                await m_CloudService.UploadObjectAsync(
                    new CloudObjectUploadRequest(
                        path,
                        CatalogObjectKey(),
                        "application/json",
                        condition),
                    null,
                    cancellationToken);
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }
                }
                catch (IOException exception)
                {
                    Debug.LogWarning($"Catalog 临时文件清理失败：{exception.Message}");
                }
                catch (UnauthorizedAccessException exception)
                {
                    Debug.LogWarning($"Catalog 临时文件清理失败：{exception.Message}");
                }
            }
        }

        private void ValidateMutation(
            IReadOnlyList<CatalogItem> items,
            CatalogItem proposed,
            bool overwrite,
            DateTimeOffset? expectedUpdatedAtUtc)
        {
            var duplicate = items.FirstOrDefault(item =>
                string.IsNullOrWhiteSpace(proposed.SourceSha256) is false &&
                string.Equals(item.SourceSha256, proposed.SourceSha256, StringComparison.Ordinal));
            var existing = items.FirstOrDefault(item =>
                string.Equals(item.MediaId, proposed.MediaId, StringComparison.Ordinal));
            if (overwrite is false)
            {
                if (duplicate != null)
                {
                    throw new CatalogException(
                        CatalogErrorKind.DuplicateSource,
                        $"Source video already exists. mediaId:{duplicate.MediaId}");
                }

                if (existing != null)
                {
                    throw new CatalogException(
                        CatalogErrorKind.Conflict,
                        $"Catalog mediaId already exists. mediaId:{proposed.MediaId}");
                }

                return;
            }

            if (existing == null ||
                string.Equals(existing.SourceSha256, proposed.SourceSha256, StringComparison.Ordinal) is false ||
                expectedUpdatedAtUtc.HasValue && existing.UpdatedAtUtc != expectedUpdatedAtUtc)
            {
                throw new CatalogException(
                    CatalogErrorKind.ItemChanged,
                    $"Catalog item changed before overwrite. mediaId:{proposed.MediaId}");
            }

            if (duplicate != null &&
                string.Equals(duplicate.MediaId, proposed.MediaId, StringComparison.Ordinal) is false)
            {
                throw new CatalogException(
                    CatalogErrorKind.DuplicateSource,
                    $"Source video belongs to another Catalog item. mediaId:{duplicate.MediaId}");
            }
        }

        private HlsCatalogDocument NextDocument(
            HlsCatalogDocument current,
            IReadOnlyList<CatalogItem> items)
        {
            return new HlsCatalogDocument(
                HlsCatalogCodec.SchemaVersion,
                current.Generation + 1,
                m_UtcNow(),
                items);
        }

        private static int FindExpectedItem(
            IReadOnlyList<CatalogItem> items,
            string mediaId,
            DateTimeOffset? expectedUpdatedAtUtc)
        {
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                if (string.Equals(item.MediaId, mediaId, StringComparison.Ordinal) is false)
                {
                    continue;
                }

                if (item.UpdatedAtUtc != expectedUpdatedAtUtc)
                {
                    break;
                }

                return index;
            }

            throw new CatalogException(
                CatalogErrorKind.ItemChanged,
                $"Catalog item changed before the operation. mediaId:{mediaId}");
        }

        private static CatalogItem CopyWithName(
            CatalogItem item,
            string name,
            string editor,
            DateTimeOffset updatedAtUtc)
        {
            return new CatalogItem(
                item.MediaId,
                name,
                item.Kind,
                item.Location,
                item.Format,
                item.ThumbnailLocation,
                item.Width,
                item.Height,
                item.Bitrate,
                item.DurationMs,
                item.Renditions,
                item.SourceFileName,
                item.SourceSha256,
                string.IsNullOrWhiteSpace(editor) ? Environment.UserName : editor.Trim(),
                item.CreatedAtUtc,
                updatedAtUtc,
                item.ObjectPrefix);
        }

        private static CatalogException RepeatedConflict()
        {
            return new CatalogException(
                CatalogErrorKind.Conflict,
                "Catalog was modified repeatedly; refresh and retry the operation.");
        }

        private string CatalogObjectKey()
        {
            var rootPrefix = m_CloudConfigProvider()?.RootPrefix?.Trim().Trim('/') ?? string.Empty;
            return rootPrefix.Length == 0 ? "catalog.json" : rootPrefix + "/catalog.json";
        }

        internal sealed class OriginSnapshot
        {
            public OriginSnapshot(HlsCatalogDocument document, string etag)
            {
                Document = document;
                ETag = etag ?? string.Empty;
            }

            public HlsCatalogDocument Document { get; }

            public string ETag { get; }
        }
    }
}

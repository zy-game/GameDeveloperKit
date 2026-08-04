using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.StoryEditor.Media;
using NUnit.Framework;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests.Story
{
    public sealed class HlsCatalogManagementTests
    {
        private string m_TempDirectory;
        private CloudProjectConfig m_Config;
        private CatalogStoreTransport m_Transport;
        private HlsCatalogOriginRepository m_Repository;
        private DateTimeOffset m_Now;

        [SetUp]
        public void SetUp()
        {
            m_TempDirectory = Path.Combine(
                Path.GetTempPath(),
                "gdk-catalog-management-" + Guid.NewGuid().ToString("N"));
            IODirectory.CreateDirectory(m_TempDirectory);
            m_Config = new CloudProjectConfig
            {
                ProviderId = CloudProviderId.TencentCos,
                CredentialProfileName = "publisher",
                Bucket = "bucket-1250000000",
                Region = "ap-chengdu",
                RootPrefix = "videos"
            };
            var credentialStore = new CloudCredentialStore(
                Path.Combine(m_TempDirectory, "credentials.json"));
            credentialStore.Save(
                CloudProviderId.TencentCos,
                "publisher",
                new CloudCredential("access", "secret"));
            var original = CreateItem(
                "media-a",
                "Original",
                DateTimeOffset.Parse("2026-07-27T01:00:00Z"),
                DateTimeOffset.Parse("2026-07-27T02:00:00Z"));
            m_Transport = new CatalogStoreTransport(
                HlsCatalogCodec.SerializeDocument(new HlsCatalogDocument(
                    HlsCatalogCodec.SchemaVersion,
                    4,
                    DateTimeOffset.Parse("2026-07-27T02:00:00Z"),
                    new[] { original })),
                "etag-4");
            var cloudService = new CloudService(
                new CloudProviderRegistry().Register(new TencentCosProvider()),
                m_Transport,
                () => m_Config,
                credentialStore,
                (_, _) => UniTask.CompletedTask);
            m_Now = DateTimeOffset.Parse("2026-07-28T03:00:00Z");
            m_Repository = new HlsCatalogOriginRepository(
                cloudService,
                () => m_Config,
                () => "https://cdn.example.com/videos",
                () => m_Now);
        }

        [TearDown]
        public void TearDown()
        {
            if (IODirectory.Exists(m_TempDirectory))
            {
                IODirectory.Delete(m_TempDirectory, true);
            }
        }

        [Test]
        public void LoadOriginAsync_ReadsAuthenticatedOriginCatalog()
        {
            var result = m_Repository.LoadOriginAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual("media-a", result.Document.Items.Single().MediaId);
            Assert.AreEqual("etag-4", result.ETag);
            Assert.AreEqual(
                "https://bucket-1250000000.cos.ap-chengdu.myqcloud.com/videos/catalog.json",
                m_Transport.LastGetUri.AbsoluteUri);
        }

        [Test]
        public void LoadOriginAsync_WhenCatalogDoesNotExist_ReturnsEmptyCatalog()
        {
            m_Transport.GetStatusCode = 404;

            var result = m_Repository.LoadOriginAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(0, result.Document.Generation);
            Assert.IsEmpty(result.Document.Items);
            Assert.AreEqual(string.Empty, result.ETag);
        }

        [Test]
        public void RenameAsync_ChangesOnlyEditableFieldsAndUsesIfMatch()
        {
            var expectedUpdatedAt = DateTimeOffset.Parse("2026-07-27T02:00:00Z");

            var result = m_Repository.RenameAsync(
                    "media-a",
                    expectedUpdatedAt,
                    "Renamed",
                    "editor-user",
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(5, result.Document.Generation);
            Assert.AreEqual("Renamed", result.Item.Name);
            Assert.AreEqual("editor-user", result.Item.Uploader);
            Assert.AreEqual(m_Now, result.Item.UpdatedAtUtc);
            Assert.AreEqual(DateTimeOffset.Parse("2026-07-27T01:00:00Z"), result.Item.CreatedAtUtc);
            Assert.AreEqual("media-a/master.m3u8", result.Item.Location);
            Assert.AreEqual(new string('a', 64), result.Item.SourceSha256);
            Assert.AreEqual(CloudWriteConditionKind.IfMatchETag, m_Transport.LastWriteCondition.Kind);
            Assert.AreEqual("etag-4", m_Transport.LastWriteCondition.ETag);
        }

        [Test]
        public void RemoveAsync_RemovesOnlyExpectedItem()
        {
            var result = m_Repository.RemoveAsync(
                    "media-a",
                    DateTimeOffset.Parse("2026-07-27T02:00:00Z"),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual("media-a", result.RemovedItem.MediaId);
            Assert.AreEqual(0, result.Document.Items.Count);
            Assert.AreEqual(5, result.Document.Generation);
            var persisted = HlsCatalogCodec.ParseDocument(
                m_Transport.Content,
                "https://cdn.example.com/videos",
                true);
            Assert.AreEqual(0, persisted.Items.Count);
        }

        [Test]
        public void RenameAsync_WhenExpectedRevisionChanged_ReturnsItemChangedWithoutPut()
        {
            var exception = Assert.Throws<CatalogException>(() => m_Repository.RenameAsync(
                    "media-a",
                    DateTimeOffset.Parse("2026-07-27T05:00:00Z"),
                    "Renamed",
                    "editor-user",
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult());

            Assert.AreEqual(CatalogErrorKind.ItemChanged, exception.Kind);
            Assert.AreEqual(0, m_Transport.PutCount);
        }

        [Test]
        public void UpsertAsync_WhenAnotherItemIsCommittedConcurrently_MergesBothChanges()
        {
            var original = CreateItem(
                "media-a",
                "Original",
                DateTimeOffset.Parse("2026-07-27T01:00:00Z"),
                DateTimeOffset.Parse("2026-07-27T02:00:00Z"));
            var concurrent = CreateItem(
                "media-b",
                "Concurrent",
                DateTimeOffset.Parse("2026-07-27T01:30:00Z"),
                DateTimeOffset.Parse("2026-07-27T02:30:00Z"),
                new string('b', 64));
            var proposed = CreateItem(
                "media-c",
                "Proposed",
                m_Now,
                m_Now,
                new string('c', 64));
            m_Transport.ConcurrentContent = HlsCatalogCodec.SerializeDocument(
                new HlsCatalogDocument(
                    HlsCatalogCodec.SchemaVersion,
                    5,
                    DateTimeOffset.Parse("2026-07-27T02:30:00Z"),
                    new[] { original, concurrent }));
            m_Transport.ConcurrentETag = "etag-5";
            m_Transport.PreconditionFailuresRemaining = 1;

            var result = m_Repository.UpsertAsync(
                    proposed,
                    false,
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            CollectionAssert.AreEquivalent(
                new[] { "media-a", "media-b", "media-c" },
                result.Document.Items.Select(item => item.MediaId));
            Assert.AreEqual(6, result.Document.Generation);
            Assert.AreEqual(2, m_Transport.PutCount);
            Assert.AreEqual(CloudWriteConditionKind.IfMatchETag, m_Transport.LastWriteCondition.Kind);
            Assert.AreEqual("etag-5", m_Transport.LastWriteCondition.ETag);
        }

        [Test]
        public void RemoteCleanup_WhenDeletePartiallyFails_CanRetryAcrossAllPages()
        {
            var transport = new CleanupTransport();
            var credentialStore = new CloudCredentialStore(
                Path.Combine(m_TempDirectory, "cleanup-credentials.json"));
            credentialStore.Save(
                CloudProviderId.TencentCos,
                "publisher",
                new CloudCredential("access", "secret"));
            var cloudService = new CloudService(
                new CloudProviderRegistry().Register(new TencentCosProvider()),
                transport,
                () => m_Config,
                credentialStore,
                (_, _) => UniTask.CompletedTask);
            var cleaner = new HlsRemoteObjectCleaner(cloudService, () => m_Config);

            var first = cleaner.CleanupAsync("media-a", CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(2, transport.ListCount);
            Assert.AreEqual(2, first.SucceededCount);
            Assert.AreEqual(1, first.Failed.Count);
            Assert.IsTrue(first.Failed.ContainsKey("videos/media-a/preview.jpg"));

            transport.FailPreviewDelete = false;
            var retry = cleaner.CleanupAsync("media-a", CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(4, transport.ListCount);
            Assert.IsTrue(retry.IsSuccess);
            Assert.AreEqual(3, retry.SucceededCount);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "videos/media-a/master.m3u8",
                    "videos/media-a/preview.jpg",
                    "videos/media-a/720P/segment_00000.ts"
                },
                transport.DeleteKeys.Distinct());
        }

        private static CatalogItem CreateItem(
            string mediaId,
            string name,
            DateTimeOffset createdAtUtc,
            DateTimeOffset updatedAtUtc,
            string sourceSha256 = null)
        {
            return new CatalogItem(
                mediaId,
                name,
                MediaKind.Video,
                mediaId + "/master.m3u8",
                VideoFormat.Hls,
                mediaId + "/preview.jpg",
                1280,
                720,
                2000000,
                12000,
                new[]
                {
                    new CatalogRendition(
                        "720P",
                        null,
                        mediaId + "/720P/index.m3u8",
                        1280,
                        720,
                        2000000,
                        12000)
                },
                "source.mp4",
                sourceSha256 ?? new string('a', 64),
                "original-user",
                createdAtUtc,
                updatedAtUtc,
                mediaId + "/");
        }

        private sealed class CatalogStoreTransport : ICloudHttpTransport, ICloudHttpReadTransport
        {
            private string m_ETag;

            public CatalogStoreTransport(string content, string etag)
            {
                Content = content;
                m_ETag = etag;
            }

            public string Content { get; private set; }
            public int GetStatusCode { get; set; } = 200;
            public Uri LastGetUri { get; private set; }
            public int PutCount { get; private set; }
            public CloudWriteCondition LastWriteCondition { get; private set; }
            public int PreconditionFailuresRemaining { get; set; }
            public string ConcurrentContent { get; set; }
            public string ConcurrentETag { get; set; }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CancellationToken cancellationToken)
            {
                LastGetUri = request.Uri;
                return UniTask.FromResult(new CloudHttpResponse(
                    GetStatusCode,
                    new Dictionary<string, string>
                    {
                        ["ETag"] = m_ETag,
                        ["x-cos-request-id"] = "get-request"
                    },
                    Content));
            }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                PutCount++;
                LastWriteCondition = upload.WriteCondition;
                if (PreconditionFailuresRemaining > 0)
                {
                    PreconditionFailuresRemaining--;
                    Content = ConcurrentContent;
                    m_ETag = ConcurrentETag;
                    return UniTask.FromResult(new CloudHttpResponse(
                        412,
                        new Dictionary<string, string>
                        {
                            ["x-cos-request-id"] = "conflict-request"
                        },
                        string.Empty));
                }

                Content = IOFile.ReadAllText(upload.LocalFilePath);
                m_ETag = "etag-" + (4 + PutCount);
                return UniTask.FromResult(new CloudHttpResponse(
                    200,
                    new Dictionary<string, string>
                    {
                        ["ETag"] = m_ETag,
                        ["x-cos-request-id"] = "put-request"
                    },
                    string.Empty));
            }
        }

        private sealed class CleanupTransport : ICloudHttpTransport, ICloudHttpReadTransport
        {
            private readonly HashSet<string> m_Deleted = new HashSet<string>(StringComparer.Ordinal);

            public bool FailPreviewDelete { get; set; } = true;
            public int ListCount { get; private set; }
            public List<string> DeleteKeys { get; } = new List<string>();

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CancellationToken cancellationToken)
            {
                if (request.Method == CloudHttpMethod.Get)
                {
                    ListCount++;
                    var secondPage = request.Uri.Query.IndexOf(
                        "continuation-token=page-2",
                        StringComparison.Ordinal) >= 0;
                    var body = secondPage
                        ? "<ListBucketResult><IsTruncated>false</IsTruncated>" +
                          "<Contents><Key>videos/media-a/720P/segment_00000.ts</Key><ETag>c</ETag><Size>3</Size></Contents>" +
                          "</ListBucketResult>"
                        : "<ListBucketResult><IsTruncated>true</IsTruncated>" +
                          "<NextContinuationToken>page-2</NextContinuationToken>" +
                          "<Contents><Key>videos/media-a/master.m3u8</Key><ETag>a</ETag><Size>12</Size></Contents>" +
                          "<Contents><Key>videos/media-a/preview.jpg</Key><ETag>b</ETag><Size>24</Size></Contents>" +
                          "</ListBucketResult>";
                    return UniTask.FromResult(Response(200, body));
                }

                var objectKey = Uri.UnescapeDataString(request.Uri.AbsolutePath.TrimStart('/'));
                DeleteKeys.Add(objectKey);
                if (FailPreviewDelete && objectKey.EndsWith("/preview.jpg", StringComparison.Ordinal))
                {
                    return UniTask.FromResult(Response(403, string.Empty));
                }

                var existed = m_Deleted.Add(objectKey);
                return UniTask.FromResult(Response(existed ? 204 : 404, string.Empty));
            }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Upload is not expected during remote cleanup.");
            }

            private static CloudHttpResponse Response(int statusCode, string body)
            {
                return new CloudHttpResponse(
                    statusCode,
                    new Dictionary<string, string>
                    {
                        ["x-cos-request-id"] = "cleanup-request"
                    },
                    body);
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.MediaEditor;
using GameDeveloperKit.StoryEditor.Media;
using NUnit.Framework;
using UnityEngine.TestTools;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests.Cloud
{
    public sealed class HlsPublishWorkflowTests
    {
        private string m_Root;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "gdk-hls-workflow-" + Guid.NewGuid().ToString("N"));
            IODirectory.CreateDirectory(m_Root);
        }

        [TearDown]
        public void TearDown()
        {
            if (IODirectory.Exists(m_Root))
            {
                IODirectory.Delete(m_Root, true);
            }
        }

        [UnityTest]
        public IEnumerator ComputeSourceSha256Async_StreamsExpectedLowercaseHash()
        {
            var path = Path.Combine(m_Root, "source.mp4");
            var bytes = Enumerable.Range(0, 400000)
                .Select(index => (byte)(index % 251))
                .ToArray();
            IOFile.WriteAllBytes(path, bytes);
            string expected;
            using (var algorithm = SHA256.Create())
            {
                expected = string.Concat(algorithm.ComputeHash(bytes)
                    .Select(value => value.ToString("x2")));
            }

            return UniTask.ToCoroutine(async () =>
            {
                var actual = await HlsPublishWorkflow.ComputeSourceSha256Async(
                    path,
                    CancellationToken.None);
                Assert.AreEqual(expected, actual);
            });
        }

        [Test]
        public void PublishAsync_UploadsPackageThenCommitsCatalogAsOnlyCompletionMarker()
        {
            var packageRoot = Path.Combine(m_Root, "package");
            IODirectory.CreateDirectory(Path.Combine(packageRoot, "720P"));
            var master = Path.Combine(packageRoot, "master.m3u8");
            var playlist = Path.Combine(packageRoot, "720P", "index.m3u8");
            var preview = Path.Combine(packageRoot, "preview.jpg");
            IOFile.WriteAllText(master, "#EXTM3U\n720P/index.m3u8\n");
            IOFile.WriteAllText(playlist, "#EXTM3U\n#EXTINF:2,\nsegment_00000.ts\n");
            IOFile.WriteAllBytes(
                Path.Combine(packageRoot, "720P", "segment_00000.ts"),
                new byte[] { 1, 2, 3 });
            IOFile.WriteAllBytes(preview, new byte[]
            {
                0xff, 0xd8,
                0xff, 0xc0, 0x00, 0x07, 0x08, 0x01, 0x68, 0x02, 0x80,
                0xff, 0xd9
            });
            var transcode = new HlsTranscodeResult(
                packageRoot,
                master,
                new[]
                {
                    new HlsRenditionInfo("720P", 1280, 720, 2000000, playlist)
                },
                string.Empty,
                string.Empty,
                preview,
                12000);
            var config = new CloudProjectConfig
            {
                ProviderId = CloudProviderId.TencentCos,
                CredentialProfileName = "publisher",
                Bucket = "bucket-1250000000",
                Region = "ap-chengdu",
                RootPrefix = "videos"
            };
            var credentialStore = new CloudCredentialStore(
                Path.Combine(m_Root, "credentials.json"));
            credentialStore.Save(
                CloudProviderId.TencentCos,
                "publisher",
                new CloudCredential("access", "secret"));
            var transport = new WorkflowTransport();
            var cloud = new CloudService(
                new CloudProviderRegistry().Register(new TencentCosProvider()),
                transport,
                () => config,
                credentialStore,
                (_, _) => UniTask.CompletedTask);
            var repository = new HlsCatalogOriginRepository(
                cloud,
                () => config,
                () => "https://cdn.example.com/videos",
                () => DateTimeOffset.Parse("2026-07-28T03:00:00Z"));
            var workflow = new HlsPublishWorkflow(
                new HlsPackagePublisher(cloud, () => config, () => "unused"),
                repository,
                () => DateTimeOffset.Parse("2026-07-28T03:00:00Z"));
            var intent = new HlsPublishIntent(
                Path.Combine(m_Root, "source.mp4"),
                "Intro",
                new string('a', 64),
                "media-a",
                false,
                null,
                null);

            var result = workflow.PublishAsync(intent, transcode, null, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            var calls = transport.UploadedKeys.ToArray();
            Assert.AreEqual("videos/catalog.json", calls.Last());
            Assert.That(Array.IndexOf(calls, "videos/media-a/preview.jpg"),
                Is.LessThan(Array.IndexOf(calls, "videos/media-a/master.m3u8")));
            Assert.That(Array.IndexOf(calls, "videos/media-a/master.m3u8"),
                Is.LessThan(Array.IndexOf(calls, "videos/catalog.json")));
            Assert.IsFalse(calls.Any(key => key.EndsWith("metadata.json", StringComparison.Ordinal)));
            Assert.IsFalse(calls.Any(key => key.EndsWith("manifest.json", StringComparison.Ordinal)));
            Assert.AreEqual("media-a/preview.jpg", result.Catalog.Item.ThumbnailLocation);
            Assert.AreEqual("media-a/master.m3u8", result.Catalog.Item.Location);
            Assert.AreEqual(CloudWriteConditionKind.IfAbsent, transport.CatalogWriteCondition.Kind);
        }

        [Test]
        public void CommitCatalogAsync_AfterCatalogFailure_DoesNotUploadPackageAgain()
        {
            var fixture = CreateWorkflowFixture();
            fixture.Transport.FailCatalogWritesRemaining = 1;

            var exception = Assert.Throws<HlsCatalogCommitPendingException>(() =>
                fixture.Workflow.PublishAsync(
                        fixture.Intent,
                        fixture.Transcode,
                        null,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult());
            var packageUploadsBeforeRetry = fixture.Transport.UploadedKeys.Count(key =>
                key.StartsWith("videos/media-a/", StringComparison.Ordinal));
            var catalogAttemptsBeforeRetry = fixture.Transport.UploadedKeys.Count(key =>
                key == "videos/catalog.json");

            var catalog = fixture.Workflow.CommitCatalogAsync(
                    fixture.Intent,
                    fixture.Transcode,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual("media-a", exception.Package.MediaId);
            Assert.AreEqual("media-a", catalog.Item.MediaId);
            Assert.AreEqual(packageUploadsBeforeRetry, fixture.Transport.UploadedKeys.Count(key =>
                key.StartsWith("videos/media-a/", StringComparison.Ordinal)));
            Assert.AreEqual(catalogAttemptsBeforeRetry + 1, fixture.Transport.UploadedKeys.Count(key =>
                key == "videos/catalog.json"));
        }

        private WorkflowFixture CreateWorkflowFixture()
        {
            var packageRoot = Path.Combine(m_Root, "retry-package");
            IODirectory.CreateDirectory(Path.Combine(packageRoot, "720P"));
            var master = Path.Combine(packageRoot, "master.m3u8");
            var playlist = Path.Combine(packageRoot, "720P", "index.m3u8");
            var preview = Path.Combine(packageRoot, "preview.jpg");
            IOFile.WriteAllText(master, "#EXTM3U\n720P/index.m3u8\n");
            IOFile.WriteAllText(playlist, "#EXTM3U\n#EXTINF:2,\nsegment_00000.ts\n");
            IOFile.WriteAllBytes(
                Path.Combine(packageRoot, "720P", "segment_00000.ts"),
                new byte[] { 1, 2, 3 });
            IOFile.WriteAllBytes(preview, new byte[]
            {
                0xff, 0xd8,
                0xff, 0xc0, 0x00, 0x07, 0x08, 0x01, 0x68, 0x02, 0x80,
                0xff, 0xd9
            });
            var transcode = new HlsTranscodeResult(
                packageRoot,
                master,
                new[]
                {
                    new HlsRenditionInfo("720P", 1280, 720, 2000000, playlist)
                },
                string.Empty,
                string.Empty,
                preview,
                12000);
            var config = new CloudProjectConfig
            {
                ProviderId = CloudProviderId.TencentCos,
                CredentialProfileName = "publisher",
                Bucket = "bucket-1250000000",
                Region = "ap-chengdu",
                RootPrefix = "videos"
            };
            var credentialStore = new CloudCredentialStore(
                Path.Combine(m_Root, "retry-credentials.json"));
            credentialStore.Save(
                CloudProviderId.TencentCos,
                "publisher",
                new CloudCredential("access", "secret"));
            var transport = new WorkflowTransport();
            var cloud = new CloudService(
                new CloudProviderRegistry().Register(new TencentCosProvider()),
                transport,
                () => config,
                credentialStore,
                (_, _) => UniTask.CompletedTask);
            var repository = new HlsCatalogOriginRepository(
                cloud,
                () => config,
                () => "https://cdn.example.com/videos",
                () => DateTimeOffset.Parse("2026-07-28T03:00:00Z"));
            var workflow = new HlsPublishWorkflow(
                new HlsPackagePublisher(cloud, () => config, () => "unused"),
                repository,
                () => DateTimeOffset.Parse("2026-07-28T03:00:00Z"));
            var intent = new HlsPublishIntent(
                Path.Combine(m_Root, "source.mp4"),
                "Intro",
                new string('a', 64),
                "media-a",
                false,
                null,
                null);
            return new WorkflowFixture(workflow, intent, transcode, transport);
        }

        private sealed class WorkflowTransport : ICloudHttpTransport, ICloudHttpReadTransport
        {
            private readonly object m_Gate = new object();
            private readonly List<string> m_UploadedKeys = new List<string>();

            public IReadOnlyList<string> UploadedKeys
            {
                get
                {
                    lock (m_Gate)
                    {
                        return m_UploadedKeys.ToArray();
                    }
                }
            }

            public CloudWriteCondition CatalogWriteCondition { get; private set; }
            public int FailCatalogWritesRemaining { get; set; }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CancellationToken cancellationToken)
            {
                return UniTask.FromResult(new CloudHttpResponse(
                    404,
                    new Dictionary<string, string>(),
                    string.Empty));
            }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                lock (m_Gate)
                {
                    m_UploadedKeys.Add(upload.ObjectKey);
                }

                if (upload.ObjectKey.EndsWith("catalog.json", StringComparison.Ordinal))
                {
                    CatalogWriteCondition = upload.WriteCondition;
                    if (FailCatalogWritesRemaining > 0)
                    {
                        FailCatalogWritesRemaining--;
                        return UniTask.FromResult(new CloudHttpResponse(
                            403,
                            new Dictionary<string, string>
                            {
                                ["x-cos-request-id"] = "catalog-denied"
                            },
                            string.Empty));
                    }
                }

                return UniTask.FromResult(new CloudHttpResponse(
                    200,
                    new Dictionary<string, string>
                    {
                        ["ETag"] = "etag",
                        ["x-cos-request-id"] = "request"
                    },
                    string.Empty));
            }
        }

        private sealed class WorkflowFixture
        {
            public WorkflowFixture(
                HlsPublishWorkflow workflow,
                HlsPublishIntent intent,
                HlsTranscodeResult transcode,
                WorkflowTransport transport)
            {
                Workflow = workflow;
                Intent = intent;
                Transcode = transcode;
                Transport = transport;
            }

            public HlsPublishWorkflow Workflow { get; }
            public HlsPublishIntent Intent { get; }
            public HlsTranscodeResult Transcode { get; }
            public WorkflowTransport Transport { get; }
        }
    }
}

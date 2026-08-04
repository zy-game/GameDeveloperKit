using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.MediaEditor;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.StoryEditor.Media;
using NUnit.Framework;

namespace GameDeveloperKit.Tests.MediaProcessing
{
    public sealed class HlsBatchPublishControllerTests
    {
        private string m_Root;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "gdk-hls-batch-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, true);
            }
        }

        [Test]
        public void RunAsync_ProcessesSeriallyAndContinuesAfterOneItemFails()
        {
            var calls = new List<string>();
            var dependencies = SuccessfulDependencies(calls);
            dependencies.TranscodeAsync = (request, _, _) =>
            {
                var name = Path.GetFileNameWithoutExtension(request.InputMp4Path);
                calls.Add("transcode:" + name);
                return name == "second"
                    ? UniTask.FromException<HlsTranscodeResult>(new InvalidOperationException("encode failed"))
                    : UniTask.FromResult(TranscodeResult(name));
            };
            var controller = CreateController(
                dependencies,
                CreateIntent("first"),
                CreateIntent("second"),
                CreateIntent("third"));

            controller.RunAsync(CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(
                new[]
                {
                    "probe:first", "transcode:first", "publish:first",
                    "probe:second", "transcode:second",
                    "probe:third", "transcode:third", "publish:third"
                },
                calls);
            Assert.AreEqual(HlsBatchPublishItemState.Completed, controller.Items[0].State);
            Assert.AreEqual(HlsBatchPublishItemState.Failed, controller.Items[1].State);
            Assert.AreEqual(HlsBatchPublishItemState.Completed, controller.Items[2].State);
        }

        [Test]
        public void RunAsync_WhenSourceHasNoEligibleRendition_FailsOnlyThatItemWithoutPublishing()
        {
            var calls = new List<string>();
            var dependencies = SuccessfulDependencies(calls);
            dependencies.ProbeAsync = (_, input, _) =>
            {
                var name = Path.GetFileNameWithoutExtension(input);
                calls.Add("probe:" + name);
                return UniTask.FromResult(name == "first"
                    ? new MediaProbeInfo(320, 180, 10d, 30d, 16000000L, true)
                    : new MediaProbeInfo(1920, 1080, 10d, 30d, 10000000L, true));
            };
            var controller = CreateController(
                dependencies,
                CreateIntent("first"),
                CreateIntent("second"));

            controller.RunAsync(CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(
                new[] { "probe:first", "probe:second", "transcode:second", "publish:second" },
                calls);
            Assert.AreEqual(HlsBatchPublishItemState.Failed, controller.Items[0].State);
            StringAssert.Contains("没有符合", controller.Items[0].Error);
            Assert.AreEqual(HlsBatchPublishItemState.Completed, controller.Items[1].State);
        }

        [Test]
        public void RetryAsync_WhenCatalogIsPending_OnlyRetriesCatalogCommit()
        {
            var calls = new List<string>();
            var dependencies = SuccessfulDependencies(calls);
            dependencies.PublishAsync = (intent, _, _, _, _) =>
            {
                calls.Add("publish:" + intent.DisplayName);
                return UniTask.FromException<HlsPublishWorkflowResult>(
                    new HlsCatalogCommitPendingException(
                        Package(intent.MediaId),
                        new InvalidOperationException("catalog unavailable")));
            };
            var controller = CreateController(dependencies, CreateIntent("first"));

            controller.RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(HlsBatchPublishItemState.CatalogPending, controller.Items[0].State);

            controller.RetryAsync(controller.Items[0], CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            CollectionAssert.AreEqual(
                new[] { "probe:first", "transcode:first", "publish:first", "commit:first" },
                calls);
            Assert.AreEqual(HlsBatchPublishItemState.Completed, controller.Items[0].State);
            Assert.IsNull(controller.Items[0].PendingCatalogPackage);
        }

        [Test]
        public void RunAsync_WhenCancelled_StopsBeforePendingItemsStart()
        {
            var cancellation = new CancellationTokenSource();
            var calls = new List<string>();
            var dependencies = SuccessfulDependencies(calls);
            dependencies.ProbeAsync = (_, input, token) =>
            {
                calls.Add("probe:" + Path.GetFileNameWithoutExtension(input));
                cancellation.Cancel();
                return UniTask.FromException<MediaProbeInfo>(new OperationCanceledException(token));
            };
            var controller = CreateController(
                dependencies,
                CreateIntent("first"),
                CreateIntent("second"));

            Assert.Throws<OperationCanceledException>(() =>
                controller.RunAsync(cancellation.Token).GetAwaiter().GetResult());

            CollectionAssert.AreEqual(new[] { "probe:first" }, calls);
            Assert.AreEqual(HlsBatchPublishItemState.Cancelled, controller.Items[0].State);
            Assert.AreEqual(HlsBatchPublishItemState.Pending, controller.Items[1].State);
        }

        [Test]
        public void Preflight_DeduplicatesPathsAndContentAndRejectsInvalidFiles()
        {
            var first = Path.Combine(m_Root, "first.mp4");
            var duplicateContent = Path.Combine(m_Root, "duplicate.mp4");
            var wrongExtension = Path.Combine(m_Root, "notes.txt");
            System.IO.File.WriteAllText(first, "first");
            System.IO.File.WriteAllText(duplicateContent, "duplicate");
            System.IO.File.WriteAllText(wrongExtension, "notes");

            var result = HlsBatchPublishPreflight.CreateAsync(
                    new[] { first, first, duplicateContent, wrongExtension, Path.Combine(m_Root, "missing.mp4") },
                    EmptyCatalog(),
                    (_, _) => UniTask.FromResult("same-fingerprint"),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(1, result.Candidates.Count);
            Assert.AreEqual(4, result.Rejected.Count);
            Assert.AreEqual(Path.GetFullPath(first).Replace('\\', '/'), result.Candidates[0].SourcePath);
        }

        [Test]
        public void Preflight_ExistingSourceIsSkippedUnlessOverwriteIsExplicit()
        {
            var source = Path.Combine(m_Root, "existing.mp4");
            System.IO.File.WriteAllText(source, "existing");
            var existing = new CatalogItem(
                "existing-media-id",
                "Existing name",
                MediaKind.Video,
                "existing-media-id/master.m3u8",
                VideoFormat.Hls,
                string.Empty,
                1920,
                1080,
                1000000,
                1000,
                Array.Empty<CatalogRendition>(),
                sourceFileName: "existing.mp4",
                sourceSha256: "existing-sha",
                createdAtUtc: DateTimeOffset.UtcNow,
                updatedAtUtc: DateTimeOffset.UtcNow);
            var catalog = new HlsCatalogDocument(1, 1, DateTimeOffset.UtcNow, new[] { existing });

            var result = HlsBatchPublishPreflight.CreateAsync(
                    new[] { source },
                    catalog,
                    (_, _) => UniTask.FromResult("existing-sha"),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(1, result.ExistingCount);
            Assert.AreEqual(0, result.CreateIntents(false).Count);
            var overwrite = result.CreateIntents(true);
            Assert.AreEqual(1, overwrite.Count);
            Assert.AreEqual("existing-media-id", overwrite[0].MediaId);
            Assert.IsTrue(overwrite[0].IsOverwrite);
        }

        private HlsBatchPublishController CreateController(
            HlsBatchPublishDependencies dependencies,
            params HlsPublishIntent[] intents)
        {
            return new HlsBatchPublishController(intents, m_Root, "ffprobe", dependencies);
        }

        private HlsPublishIntent CreateIntent(string name)
        {
            var path = Path.Combine(m_Root, name + ".mp4");
            System.IO.File.WriteAllText(path, name);
            return new HlsPublishIntent(
                path,
                name,
                "sha-" + name,
                "media-" + name,
                false,
                null,
                null);
        }

        private static HlsBatchPublishDependencies SuccessfulDependencies(ICollection<string> calls)
        {
            return new HlsBatchPublishDependencies
            {
                ProbeAsync = (_, input, _) =>
                {
                    calls.Add("probe:" + Path.GetFileNameWithoutExtension(input));
                    return UniTask.FromResult(new MediaProbeInfo(
                        1920,
                        1080,
                        10d,
                        30d,
                        10000000L,
                        true));
                },
                TranscodeAsync = (request, _, _) =>
                {
                    var name = Path.GetFileNameWithoutExtension(request.InputMp4Path);
                    calls.Add("transcode:" + name);
                    return UniTask.FromResult(TranscodeResult(name));
                },
                PublishAsync = (intent, _, _, _, _) =>
                {
                    calls.Add("publish:" + intent.DisplayName);
                    return UniTask.FromResult(new HlsPublishWorkflowResult(
                        Package(intent.MediaId),
                        CatalogResult()));
                },
                CommitCatalogAsync = (intent, _, _) =>
                {
                    calls.Add("commit:" + intent.DisplayName);
                    return UniTask.FromResult(CatalogResult());
                },
                DirectoryExists = _ => false
            };
        }

        private static HlsTranscodeResult TranscodeResult(string name)
        {
            return new HlsTranscodeResult(
                name,
                name + "/master.m3u8",
                Array.Empty<HlsRenditionInfo>(),
                string.Empty,
                string.Empty);
        }

        private static HlsPackagePublishResult Package(string mediaId)
        {
            return new HlsPackagePublishResult(
                mediaId,
                mediaId + "/master.m3u8",
                Array.Empty<GameDeveloperKit.EditorCloud.CloudUploadResult>());
        }

        private static HlsCatalogCommitResult CatalogResult()
        {
            return new HlsCatalogCommitResult(
                EmptyCatalog(),
                null);
        }

        private static HlsCatalogDocument EmptyCatalog()
        {
            return new HlsCatalogDocument(1, 1, DateTimeOffset.UtcNow, Array.Empty<CatalogItem>());
        }
    }
}

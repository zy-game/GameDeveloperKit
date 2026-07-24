using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Resource;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class VisualPlayableTests : RuntimeTestBase
    {
        [SetUp]
        public void SetUp()
        {
            if (App.TryGetRegistered<PlayableModule>(out _))
            {
                App.Unregister<PlayableModule>().GetAwaiter().GetResult();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (App.TryGetRegistered<PlayableModule>(out _))
            {
                App.Unregister<PlayableModule>().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void VideoQualityContracts_WhenCreated_UseStandardLabelsAndSelections()
        {
            var option = new VideoQualityOption(null, 2560, 1440, 8000000, "https://cdn.example.com/2k.m3u8");
            var selection = new VideoQualitySelection(VideoQualityMode.FixedHeight, 1440);

            Assert.AreEqual("2K", option.Label);
            Assert.AreEqual(1440, selection.Height);
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new VideoQualitySelection(VideoQualityMode.FixedHeight));
        }

        [Test]
        public void VideoPlayableHandle_WhenTwoQualitiesProvided_ExposesAutoAndRejectsMissingHeight()
        {
            var handle = new VideoPlayableHandle(
                "https://cdn.example.com/master.m3u8",
                new VideoPlayableOptions
                {
                    SupportsAutoQuality = true,
                    QualityOptions = new[]
                    {
                        new VideoQualityOption("HD", 1280, 720, 3000000, "https://cdn.example.com/720.m3u8"),
                        new VideoQualityOption("FHD", 1920, 1080, 6000000, "https://cdn.example.com/1080.m3u8")
                    }
                },
                false);
            try
            {
                Assert.IsTrue(handle.CanSelectQuality);
                Assert.IsTrue(handle.SupportsAutoQuality);
                Assert.AreEqual(VideoQualityMode.Auto, handle.Quality.Mode);
                Assert.Throws<GameException>(() =>
                    handle.SetQualityAsync(new VideoQualitySelection(VideoQualityMode.FixedHeight, 2160)));
            }
            finally
            {
                handle.Dispose();
            }
        }

        [Test]
        public void VideoSurfaceBinder_WhenTargetIsWider_CropsVerticalCenter()
        {
            var uv = VideoSurfaceBinder.CalculateCoverUvRect(21f / 9f, 16f / 9f, false);

            Assert.AreEqual(0f, uv.x, 0.0001f);
            Assert.AreEqual(1f, uv.width, 0.0001f);
            Assert.Greater(uv.y, 0f);
            Assert.Less(uv.height, 1f);
        }

        [Test]
        public void VideoSurfaceBinder_WhenTargetIsNarrowerAndFlipped_CropsHorizontalAndFlips()
        {
            var uv = VideoSurfaceBinder.CalculateCoverUvRect(4f / 3f, 16f / 9f, true);

            Assert.Greater(uv.x, 0f);
            Assert.Less(uv.width, 1f);
            Assert.AreEqual(1f, uv.y, 0.0001f);
            Assert.AreEqual(-1f, uv.height, 0.0001f);
        }

        [Test]
        public void PlayableModule_WhenResolved_RegistersVisualPlayables()
        {
            var module = App.Playable;

            Assert.IsNotNull(module.Text);
            Assert.IsNotNull(module.Image);
            Assert.IsNotNull(module.Video);
        }

        [Test]
        public void PlayTextAsync_WhenStopped_ClearsOutput()
        {
            var output = string.Empty;

            var handle = App.Playable.PlayTextAsync("line", value => output = value).GetAwaiter().GetResult();

            Assert.AreEqual("line", output);
            Assert.AreEqual(PlayableStatus.Playing, handle.Status);
            handle.Stop();
            Assert.AreEqual(string.Empty, output);
            Assert.AreEqual(PlayableStatus.Stopped, handle.Status);
        }

        [Test]
        public void VisualRequests_WhenSourceIsEmpty_RejectImmediately()
        {
            Assert.Throws<ArgumentException>(() => new ImagePlayableRequest("", _ => { }));
            Assert.Throws<ArgumentException>(() => new VideoPlayableRequest(""));
            Assert.Throws<ArgumentNullException>(() => new TextPlayableRequest(null, _ => { }));
        }

        [Test]
        public void PlayTextAsync_WhenCanceled_DoesNotWriteOutput()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            var called = false;

            Assert.Throws<OperationCanceledException>(() =>
                App.Playable.PlayTextAsync("line", _ => called = true, source.Token).GetAwaiter().GetResult());
            Assert.IsFalse(called);
        }

        [Test]
        public void StartLoadedImage_WhenOutputThrows_ReleasesLoadedAsset()
        {
            var texture = new Texture2D(1, 1);
            var asset = AssetHandle.Success(
                new AssetInfo { Location = "image-output-failure", TypeName = nameof(Texture2D) },
                texture);
            var request = new ImagePlayableRequest(
                "image-output-failure",
                _ => throw new InvalidOperationException("output failed"));

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    ImagePlayable.StartLoadedImage(request, texture, asset));

                Assert.AreEqual("output failed", exception.Message);
                Assert.AreEqual(ResourceStatus.Released, asset.Status);
                Assert.AreEqual(0, asset.ReferenceCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void StartHandle_WhenVideoStartThrows_RollsBackActiveHandle()
        {
            var playable = new VideoPlayable();
            var handle = new VideoPlayableHandle(
                "video-start-failure",
                new VideoPlayableOptions { DontDestroyOnLoad = false },
                false);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                playable.StartHandle(handle, _ => throw new InvalidOperationException("open failed")));

            Assert.AreEqual("open failed", exception.Message);
            Assert.IsEmpty(playable.ActiveHandles);
            Assert.AreEqual(PlayableStatus.Stopped, handle.Status);
            playable.Dispose();
        }

        [UnityTest]
        public IEnumerator StartHandle_WhenPreloadedHandleHasFirstFrame_ReplaysPlaybackStarted()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var playable = new VideoPlayable();
                var path = Path.Combine(
                    Application.streamingAssetsPath,
                    "AVProVideoSamples",
                    "BigBuckBunny-360p30-H264.mp4");
                var handle = new VideoPlayableHandle(
                    path,
                    new VideoPlayableOptions { DontDestroyOnLoad = false },
                    true);
                using var timeout = new CancellationTokenSource();
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    handle.Preload();
                    await handle.WaitUntilReadyAsync(timeout.Token);
                    await UniTask.WaitUntil(
                        () => handle.HasFirstFrame,
                        cancellationToken: timeout.Token);

                    var playbackStartedCount = 0;
                    playable.PlaybackStarted += _ => playbackStartedCount++;
                    playable.StartHandle(handle, value => value.Play());

                    Assert.AreEqual(1, playbackStartedCount);
                }
                finally
                {
                    playable.Dispose();
                    handle.Dispose();
                }
            });
        }

        [Test]
        public void ReleasePreload_WhenPathIsNotCached_IsIdempotent()
        {
            var playable = new VideoPlayable();
            try
            {
                Assert.IsFalse(playable.ReleasePreload("missing-video"));
                Assert.IsFalse(playable.ReleasePreload("missing-video"));
                Assert.IsEmpty(playable.ActiveHandles);
            }
            finally
            {
                playable.Dispose();
            }
        }
    }
}

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

        [TestCase("https://cdn.example.com/master.m3u8", true)]
        [TestCase("https://cdn.example.com/4K/index.M3U8?token=abc#live", true)]
        [TestCase("https://cdn.example.com/video.mp4", false)]
        [TestCase("", false)]
        public void VideoPlayableHandle_IsHlsPath_DetectsPlaylistUrls(string path, bool expected)
        {
            Assert.AreEqual(expected, VideoPlayableHandle.IsHlsPath(path));
        }

        [Test]
        public void VideoPlayableHandle_WhenTwoQualitiesProvided_ExposesPlatformCapabilityAndRejectsMissingHeight()
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
                Assert.AreEqual(VideoPlayableHandle.SupportsNativeHlsVariantSelection, handle.CanSelectQuality);
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
        public void VideoPlayableHandle_WhenAutoAndOneFixedQualityProvided_UsesNativePlatformCapability()
        {
            var handle = new VideoPlayableHandle(
                "https://cdn.example.com/master.m3u8",
                new VideoPlayableOptions
                {
                    SupportsAutoQuality = true,
                    QualityOptions = new[]
                    {
                        new VideoQualityOption("HD", 1280, 720, 3000000, "https://cdn.example.com/720.m3u8")
                    }
                },
                false);
            try
            {
                Assert.AreEqual(VideoPlayableHandle.SupportsNativeHlsVariantSelection, handle.CanSelectQuality);
            }
            finally
            {
                handle.Dispose();
            }
        }

        [Test]
        public void VideoPlayableHandle_WhenHlsRequestsFixedInitialQuality_StartsWithNativeAuto()
        {
            const string masterPath = "https://cdn.example.com/master.m3u8";
            var handle = new VideoPlayableHandle(
                masterPath,
                new VideoPlayableOptions
                {
                    SupportsAutoQuality = true,
                    InitialQuality = new VideoQualitySelection(VideoQualityMode.FixedHeight, 720),
                    QualityOptions = new[]
                    {
                        new VideoQualityOption("HD", 1280, 720, 3000000, "https://cdn.example.com/720.m3u8"),
                        new VideoQualityOption("FHD", 1920, 1080, 6000000, "https://cdn.example.com/1080.m3u8")
                    }
                },
                false);
            try
            {
                Assert.AreEqual(masterPath, handle.Path);
                Assert.AreEqual(VideoQualityMode.Auto, handle.Quality.Mode);
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
        public void VideoSurfaceBinder_WhenContainerIsWider_FitsByHeight()
        {
            var size = VideoSurfaceBinder.CalculateFitSize(1920f, 1080f, 1080f, 1920f);

            Assert.AreEqual(1080f, size.y, 0.0001f);
            Assert.AreEqual(1080f * (1080f / 1920f), size.x, 0.0001f);
        }

        [Test]
        public void VideoSurfaceBinder_WhenContainerIsNarrower_FitsByWidth()
        {
            var size = VideoSurfaceBinder.CalculateFitSize(1080f, 1920f, 1920f, 1080f);

            Assert.AreEqual(1080f, size.x, 0.0001f);
            Assert.AreEqual(1080f * (1080f / 1920f), size.y, 0.0001f);
        }

        [Test]
        public void VideoSurfaceBinder_WhenAspectsMatch_FillsContainer()
        {
            var size = VideoSurfaceBinder.CalculateFitSize(1920f, 1080f, 1280f, 720f);

            Assert.AreEqual(1920f, size.x, 0.0001f);
            Assert.AreEqual(1080f, size.y, 0.0001f);
        }

        [Test]
        public void VideoSurfaceBinder_NoScaling_PreservesNativeSize()
        {
            VideoSurfaceBinder.CalculateLayout(1920f, 1080f, 1280f, 720f, VideoDisplayMode.NoScaling, out var size, out var uv);

            Assert.AreEqual(1280f, size.x, 0.0001f);
            Assert.AreEqual(720f, size.y, 0.0001f);
            Assert.AreEqual(1f, uv.width, 0.0001f);
            Assert.AreEqual(1f, uv.height, 0.0001f);
        }

        [Test]
        public void VideoSurfaceBinder_NoScaling_WhenVideoExceedsContainer_Downscales()
        {
            VideoSurfaceBinder.CalculateLayout(1920f, 1080f, 3840f, 2160f, VideoDisplayMode.NoScaling, out var size, out _);

            Assert.AreEqual(1920f, size.x, 0.0001f);
            Assert.AreEqual(1080f, size.y, 0.0001f);
        }

        [Test]
        public void VideoSurfaceBinder_FitVertically_WhenVideoNarrower_Letterboxes()
        {
            VideoSurfaceBinder.CalculateLayout(1920f, 1080f, 1080f, 1920f, VideoDisplayMode.FitVertically, out var size, out var uv);

            Assert.AreEqual(1080f, size.y, 0.0001f);
            Assert.AreEqual(1080f * (1080f / 1920f), size.x, 0.0001f);
            Assert.AreEqual(1f, uv.width, 0.0001f);
            Assert.AreEqual(1f, uv.height, 0.0001f);
        }

        [Test]
        public void VideoSurfaceBinder_FitVertically_WhenVideoWider_CropsHorizontally()
        {
            VideoSurfaceBinder.CalculateLayout(1080f, 1920f, 1920f, 1080f, VideoDisplayMode.FitVertically, out var size, out var uv);

            Assert.AreEqual(1080f, size.x, 0.0001f);
            Assert.AreEqual(1920f, size.y, 0.0001f);
            Assert.AreEqual(1080f / 1920f / (1920f / 1080f), uv.width, 0.0001f);
            Assert.Greater(uv.x, 0f);
            Assert.Less(uv.x, 0.5f);
        }

        [Test]
        public void VideoSurfaceBinder_FitHorizontally_WhenVideoWider_Letterboxes()
        {
            VideoSurfaceBinder.CalculateLayout(1080f, 1920f, 1920f, 1080f, VideoDisplayMode.FitHorizontally, out var size, out var uv);

            Assert.AreEqual(1080f, size.x, 0.0001f);
            Assert.AreEqual(1080f / (1920f / 1080f), size.y, 0.0001f);
            Assert.AreEqual(1f, uv.width, 0.0001f);
            Assert.AreEqual(1f, uv.height, 0.0001f);
        }

        [Test]
        public void VideoSurfaceBinder_FitHorizontally_WhenVideoTaller_CropsVertically()
        {
            VideoSurfaceBinder.CalculateLayout(1920f, 1080f, 1080f, 1920f, VideoDisplayMode.FitHorizontally, out var size, out var uv);

            Assert.AreEqual(1920f, size.x, 0.0001f);
            Assert.AreEqual(1080f, size.y, 0.0001f);
            Assert.AreEqual(1080f / 1920f / (1920f / 1080f), uv.height, 0.0001f);
            Assert.Greater(uv.y, 0f);
            Assert.Less(uv.y, 0.5f);
        }

        [Test]
        public void VideoSurfaceBinder_Stretch_FillsContainer()
        {
            VideoSurfaceBinder.CalculateLayout(1920f, 1080f, 100f, 200f, VideoDisplayMode.Stretch, out var size, out var uv);

            Assert.AreEqual(1920f, size.x, 0.0001f);
            Assert.AreEqual(1080f, size.y, 0.0001f);
            Assert.AreEqual(1f, uv.width, 0.0001f);
            Assert.AreEqual(1f, uv.height, 0.0001f);
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
                var path = FindStoryVideoFixture();
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
                    Assert.IsTrue(handle.HasFirstFrame);

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

        [Test]
        public void AvProVideoPlayerInstance_WhenCreatedTwice_DoesNotReuseMediaPlayer()
        {
            var first = new AvProVideoPlayerInstance("FirstVideo", null, false, false);
            var firstPlayer = first.Player;
            first.Dispose();

            var second = new AvProVideoPlayerInstance("SecondVideo", null, false, false);
            try
            {
                Assert.AreNotSame(firstPlayer, second.Player);
            }
            finally
            {
                second.Dispose();
            }
        }

        [TestCase(10d, true)]
        [TestCase(0.05d, false)]
        [TestCase(0d, false)]
        public void RequiresFinalQualityAlignment_UsesMeaningfulSourceTime(
            double sourceTime,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                VideoPlayableHandle.RequiresFinalQualityAlignment(sourceTime));
        }

        [Test]
        public void RequiresFinalQualityAlignment_WhenSourceTimeIsInvalid_SkipsAlignment()
        {
            Assert.IsFalse(VideoPlayableHandle.RequiresFinalQualityAlignment(double.NaN));
            Assert.IsFalse(VideoPlayableHandle.RequiresFinalQualityAlignment(double.PositiveInfinity));
        }

        [Test]
        public void VideoPlayableHandle_WhenPreloading_UsesHighestRenditionAndFastStartPlayer()
        {
            VideoPlayableHandle handle = null;
            try
            {
                handle = new VideoPlayableHandle(
                    "https://cdn.example.com/master.m3u8",
                    new VideoPlayableOptions
                    {
                        SupportsAutoQuality = true,
                        QualityOptions = new[]
                        {
                            new VideoQualityOption(
                                "480P",
                                854,
                                480,
                                1000000,
                                "https://cdn.example.com/480P/index.m3u8"),
                            new VideoQualityOption(
                                "240P",
                                426,
                                240,
                                350000,
                                "https://cdn.example.com/240P/index.m3u8")
                        }
                    },
                    true);

                Assert.AreEqual("https://cdn.example.com/480P/index.m3u8", handle.Path);
                handle.Dispose();
                handle = null;

                using var fastStart = new AvProVideoPlayerInstance("Preload", null, false, false);
                Assert.AreEqual(
                    RenderHeads.Media.AVProVideo.Windows.VideoApi.MediaFoundation,
                    fastStart.Player.PlatformOptionsWindows.videoApi);
                Assert.IsTrue(fastStart.Player.PlatformOptionsWindows.useLowLatency);
                Assert.IsFalse(fastStart.PreferHighBitrate);
                Assert.IsFalse(fastStart.Player.PlatformOptionsAndroid.startWithHighestBitrate);
            }
            finally
            {
                handle?.Dispose();
            }
        }

        [Test]
        public void VideoPlayableHandle_WhenPreloadingWithTargetHeight_UsesTargetRendition()
        {
            VideoPlayableHandle handle = null;
            try
            {
                handle = new VideoPlayableHandle(
                    "https://cdn.example.com/master.m3u8",
                    new VideoPlayableOptions
                    {
                        SupportsAutoQuality = true,
                        PreloadTargetHeight = 240,
                        QualityOptions = new[]
                        {
                            new VideoQualityOption(
                                "480P",
                                854,
                                480,
                                1000000,
                                "https://cdn.example.com/480P/index.m3u8"),
                            new VideoQualityOption(
                                "240P",
                                426,
                                240,
                                350000,
                                "https://cdn.example.com/240P/index.m3u8")
                        }
                    },
                    true);

                Assert.AreEqual("https://cdn.example.com/240P/index.m3u8", handle.Path);
            }
            finally
            {
                handle?.Dispose();
            }
        }

        [Test]
        public void VideoPlayableHandle_WhenPreloadingWithFixedInitialQuality_KeepsFixedQuality()
        {
            VideoPlayableHandle handle = null;
            try
            {
                handle = new VideoPlayableHandle(
                    "https://cdn.example.com/master.m3u8",
                    new VideoPlayableOptions
                    {
                        SupportsAutoQuality = true,
                        PreloadTargetHeight = 480,
                        InitialQuality = new VideoQualitySelection(VideoQualityMode.FixedHeight, 480),
                        QualityOptions = new[]
                        {
                            new VideoQualityOption(
                                "480P",
                                854,
                                480,
                                1000000,
                                "https://cdn.example.com/480P/index.m3u8"),
                            new VideoQualityOption(
                                "240P",
                                426,
                                240,
                                350000,
                                "https://cdn.example.com/240P/index.m3u8")
                        }
                    },
                    true);

                Assert.AreEqual(VideoQualityMode.FixedHeight, handle.Quality.Mode);
                Assert.AreEqual(480, handle.Quality.Height);
                Assert.AreEqual("https://cdn.example.com/480P/index.m3u8", handle.Path);
            }
            finally
            {
                handle?.Dispose();
            }
        }

        private static string FindStoryVideoFixture()
        {
            var root = Path.Combine(Application.streamingAssetsPath, "videos");
            if (Directory.Exists(root) is false)
            {
                throw new DirectoryNotFoundException($"Story video fixture directory was not found: {root}");
            }

            var paths = Directory.GetFiles(root, "master.m3u8", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.Ordinal);
            if (paths.Length == 0)
            {
                throw new FileNotFoundException("No story HLS master playlist is available for Playable tests.", root);
            }

            return paths[0];
        }

    }
}

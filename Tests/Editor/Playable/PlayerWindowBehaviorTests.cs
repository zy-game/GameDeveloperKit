using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameDeveloperKit.Tests
{
    public sealed class PlayerWindowBehaviorTests
    {
        private static readonly FieldInfo s_VideoPlaybackField = typeof(VideoPlayerWindow).GetField(
            "m_Playback",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo s_ImagePlaybackField = typeof(ImagePlayerWindow).GetField(
            "m_Playback",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo s_SetPlayingMethod = typeof(PlayableHandle).GetMethod(
            "SetPlaying",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo s_RebuildQualityOptionsMethod = typeof(VideoPlayerWindow).GetMethod(
            "RebuildQualityOptions",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void VideoWindow_WhenPlayingBecomesIdle_HidesChromeAndDispatchesEachChangeOnce()
        {
            var window = new RecordingVideoPlayerWindow
            {
                ChromeAutoHideDelaySeconds = 1f
            };
            var playback = CreatePlayingVideoHandle();
            s_VideoPlaybackField.SetValue(window, playback);
            var eventCount = 0;
            window.ControlsVisibilityChanged += _ => eventCount++;

            try
            {
                window.OnUpdate(0f, 1.1f);

                Assert.IsFalse(window.AreControlsVisible);
                Assert.AreEqual(1, window.VisibilityHookCount);
                Assert.AreEqual(1, eventCount);

                window.SetControlsVisible(false);
                Assert.AreEqual(1, window.VisibilityHookCount);
                Assert.AreEqual(1, eventCount);

                window.ToggleControls();
                Assert.IsTrue(window.AreControlsVisible);
                Assert.AreEqual(2, window.VisibilityHookCount);
                Assert.AreEqual(2, eventCount);
            }
            finally
            {
                s_VideoPlaybackField.SetValue(window, null);
                playback.Dispose();
                window.Release();
            }
        }

        [Test]
        public void VideoWindow_WhenBackRequested_DispatchesHookAndEventOnce()
        {
            var window = new RecordingVideoPlayerWindow();
            var eventCount = 0;
            window.BackRequested += () => eventCount++;

            window.RequestBack();

            Assert.AreEqual(1, window.BackHookCount);
            Assert.AreEqual(1, eventCount);
        }

        [Test]
        public void VideoWindow_WhenUrlIsNotFinalHttps_RejectsBeforeOpeningPlayback()
        {
            var window = new RecordingVideoPlayerWindow();

            Assert.Throws<ArgumentException>(() => window.PlayAsync("story/chapter-1.m3u8").GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => window.PlayAsync("http://cdn.example.com/chapter-1.m3u8").GetAwaiter().GetResult());
        }

        [Test]
        public void VideoWindow_PrefabControls_ForwardPlaybackSeekSpeedAndQualityActions()
        {
            var window = CreateVideoWindow(out var instance);
            var playback = new VideoPlayableHandle(
                "https://cdn.example.com/720/index.m3u8",
                new VideoPlayableOptions
                {
                    DontDestroyOnLoad = false,
                    InitialQuality = new VideoQualitySelection(VideoQualityMode.FixedHeight, 720),
                    QualityOptions = new[]
                    {
                        new VideoQualityOption("720P", 1280, 720, 3000000, "https://cdn.example.com/720/index.m3u8"),
                        new VideoQualityOption("1080P", 1920, 1080, 6000000, "https://cdn.example.com/1080/index.m3u8")
                    }
                },
                false);
            s_SetPlayingMethod.Invoke(playback, null);
            s_VideoPlaybackField.SetValue(window, playback);
            var stateEventCount = 0;
            var speedEventCount = 0;
            window.PlaybackStateChanged += _ => stateEventCount++;
            window.PlaybackSpeedChanged += _ => speedEventCount++;

            try
            {
                window.Document.GetComponent<Button>("PlayPauseButton").onClick.Invoke();
                Assert.AreEqual(PlayableStatus.Paused, playback.Status);
                Assert.AreEqual(1, window.StateHookCount);
                Assert.AreEqual(1, stateEventCount);

                window.Document.GetComponent<Button>("PlayPauseButton").onClick.Invoke();
                Assert.AreEqual(PlayableStatus.Playing, playback.Status);
                Assert.AreEqual(2, window.StateHookCount);
                Assert.AreEqual(2, stateEventCount);

                window.Document.GetComponent<Button>("SpeedButton").onClick.Invoke();
                Assert.AreEqual(1.25f, playback.PlaybackRate, 0.001f);
                Assert.AreEqual(1, window.SpeedHookCount);
                Assert.AreEqual(1, speedEventCount);

                var progress = window.Document.GetComponent<Slider>("ProgressSlider");
                progress.maxValue = 120f;
                progress.SetValueWithoutNotify(0f);
                progress.value = 42f;
                Assert.AreEqual(1, window.SeekCallCount);
                Assert.AreEqual(42d, window.LastSeekSeconds, 0.001d);

                s_RebuildQualityOptionsMethod.Invoke(window, null);
                var quality1080 = window.Document.GetTarget("QualityOptionsRoot")
                    .transform.Find("Quality1080")
                    .GetComponent<Button>();
                quality1080.onClick.Invoke();
                Assert.AreEqual(1, window.QualityCallCount);
                Assert.AreEqual(VideoQualityMode.FixedHeight, window.LastQuality.Mode);
                Assert.AreEqual(1080, window.LastQuality.Height);
            }
            finally
            {
                s_VideoPlaybackField.SetValue(window, null);
                playback.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void VideoWindow_WhenSeekAndSpeedAreRestricted_DisablesControlsAndNormalizesRate()
        {
            var window = CreateVideoWindow(out var instance);
            var playback = new VideoPlayableHandle(
                "https://cdn.example.com/720/index.m3u8",
                new VideoPlayableOptions
                {
                    Seekable = true,
                    DontDestroyOnLoad = false
                },
                false);
            s_SetPlayingMethod.Invoke(playback, null);
            s_VideoPlaybackField.SetValue(window, playback);

            try
            {
                window.SetPlaybackSpeed(1.5f);
                Assert.AreEqual(1.5f, playback.PlaybackRate, 0.001f);

                window.SetSeekAndSpeedAllowed(false, false);

                Assert.IsFalse(window.SeekingAllowed);
                Assert.IsFalse(window.PlaybackSpeedAllowed);
                Assert.IsFalse(window.Document.GetComponent<Slider>("ProgressSlider").interactable);
                Assert.IsFalse(window.Document.GetComponent<Button>("SpeedButton").interactable);
                Assert.AreEqual(1f, playback.PlaybackRate, 0.001f);

                window.SetPlaybackSpeed(2f);
                Assert.AreEqual(1f, playback.PlaybackRate, 0.001f);

                window.SetSeekAndSpeedAllowed(true, true);
                Assert.IsTrue(window.Document.GetComponent<Slider>("ProgressSlider").interactable);
                Assert.IsTrue(window.Document.GetComponent<Button>("SpeedButton").interactable);
            }
            finally
            {
                s_VideoPlaybackField.SetValue(window, null);
                playback.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ImageWindow_WhenCurrentImageClicked_DispatchesHookAndEventOnce()
        {
            var window = new RecordingImagePlayerWindow();
            var texture = new Texture2D(2, 1);
            var playback = new ImagePlayableHandle("image-a", texture, _ => { }, null);
            playback.Start();
            window.SetImages(new[] { "image-a" });
            s_ImagePlaybackField.SetValue(window, playback);
            var eventCount = 0;
            window.ImageClicked += (_, _, _) => eventCount++;

            try
            {
                window.ClickCurrentImage();

                Assert.AreEqual(1, window.ClickHookCount);
                Assert.AreEqual(1, eventCount);
                Assert.AreEqual(0, window.LastClickedIndex);
                Assert.AreEqual("image-a", window.LastClickedLocation);
                Assert.AreSame(texture, window.LastClickedTexture);

                s_ImagePlaybackField.SetValue(window, null);
                window.ClickCurrentImage();
                Assert.AreEqual(1, window.ClickHookCount);
                Assert.AreEqual(1, eventCount);
            }
            finally
            {
                s_ImagePlaybackField.SetValue(window, null);
                playback.Dispose();
                window.Release();
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [UnityTest]
        public IEnumerator ImageWindow_WhenCarouselAdvanceIsPending_DoesNotQueueAnotherAdvance()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var window = new DeferredCarouselWindow();
                window.SetImages(new[] { "image-a", "image-b" });
                window.StartCarousel(0.5f);

                window.OnUpdate(0f, 0.6f);
                window.OnUpdate(0f, 0.6f);
                Assert.AreEqual(1, window.NextCallCount);

                window.CompleteNext();
                await UniTask.Yield();
                window.OnUpdate(0f, 0.6f);
                Assert.AreEqual(2, window.NextCallCount);

                window.StopCarousel();
                window.CompleteNext();
                await UniTask.Yield();
                window.Release();
            });
        }

        private static VideoPlayableHandle CreatePlayingVideoHandle()
        {
            var playback = new VideoPlayableHandle(
                "https://cdn.example.com/chapter-1.m3u8",
                new VideoPlayableOptions { DontDestroyOnLoad = false },
                false);
            s_SetPlayingMethod.Invoke(playback, null);
            return playback;
        }

        private static RecordingVideoPlayerWindow CreateVideoWindow(out GameObject instance)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Bundles/Playback/VideoPlayerWindow.prefab");
            Assert.IsNotNull(prefab);
            instance = UnityEngine.Object.Instantiate(prefab);
            var document = instance.GetComponent<UIDocument>();
            Assert.IsNotNull(document);
            var window = new RecordingVideoPlayerWindow();
            window.Initialize(document, instance, document.Layer);
            window.OnAwakeAsync().GetAwaiter().GetResult();
            return window;
        }

        private sealed class RecordingVideoPlayerWindow : VideoPlayerWindow
        {
            public int VisibilityHookCount { get; private set; }

            public int BackHookCount { get; private set; }

            public int StateHookCount { get; private set; }

            public int SpeedHookCount { get; private set; }

            public int SeekCallCount { get; private set; }

            public double LastSeekSeconds { get; private set; }

            public int QualityCallCount { get; private set; }

            public VideoQualitySelection LastQuality { get; private set; }

            protected override void OnControlsVisibilityChanged(bool visible)
            {
                VisibilityHookCount++;
            }

            protected override void OnBackRequested()
            {
                BackHookCount++;
            }

            protected override void OnPlaybackStateChanged(VideoPlayableHandle playback)
            {
                StateHookCount++;
            }

            protected override void OnPlaybackSpeedChanged(float rate)
            {
                SpeedHookCount++;
            }

            public override void Seek(double timeSeconds)
            {
                SeekCallCount++;
                LastSeekSeconds = timeSeconds;
            }

            public override UniTask SetQualityAsync(
                VideoQualitySelection selection,
                CancellationToken cancellationToken = default)
            {
                QualityCallCount++;
                LastQuality = selection;
                return UniTask.CompletedTask;
            }
        }

        private sealed class RecordingImagePlayerWindow : ImagePlayerWindow
        {
            public int ClickHookCount { get; private set; }

            public int LastClickedIndex { get; private set; } = -1;

            public string LastClickedLocation { get; private set; }

            public Texture LastClickedTexture { get; private set; }

            protected override void OnImageClicked(int index, string location, Texture texture)
            {
                ClickHookCount++;
                LastClickedIndex = index;
                LastClickedLocation = location;
                LastClickedTexture = texture;
            }
        }

        private sealed class DeferredCarouselWindow : ImagePlayerWindow
        {
            private UniTaskCompletionSource<ImagePlayableHandle> m_Next =
                new UniTaskCompletionSource<ImagePlayableHandle>();

            public int NextCallCount { get; private set; }

            public override UniTask<ImagePlayableHandle> NextAsync(CancellationToken cancellationToken = default)
            {
                NextCallCount++;
                return m_Next.Task;
            }

            public void CompleteNext()
            {
                var completion = m_Next;
                m_Next = new UniTaskCompletionSource<ImagePlayableHandle>();
                completion.TrySetResult(null);
            }
        }
    }
}

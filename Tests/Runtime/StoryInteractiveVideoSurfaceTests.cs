using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;
using GameDeveloperKit.Story.Protocol;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryInteractiveVideoSurfaceTests : RuntimeTestBase
    {
        [Test]
        public void PlaybackView_WhenChannelReturnsInsetVideoSurface_PreservesLayout()
        {
            var module = new StoryModule();
            PlaybackView view = null;
            var surfaceRootObject = new GameObject("InteractiveVideoSampleSurface", typeof(RectTransform));
            try
            {
                module.Startup();
                var videoObject = new GameObject("InsetVideo", typeof(RectTransform), typeof(RawImage));
                videoObject.transform.SetParent(surfaceRootObject.transform, false);
                var videoOutput = videoObject.GetComponent<RawImage>();
                var rect = videoOutput.rectTransform;
                rect.anchorMin = new Vector2(0.12f, 0.18f);
                rect.anchorMax = new Vector2(0.82f, 0.76f);
                rect.offsetMin = new Vector2(14f, 22f);
                rect.offsetMax = new Vector2(-18f, -26f);
                var expectedAnchorMin = rect.anchorMin;
                var expectedAnchorMax = rect.anchorMax;
                var expectedOffsetMin = rect.offsetMin;
                var expectedOffsetMax = rect.offsetMax;
                var channel = new InsetVideoChannel(new PlaybackSurfaceView(videoOutput: videoOutput));
                view = CreatePlaybackViewInstance();
                var storyModuleField = typeof(PlaybackView).GetField("m_StoryModule", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(storyModuleField);
                storyModuleField.SetValue(view, module);
                var presenterField = typeof(PlaybackView).GetField("m_Presenter", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(presenterField);
                var presenter = new Presenter(module, view);
                presenterField.SetValue(view, presenter);
                view.SetInteractionChannel(channel);

                view.Present(CreateVideoFrame(), presenter);

                Assert.AreEqual(1, channel.VideoRequestCount);
                Assert.AreSame(videoOutput, channel.LastVideoOutput);
                Assert.AreEqual(expectedAnchorMin, rect.anchorMin);
                Assert.AreEqual(expectedAnchorMax, rect.anchorMax);
                Assert.AreEqual(expectedOffsetMin, rect.offsetMin);
                Assert.AreEqual(expectedOffsetMax, rect.offsetMax);
                Assert.Less(rect.anchorMax.x - rect.anchorMin.x, 1f);
                Assert.Less(rect.anchorMax.y - rect.anchorMin.y, 1f);
            }
            finally
            {
                var viewObject = view?.GameObject;
                view?.Release();
                module.Shutdown();
                if (viewObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(viewObject);
                }
                UnityEngine.Object.DestroyImmediate(surfaceRootObject);
            }
        }

        private static Frame CreateVideoFrame()
        {
            var command = new global::GameDeveloperKit.Story.Model.Command(
                "interactive_surface_video",
                MediaCommandNames.PlayVideo,
                new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [MediaCommandNames.VideoSourceArgument] = Value.FromString(MediaCommandNames.VideoSourceStreamingAssets),
                    [MediaCommandNames.ClipArgument] = Value.FromString("videos/6.mp4"),
                    [MediaCommandNames.VideoSeekableArgument] = Value.FromBoolean(false),
                }),
                true,
                new[] { MediaCommandNames.CompletedOutcome });
            var step = new Step("interactive_surface_video", StepKind.Command, new StepData(command: command));
            var episode = StoryProgramTestFactory.Episode("episode_interactive_surface", "Interactive Surface", step.StepId, new[] { step });
            var program = StoryProgramTestFactory.Program("interactive_surface_sample", "1", episode.EpisodeId, new[] { episode });
            return new Frame(
                program,
                program.Volumes[0],
                episode,
                step,
                new[] { FrameTrack.CreateCommand(step) },
                null,
                false,
                true);
        }

        private sealed class InsetVideoChannel : IInteractionChannel
        {
            private readonly PlaybackSurfaceView m_Surface;

            public InsetVideoChannel(PlaybackSurfaceView surface)
            {
                m_Surface = surface;
            }

            public int VideoRequestCount { get; private set; }

            public RawImage LastVideoOutput { get; private set; }

            public UniTask OnAwake(InteractionContext context, CancellationToken cancellationToken)
            {
                return UniTask.CompletedTask;
            }

            public void OnStoryStarted(InteractionContext context)
            {
            }

            public void OnEpisodeChanged(EpisodeInteractionContext context)
            {
            }

            public void OnFrameChanged(Frame frame)
            {
            }

            public PlaybackSurfaceView GetPlaybackSurfaceView(InteractionRequest request)
            {
                if (request.Kind == InteractionRequestKind.Video)
                {
                    VideoRequestCount++;
                    LastVideoOutput = m_Surface.VideoOutput;
                }

                return m_Surface;
            }

            public void Tick(float deltaTime)
            {
            }

            public void OnStoryStopped()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}

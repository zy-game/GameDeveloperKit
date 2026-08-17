using System;
using GameDeveloperKit.Media;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;
using GameDeveloperKit.Story.Text;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryPlayableTests : RuntimeTestBase
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
        public void StoryPlaybackWindow_TypeBelongsToRuntimeAssembly()
        {
            Assert.AreEqual("GameDeveloperKit.Runtime", typeof(StoryPlaybackWindow).Assembly.GetName().Name);
            Assert.IsTrue(typeof(VideoPlayerWindow).IsAssignableFrom(typeof(StoryPlaybackWindow)));
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(typeof(StoryPlaybackWindow)));
        }

        [Test]
        public void PlayEpisodeVideoAsync_UsesCdnAndKeepsRunnerStateIsolated()
        {
            App.Shutdown().GetAwaiter().GetResult();
            var settings = new MediaDeliverySettings();
            settings.SetPublicUrls("https://origin.example.com", "https://cdn.example.com");
            var story = new StoryModule();
            story.Startup();
            var unlockEvents = 0;
            var subscription = App.Event.Subscribe<Story.Events.StoryUnlockEvent>(_ => unlockEvents++);
            VideoPlayableHandle playback = null;
            try
            {
                App.Config.LoadMediaDeliverySettings(_ => settings, new GdkSettings());
                _ = App.Playable;
                var currentEpisode = StoryProgramTestFactory.Episode(
                    "episode_current",
                    "Current",
                    "start",
                    new[]
                    {
                        new Step("start", StepKind.Start, new StepData(target: Target.Step("line"))),
                        new Step("line", StepKind.Line, new StepData(textKey: Literal("current")))
                    });
                var mediaEpisode = StoryProgramTestFactory.Episode(
                    "episode_media",
                    "Media",
                    "start",
                    new[]
                    {
                        new Step("start", StepKind.Start, new StepData(target: Target.Step("video"))),
                        VideoStep("video", "videos/story/chapter/master.m3u8")
                    });
                var program = StoryProgramTestFactory.Program(
                    "story_pure_video",
                    "1",
                    currentEpisode.EpisodeId,
                    new[] { currentEpisode, mediaEpisode });
                story.Register(program);
                var runner = story.StartEpisode(
                    program.StoryId,
                    StoryProgramTestFactory.VolumeId,
                    currentEpisode.EpisodeId);
                var runnerBefore = story.CurrentRunner;
                var frameBefore = story.CurrentFrame;
                var historyBefore = runner.History.Count;
                Assert.IsTrue(story.TryGetCurrentPosition(out var positionBefore));

                playback = story.PlayEpisodeVideoAsync(
                    program.StoryId,
                    StoryProgramTestFactory.VolumeId,
                    mediaEpisode.EpisodeId).GetAwaiter().GetResult();

                Assert.AreEqual(
                    "https://cdn.example.com/videos/story/chapter/master.m3u8",
                    playback.Path);
                Assert.AreSame(runnerBefore, story.CurrentRunner);
                Assert.AreSame(frameBefore, story.CurrentFrame);
                Assert.AreEqual(historyBefore, runner.History.Count);
                Assert.AreEqual(0, unlockEvents);
                Assert.IsTrue(story.TryGetCurrentPosition(out var positionAfter));
                Assert.AreEqual(positionBefore.StoryId, positionAfter.StoryId);
                Assert.AreEqual(positionBefore.VolumeId, positionAfter.VolumeId);
                Assert.AreEqual(positionBefore.EpisodeId, positionAfter.EpisodeId);
                Assert.AreEqual(positionBefore.StepId, positionAfter.StepId);
            }
            finally
            {
                playback?.Stop();
                playback?.Dispose();
                subscription.Cancel();
                story.Shutdown();

                App.Shutdown().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void PrewarmEpisode_WhenInitialFrameHasNoVideo_CompletesEmptySession()
        {
            var story = new StoryModule();
            var playable = CreateVideoPlayableModule();
            story.Startup();
            try
            {
                var episode = StoryProgramTestFactory.Episode(
                    "episode_empty",
                    "Empty",
                    "start",
                    new[]
                    {
                        new Step("start", StepKind.Start, new StepData(target: Target.Step("line"))),
                        new Step("line", StepKind.Line, new StepData(textKey: Literal("line")))
                    });
                story.Register(StoryProgramTestFactory.Program(
                    "story_empty", "1", episode.EpisodeId, new[] { episode }));

                using var session = EpisodeVideoPrewarmer.PrewarmEpisode(
                    story,
                    playable,
                    "story_empty",
                    StoryProgramTestFactory.VolumeId,
                    episode.EpisodeId);

                session.Completion.GetAwaiter().GetResult();
                Assert.AreEqual(0, session.VideoCount);
            }
            finally
            {
                story.Shutdown();
                playable.Shutdown();
            }
        }

        [Test]
        public void CollectInitialVideoInstructions_WhenVideoIsLater_ReturnsEmpty()
        {
            var story = new StoryModule();
            story.Startup();
            try
            {
                var episode = StoryProgramTestFactory.Episode(
                    "episode",
                    "Episode",
                    "start",
                    new[]
                    {
                        new Step("start", StepKind.Start, new StepData(target: Target.Step("line"))),
                        new Step("line", StepKind.Line, new StepData(
                            textKey: Literal("line"),
                            target: Target.Step("video"))),
                        VideoStep("video", "videos/story/later/master.m3u8")
                    });
                var program = StoryProgramTestFactory.Program(
                    "story_initial", "1", episode.EpisodeId, new[] { episode });
                story.Register(program);

                var instructions = EpisodeVideoPrewarmer.CollectInitialVideoInstructions(
                    story,
                    program.StoryId,
                    program,
                    StoryProgramTestFactory.VolumeId,
                    episode.EpisodeId);

                Assert.AreEqual(0, instructions.Count);
            }
            finally
            {
                story.Shutdown();
            }
        }

        [Test]
        public void FindNextVideoInstruction_WhenPathIsDeterministic_ReturnsTypedInstruction()
        {
            var current = new Step("line", StepKind.Line, new StepData(
                textKey: Literal("line"),
                target: Target.Step("video")));
            var episode = StoryProgramTestFactory.Episode(
                "episode",
                "Episode",
                "start",
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step(current.StepId))),
                    current,
                    VideoStep("video", "videos/story/next/master.m3u8")
                });

            var instruction = EpisodeVideoPrewarmer.FindNextVideoInstruction(episode, current);

            Assert.IsNotNull(instruction);
            Assert.AreEqual("video", instruction.InstructionId);
            Assert.AreEqual("videos/story/next/master.m3u8", instruction.Reference.Primary.Value);
        }

        [Test]
        public void FindNextVideoInstruction_WhenPathReachesChoice_ReturnsNull()
        {
            var current = new Step("line", StepKind.Line, new StepData(
                textKey: Literal("line"),
                target: Target.Step("choice")));
            var choice = new Choice("choice_a", "exit_a", Literal("A"));
            var episode = StoryProgramTestFactory.Episode(
                "episode",
                "Episode",
                "start",
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step(current.StepId))),
                    current,
                    new Step("choice", StepKind.Choice, new StepData(choices: new[] { choice }))
                });

            Assert.IsNull(EpisodeVideoPrewarmer.FindNextVideoInstruction(episode, current));
        }

        [Test]
        public void FindNextVideoInstruction_WhenPathRunsThroughParallelBranch_ReturnsBranchVideo()
        {
            var current = new Step("line", StepKind.Line, new StepData(
                textKey: Literal("line"),
                target: Target.Step("parallel")));
            var episode = StoryProgramTestFactory.Episode(
                "episode",
                "Episode",
                "start",
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step(current.StepId))),
                    current,
                    new Step("parallel", StepKind.Parallel, new StepData(
                        branches: new[]
                        {
                            new ParallelBranch("branch_1", "轨道 1", Target.Step("video_a")),
                            new ParallelBranch("branch_2", "轨道 2", Target.Step("video_b"))
                        })),
                    VideoStep("video_a", "videos/story/parallel-a/master.m3u8"),
                    VideoStep("video_b", "videos/story/parallel-b/master.m3u8")
                });

            var instruction = EpisodeVideoPrewarmer.FindNextVideoInstruction(episode, current);

            Assert.IsNotNull(instruction);
            Assert.AreEqual("videos/story/parallel-a/master.m3u8", instruction.Reference.Primary.Value);
        }

        [Test]
        public void FindNextVideoInstruction_WhenParallelBranchReachesTransition_FollowsBranchExit()
        {
            var current = new Step("line", StepKind.Line, new StepData(
                textKey: Literal("line"),
                target: Target.Step("parallel")));
            var episode = StoryProgramTestFactory.Episode(
                "episode",
                "Episode",
                "start",
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step(current.StepId))),
                    current,
                    new Step("parallel", StepKind.Parallel, new StepData(
                        branches: new[]
                        {
                            new ParallelBranch("branch_1", "轨道 1", Target.Step("exit_step"))
                        })),
                    new Step("exit_step", StepKind.Transition, new StepData(exitId: "branch_exit"))
                });

            var instruction = EpisodeVideoPrewarmer.FindNextVideoInstruction(episode, current, out var transitionExitId);

            Assert.IsNull(instruction);
            Assert.AreEqual("branch_exit", transitionExitId);
        }

        [Test]
        public void FindChoiceVideoInstruction_WhenChoiceRoutesToVideo_ReturnsTargetInstruction()
        {
            var choice = new Choice("choice_a", "exit_a", Literal("A"));
            var source = new Episode(
                "source",
                "Source",
                "start",
                new[] { new EpisodeExit("exit_a") },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("choice"))),
                    new Step("choice", StepKind.Choice, new StepData(choices: new[] { choice }))
                });
            var target = StoryProgramTestFactory.Episode(
                "target",
                "Target",
                "start",
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("video"))),
                    VideoStep("video", "videos/story/choice/master.m3u8")
                });
            var volume = new Volume(
                "volume",
                "Volume",
                new[] { source, target },
                new Route(new[]
                {
                    RouteEdge.FromRoot("root", source.EpisodeId),
                    RouteEdge.FromExit("choice_route", source.EpisodeId, choice.ExitId, target.EpisodeId)
                }));
            var frame = new Runner(new Program("story_choice", "1", new[] { volume }))
                .Start(volume.VolumeId, source.EpisodeId);

            var instruction = EpisodeVideoPrewarmer.FindChoiceVideoInstruction(frame, choice.ChoiceId);

            Assert.IsNotNull(instruction);
            Assert.AreEqual("videos/story/choice/master.m3u8", instruction.Reference.Primary.Value);
        }

        [Test]
        public void CollectVideoRequests_UsesCdnAndDeduplicatesPaths()
        {
            var settings = new MediaDeliverySettings();
            settings.SetPublicUrls("https://origin.example.com", "https://cdn.example.com");
            try
            {
                var first = StoryProgramTestFactory.Episode(
                    "first",
                    "First",
                    "start",
                    new[]
                    {
                        new Step("start", StepKind.Start, new StepData(target: Target.Step("video_a"))),
                        VideoStep("video_a", "videos/shared/master.m3u8")
                    });
                var second = StoryProgramTestFactory.Episode(
                    "second",
                    "Second",
                    "start",
                    new[]
                    {
                        new Step("start", StepKind.Start, new StepData(target: Target.Step("video_b"))),
                        VideoStep("video_b", "videos/shared/master.m3u8"),
                        VideoStep("video_c", "videos/unique/master.m3u8")
                    });
                var volume = new Volume(
                    "volume",
                    "Volume",
                    new[] { first, second },
                    new Route(new[]
                    {
                        RouteEdge.FromRoot("root_first", first.EpisodeId),
                        RouteEdge.FromRoot("root_second", second.EpisodeId)
                    }));

                var requests = VolumeVideoPrewarmer.CollectVideoRequests(volume, settings);

                Assert.AreEqual(2, requests.Count);
                Assert.AreEqual("https://cdn.example.com/videos/shared/master.m3u8", requests[0].Path);
                Assert.AreEqual("https://cdn.example.com/videos/unique/master.m3u8", requests[1].Path);
            }
            finally
            {

            }
        }

        [Test]
        public void PrewarmVolume_WhenVolumeDoesNotExist_RejectsWithContext()
        {
            var story = new StoryModule();
            var playable = CreateVideoPlayableModule();
            story.Startup();
            try
            {
                var episode = StoryProgramTestFactory.Episode(
                    "episode",
                    "Episode",
                    "start",
                    new[]
                    {
                        new Step("start", StepKind.Start, new StepData(target: Target.Step("line"))),
                        new Step("line", StepKind.Line, new StepData(textKey: Literal("line")))
                    });
                story.Register(StoryProgramTestFactory.Program(
                    "story_missing_volume", "1", episode.EpisodeId, new[] { episode }));

                var exception = Assert.Throws<GameException>(() =>
                    VolumeVideoPrewarmer.PrewarmVolume(
                        story,
                        playable,
                        "story_missing_volume",
                        "missing_volume"));

                StringAssert.Contains("story:story_missing_volume", exception.Message);
                StringAssert.Contains("volume:missing_volume", exception.Message);
            }
            finally
            {
                story.Shutdown();
                playable.Shutdown();
            }
        }

        private static Step VideoStep(string stepId, string path, Target target = null)
        {
            return new Step(
                stepId,
                StepKind.PlayVideo,
                new StepData(
                    videoReference: new VideoReference(new MediaPath(path), VideoFormat.Hls),
                    target: target));
        }

        private static string Literal(string value)
        {
            return TextReferenceCodec.Serialize(new TextReference(TextMode.Literal, value));
        }

        private static PlayableModule CreateVideoPlayableModule()
        {
            var module = new PlayableModule();
            module.Register(new VideoPlayable());
            return module;
        }
    }
}

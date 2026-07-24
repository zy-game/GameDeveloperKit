using System;
using System.Collections.Generic;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Execution;
using NUnit.Framework;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Playback;

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
        public void CanHandle_WhenMediaCommand_ReturnsTrue()
        {
            using var playable = new MediaCommandHandler(App.Playable, null, null);

            Assert.IsTrue(playable.CanHandle(CreateCommand("video", MediaCommandNames.PlayVideo, MediaCommandNames.ClipArgument, "clip")));
            Assert.IsTrue(playable.CanHandle(CreateCommand("image", MediaCommandNames.ShowImage, MediaCommandNames.ImageArgument, "image")));
            Assert.IsTrue(playable.CanHandle(CreateCommand("audio", MediaCommandNames.PlayAudio, MediaCommandNames.ClipArgument, "audio")));
            Assert.IsFalse(playable.CanHandle(new global::GameDeveloperKit.Story.Model.Command("event", "emit_event")));
        }

        [Test]
        public void Execute_WhenVideoPathIsInvalid_FailsStoryHandle()
        {
            using var playable = new MediaCommandHandler(App.Playable, null, null);
            var command = CreateCommand(
                "video",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "relative.mp4",
                MediaCommandNames.VideoSourceNetworkStream);

            var handle = playable.Execute(command, default);

            Assert.AreSame(command, handle.Command);
            Assert.IsNotNull(handle.Error);
            StringAssert.Contains("path is invalid", handle.Error.Message);
        }

        [Test]
        public void PlaybackView_TypeBelongsToRuntimeAssembly()
        {
            Assert.AreEqual("GameDeveloperKit.Runtime", typeof(PlaybackView).Assembly.GetName().Name);
            Assert.IsTrue(typeof(GameDeveloperKit.UI.UIWindow).IsAssignableFrom(typeof(PlaybackView)));
            Assert.IsFalse(typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(typeof(PlaybackView)));
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
                        new Step("line", StepKind.Line, new StepData(textKey: "line"))
                    });
                story.Register(StoryProgramTestFactory.Program(
                    "story_empty",
                    "1",
                    episode.EpisodeId,
                    new[] { episode }));

                using var session = EpisodeVideoPrewarmer.PrewarmEpisode(
                    story,
                    playable,
                    "story_empty",
                    StoryProgramTestFactory.VolumeId,
                    episode.EpisodeId);

                session.Completion.GetAwaiter().GetResult();
                Assert.AreEqual(0, session.VideoCount);
                Assert.AreEqual(episode.EpisodeId, session.EpisodeId);
            }
            finally
            {
                story.Shutdown();
                playable.Shutdown();
            }
        }

        [Test]
        public void PrewarmEpisode_WhenVolumeDoesNotExist_RejectsWithContext()
        {
            var story = new StoryModule();
            var playable = CreateVideoPlayableModule();
            story.Startup();
            try
            {
                var episode = StoryProgramTestFactory.Episode(
                    "episode_missing_volume",
                    "Episode",
                    "start",
                    new[]
                    {
                        new Step("start", StepKind.Start, new StepData(target: Target.Step("line"))),
                        new Step("line", StepKind.Line, new StepData(textKey: "line"))
                    });
                story.Register(StoryProgramTestFactory.Program(
                    "story_missing_volume",
                    "1",
                    episode.EpisodeId,
                    new[] { episode }));

                var exception = Assert.Throws<GameException>(() =>
                    EpisodeVideoPrewarmer.PrewarmEpisode(
                        story,
                        playable,
                        "story_missing_volume",
                        "missing_volume",
                        episode.EpisodeId));

                StringAssert.Contains("story:story_missing_volume", exception.Message);
                StringAssert.Contains("volume:missing_volume", exception.Message);
                StringAssert.Contains($"episode:{episode.EpisodeId}", exception.Message);
            }
            finally
            {
                story.Shutdown();
                playable.Shutdown();
            }
        }

        [Test]
        public void CollectInitialVideoCommands_WhenLaterVideoExists_ReturnsOnlyInitialVideo()
        {
            var initial = CreateCommand(
                "video_initial",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "initial.mp4");
            var later = CreateCommand(
                "video_later",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "later.mp4");
            var episode = StoryProgramTestFactory.Episode(
                "episode_videos",
                "Videos",
                "start",
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("video_initial"))),
                    new Step(
                        "video_initial",
                        StepKind.Command,
                        new StepData(command: initial, target: Target.Step("video_later"))),
                    new Step("video_later", StepKind.Command, new StepData(command: later))
                });
            var program = StoryProgramTestFactory.Program(
                "story_videos",
                "1",
                episode.EpisodeId,
                new[] { episode });
            var story = new StoryModule();
            story.Startup();
            try
            {
                var commands = EpisodeVideoPrewarmer.CollectInitialVideoCommands(
                    story,
                    program.StoryId,
                    program,
                    StoryProgramTestFactory.VolumeId,
                    episode.EpisodeId);

                Assert.AreEqual(1, commands.Count);
                Assert.AreSame(initial, commands[0]);
            }
            finally
            {
                story.Shutdown();
            }
        }

        [Test]
        public void CollectVideoRequests_WhenVolumeContainsLaterAndRepeatedVideos_ReturnsAllUniquePaths()
        {
            var initial = CreateCommand(
                "video_initial",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "initial.mp4");
            var later = CreateCommand(
                "video_later",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "later.mp4");
            var repeated = CreateCommand(
                "video_repeated",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "later.mp4");
            var audio = CreateCommand(
                "audio_ignored",
                MediaCommandNames.PlayAudio,
                MediaCommandNames.ClipArgument,
                "audio.mp3");
            var firstEpisode = StoryProgramTestFactory.Episode(
                "episode_first",
                "First",
                "start",
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("video_initial"))),
                    new Step(
                        "video_initial",
                        StepKind.Command,
                        new StepData(command: initial, target: Target.Step("video_later"))),
                    new Step("video_later", StepKind.Command, new StepData(command: later))
                });
            var secondEpisode = StoryProgramTestFactory.Episode(
                "episode_second",
                "Second",
                "video_repeated",
                new[]
                {
                    new Step(
                        "video_repeated",
                        StepKind.Command,
                        new StepData(command: repeated, target: Target.Step("audio_ignored"))),
                    new Step("audio_ignored", StepKind.Command, new StepData(command: audio))
                });
            var program = StoryProgramTestFactory.Program(
                "story_volume_videos",
                "1",
                firstEpisode.EpisodeId,
                new[] { firstEpisode, secondEpisode });

            var commands = VolumeVideoPrewarmer.CollectVideoCommands(program.Volumes[0]);
            var requests = VolumeVideoPrewarmer.CollectVideoRequests(program.Volumes[0]);

            CollectionAssert.AreEqual(new[] { initial, later, repeated }, commands);
            Assert.AreEqual(2, requests.Count);
            Assert.AreEqual(
                VideoPathResolver.Resolve(MediaCommandNames.VideoSourceStreamingAssets, "initial.mp4"),
                requests[0].Path);
            Assert.AreEqual(
                VideoPathResolver.Resolve(MediaCommandNames.VideoSourceStreamingAssets, "later.mp4"),
                requests[1].Path);
        }

        [Test]
        public void FindNextVideoCommand_WhenPathIsDeterministic_ReturnsOnlyNextVideo()
        {
            var current = CreateCommand(
                "video_current",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "current.mp4");
            var next = CreateCommand(
                "video_next",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "next.mp4");
            var currentStep = new Step(
                "video_current",
                StepKind.Command,
                new StepData(command: current, target: Target.Step("wait")));
            var episode = StoryProgramTestFactory.Episode(
                "episode_lookahead",
                "Episode",
                currentStep.StepId,
                new[]
                {
                    currentStep,
                    new Step("wait", StepKind.Wait, new StepData(waitSeconds: 1d, target: Target.Step("video_next"))),
                    new Step("video_next", StepKind.Command, new StepData(command: next))
                });

            Assert.AreSame(next, EpisodeVideoPrewarmer.FindNextVideoCommand(episode, currentStep));
        }

        [Test]
        public void FindNextVideoCommand_WhenPathBranches_DoesNotPredictVideo()
        {
            var current = CreateCommand(
                "video_current",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "current.mp4");
            var next = CreateCommand(
                "video_next",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "next.mp4");
            var currentStep = new Step(
                "video_current",
                StepKind.Command,
                new StepData(command: current, target: Target.Step("branch")));
            var episode = StoryProgramTestFactory.Episode(
                "episode_branch_lookahead",
                "Episode",
                currentStep.StepId,
                new[]
                {
                    currentStep,
                    new Step("branch", StepKind.Branch, new StepData(target: Target.Step("video_next"))),
                    new Step("video_next", StepKind.Command, new StepData(command: next))
                });

            Assert.IsNull(EpisodeVideoPrewarmer.FindNextVideoCommand(episode, currentStep));
        }

        [Test]
        public void FindNextVideoCommand_WhenTransitionRoutesToParallelVideo_ReturnsRoutedVideo()
        {
            var current = CreateCommand(
                "video_current",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "current.mp4");
            var next = CreateCommand(
                "video_next",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "next.mp4");
            var currentStep = new Step(
                "video_current",
                StepKind.Command,
                new StepData(command: current, target: Target.Step("transition")));
            var firstEpisode = new Episode(
                "episode_first",
                "First",
                currentStep.StepId,
                new[] { new EpisodeExit("to_next") },
                new[]
                {
                    currentStep,
                    new Step("transition", StepKind.Transition, new StepData(exitId: "to_next"))
                });
            var secondEpisode = new Episode(
                "episode_second",
                "Second",
                "start",
                Array.Empty<EpisodeExit>(),
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("parallel"))),
                    new Step(
                        "parallel",
                        StepKind.Parallel,
                        new StepData(
                            branches: new[]
                            {
                                new ParallelBranch("video", "Video", Target.Step("video_next")),
                                new ParallelBranch("wait", "Wait", Target.Step("wait"))
                            })),
                    new Step("video_next", StepKind.Command, new StepData(command: next)),
                    new Step("wait", StepKind.Wait, new StepData(waitSeconds: 9d))
                });
            var volume = new Volume(
                StoryProgramTestFactory.VolumeId,
                "Volume",
                new[] { firstEpisode, secondEpisode },
                new Route(new[]
                {
                    RouteEdge.FromRoot("root", firstEpisode.EpisodeId),
                    RouteEdge.FromExit(
                        "route_next",
                        firstEpisode.EpisodeId,
                        "to_next",
                        secondEpisode.EpisodeId)
                }));
            var program = new Program("story_route_lookahead", "1", new[] { volume });
            var frame = new Runner(program).Start(volume.VolumeId, firstEpisode.EpisodeId);

            Assert.AreSame(next, EpisodeVideoPrewarmer.FindNextVideoCommand(frame, currentStep));
        }

        [Test]
        public void FindChoiceVideoCommand_WhenChoicesRouteToDifferentVideos_ReturnsSelectedVideo()
        {
            var first = CreateCommand(
                "video_first_choice",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "first-choice.mp4");
            var second = CreateCommand(
                "video_second_choice",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "second-choice.mp4");
            var choiceEpisode = new Episode(
                "episode_choice",
                "Choice",
                "choice",
                new[] { new EpisodeExit("first"), new EpisodeExit("second") },
                new[]
                {
                    new Step(
                        "choice",
                        StepKind.Choice,
                        new StepData(choices: new[]
                        {
                            new Choice("choose_first", "first", "First"),
                            new Choice("choose_second", "second", "Second")
                        }))
                });
            var firstEpisode = new Episode(
                "episode_first_choice",
                "First",
                "video",
                Array.Empty<EpisodeExit>(),
                new[] { new Step("video", StepKind.Command, new StepData(command: first)) });
            var secondEpisode = new Episode(
                "episode_second_choice",
                "Second",
                "start",
                Array.Empty<EpisodeExit>(),
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("parallel"))),
                    new Step(
                        "parallel",
                        StepKind.Parallel,
                        new StepData(branches: new[]
                        {
                            new ParallelBranch("video", "Video", Target.Step("video")),
                            new ParallelBranch("wait", "Wait", Target.Step("wait"))
                        })),
                    new Step("video", StepKind.Command, new StepData(command: second)),
                    new Step("wait", StepKind.Wait, new StepData(waitSeconds: 1d))
                });
            var volume = new Volume(
                StoryProgramTestFactory.VolumeId,
                "Volume",
                new[] { choiceEpisode, firstEpisode, secondEpisode },
                new Route(new[]
                {
                    RouteEdge.FromRoot("root", choiceEpisode.EpisodeId),
                    RouteEdge.FromExit("first", choiceEpisode.EpisodeId, "first", firstEpisode.EpisodeId),
                    RouteEdge.FromExit("second", choiceEpisode.EpisodeId, "second", secondEpisode.EpisodeId)
                }));
            var program = new Program("story_choice_lookahead", "1", new[] { volume });
            var frame = new Runner(program).Start(volume.VolumeId, choiceEpisode.EpisodeId);

            Assert.AreSame(first, EpisodeVideoPrewarmer.FindChoiceVideoCommand(frame, "choose_first"));
            Assert.AreSame(second, EpisodeVideoPrewarmer.FindChoiceVideoCommand(frame, "choose_second"));
            CollectionAssert.AreEqual(
                new[] { first, second },
                EpisodeVideoPrewarmer.CollectChoiceVideoCommands(frame));
        }

        [Test]
        public void FindChoiceVideoCommand_WhenChoiceOrVideoRouteIsMissing_ReturnsNull()
        {
            var choiceEpisode = new Episode(
                "episode_choice_missing",
                "Choice",
                "choice",
                new[] { new EpisodeExit("without_video"), new EpisodeExit("without_route") },
                new[]
                {
                    new Step(
                        "choice",
                        StepKind.Choice,
                        new StepData(choices: new[]
                        {
                            new Choice("choose_without_video", "without_video", "Without video"),
                            new Choice("choose_without_route", "without_route", "Without route")
                        }))
                });
            var lineEpisode = new Episode(
                "episode_line",
                "Line",
                "line",
                Array.Empty<EpisodeExit>(),
                new[] { new Step("line", StepKind.Line, new StepData(textKey: "Line")) });
            var volume = new Volume(
                StoryProgramTestFactory.VolumeId,
                "Volume",
                new[] { choiceEpisode, lineEpisode },
                new Route(new[]
                {
                    RouteEdge.FromRoot("root", choiceEpisode.EpisodeId),
                    RouteEdge.FromExit(
                        "without_video",
                        choiceEpisode.EpisodeId,
                        "without_video",
                        lineEpisode.EpisodeId)
                }));
            var program = new Program("story_choice_without_video", "1", new[] { volume });
            var frame = new Runner(program).Start(volume.VolumeId, choiceEpisode.EpisodeId);

            Assert.IsNull(EpisodeVideoPrewarmer.FindChoiceVideoCommand(frame, "missing"));
            Assert.IsNull(EpisodeVideoPrewarmer.FindChoiceVideoCommand(frame, "choose_without_video"));
            Assert.IsNull(EpisodeVideoPrewarmer.FindChoiceVideoCommand(frame, "choose_without_route"));
            Assert.AreEqual(0, EpisodeVideoPrewarmer.CollectChoiceVideoCommands(frame).Count);
        }

        [Test]
        public void CollectChoiceVideoCommands_WhenChoiceIsLaterInEpisode_ReturnsAllChoiceVideos()
        {
            var intro = CreateCommand(
                "video_intro",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "intro.mp4");
            var first = CreateCommand(
                "video_later_first",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "later-first.mp4");
            var second = CreateCommand(
                "video_later_second",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "later-second.mp4");
            var sourceEpisode = new Episode(
                "episode_choice_after_video",
                "Source",
                "intro",
                new[] { new EpisodeExit("first"), new EpisodeExit("second") },
                new[]
                {
                    new Step(
                        "intro",
                        StepKind.Command,
                        new StepData(command: intro, target: Target.Step("choice"))),
                    new Step(
                        "choice",
                        StepKind.Choice,
                        new StepData(choices: new[]
                        {
                            new Choice("later_first", "first", "First"),
                            new Choice("later_second", "second", "Second")
                        }))
                });
            var firstEpisode = new Episode(
                "episode_later_first",
                "First",
                "video",
                Array.Empty<EpisodeExit>(),
                new[] { new Step("video", StepKind.Command, new StepData(command: first)) });
            var secondEpisode = new Episode(
                "episode_later_second",
                "Second",
                "video",
                Array.Empty<EpisodeExit>(),
                new[] { new Step("video", StepKind.Command, new StepData(command: second)) });
            var volume = new Volume(
                StoryProgramTestFactory.VolumeId,
                "Volume",
                new[] { sourceEpisode, firstEpisode, secondEpisode },
                new Route(new[]
                {
                    RouteEdge.FromRoot("root", sourceEpisode.EpisodeId),
                    RouteEdge.FromExit("first", sourceEpisode.EpisodeId, "first", firstEpisode.EpisodeId),
                    RouteEdge.FromExit("second", sourceEpisode.EpisodeId, "second", secondEpisode.EpisodeId)
                }));
            var program = new Program("story_later_choice_lookahead", "1", new[] { volume });
            var frame = new Runner(program).Start(volume.VolumeId, sourceEpisode.EpisodeId);

            Assert.AreSame(intro, frame.Tracks[0].Command);
            Assert.AreEqual(0, frame.Choices.Count);
            CollectionAssert.AreEqual(
                new[] { first, second },
                EpisodeVideoPrewarmer.CollectChoiceVideoCommands(frame));
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
                    "episode_volume_missing",
                    "Episode",
                    "line",
                    new[] { new Step("line", StepKind.Line, new StepData(textKey: "line")) });
                story.Register(StoryProgramTestFactory.Program(
                    "story_volume_missing",
                    "1",
                    episode.EpisodeId,
                    new[] { episode }));

                var exception = Assert.Throws<GameException>(() =>
                    VolumeVideoPrewarmer.PrewarmVolume(
                        story,
                        playable,
                        "story_volume_missing",
                        "missing_volume"));

                StringAssert.Contains("story:story_volume_missing", exception.Message);
                StringAssert.Contains("volume:missing_volume", exception.Message);
            }
            finally
            {
                story.Shutdown();
                playable.Shutdown();
            }
        }

        private static global::GameDeveloperKit.Story.Model.Command CreateCommand(
            string id,
            string name,
            string argument,
            string value,
            string videoSource = null)
        {
            var values = new Dictionary<string, Value>(StringComparer.Ordinal)
            {
                [argument] = Value.FromString(value)
            };
            if (videoSource != null)
            {
                values[MediaCommandNames.VideoSourceArgument] = Value.FromString(videoSource);
            }

            return new global::GameDeveloperKit.Story.Model.Command(id, name, new ArgumentBag(values));
        }

        private static PlayableModule CreateVideoPlayableModule()
        {
            var module = new PlayableModule();
            module.Register(new VideoPlayable());
            return module;
        }
    }
}

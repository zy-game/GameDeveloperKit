using System;
using System.Collections.Generic;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story;
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

using System;
using System.Collections.Generic;
using GameDeveloperKit.Media;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryModuleContractTests
    {
        [SetUp]
        public void SetUp()
        {
            App.Shutdown().GetAwaiter().GetResult();
        }

        [TearDown]
        public void TearDown()
        {
            App.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void FrameInstructions_WhenCommandIsNotBuiltIn_LeavesRunnerCommandUntouched()
        {
            var command = new global::GameDeveloperKit.Story.Model.Command(
                "mini_game",
                "mini_game",
                waitForCompletion: true,
                outcomePorts: new[] { "success" });
            var step = new Step("mini_game", StepKind.Command, new StepData(command: command));
            var episode = new Episode(
                "episode",
                "Episode",
                step.StepId,
                Array.Empty<EpisodeExit>(),
                new[] { step });
            var volume = new Volume("volume", "Volume", new[] { episode }, new Route());
            var program = new Program("story", "1", new[] { volume });

            var frame = Frame.CreateCommand(program, volume, episode, step, true);

            Assert.AreSame(command, frame.Tracks[0].Command);
            Assert.AreEqual(0, frame.Instructions.Count);
            Assert.IsTrue(frame.WaitsForCommand);
        }

        [Test]
        public void QueryContracts_ReturnReadOnlySnapshotsAndDoNotAdvanceRunner()
        {
            var module = new StoryModule();
            module.Startup();
            try
            {
                var program = CreateProgram(out var currentEpisode, out var mediaEpisode);
                module.Register(program);
                var runner = module.StartEpisode(
                    program.StoryId,
                    program.Volumes[0].VolumeId,
                    currentEpisode.EpisodeId);
                var runnerBefore = module.CurrentRunner;
                var frameBefore = module.CurrentFrame;
                var historyBefore = runner.History.Count;
                Assert.IsTrue(module.TryGetCurrentPosition(out var positionBefore));

                var volumes = module.GetVolumes(program.StoryId);
                var episodes = module.GetEpisodes(program.StoryId, program.Volumes[0].VolumeId);
                var cues = module.GetEpisodeMediaCues(
                    program.StoryId,
                    program.Volumes[0].VolumeId,
                    mediaEpisode.EpisodeId);

                Assert.AreEqual(1, volumes.Count);
                Assert.AreEqual(2, episodes.Count);
                Assert.Throws<NotSupportedException>(() =>
                    ((IList<Volume>)volumes)[0] = program.Volumes[0]);
                Assert.Throws<NotSupportedException>(() =>
                    ((IList<Episode>)episodes)[0] = currentEpisode);
                Assert.AreEqual(3, cues.Count);
                Assert.IsInstanceOf<StoryInstruction.PlayVideo>(cues[0].Instruction);
                Assert.IsInstanceOf<StoryInstruction.ShowImage>(cues[1].Instruction);
                Assert.IsInstanceOf<StoryInstruction.PlayAudio>(cues[2].Instruction);
                Assert.AreEqual("video", cues[0].StepId);
                Assert.AreSame(mediaEpisode, module.GetEpisode(
                    program.StoryId,
                    program.Volumes[0].VolumeId,
                    mediaEpisode.EpisodeId));

                Assert.AreSame(runnerBefore, module.CurrentRunner);
                Assert.AreSame(frameBefore, module.CurrentFrame);
                Assert.AreEqual(historyBefore, runner.History.Count);
                Assert.IsTrue(module.TryGetCurrentPosition(out var positionAfter));
                AssertPosition(positionBefore, positionAfter);
            }
            finally
            {
                module.Shutdown();
            }
        }

        private static Program CreateProgram(out Episode currentEpisode, out Episode mediaEpisode)
        {
            currentEpisode = new Episode(
                "current",
                "Current",
                "start",
                Array.Empty<EpisodeExit>(),
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("line"))),
                    new Step("line", StepKind.Line, new StepData(textKey: "current.line"))
                });

            var videoReference = new VideoReference(
                new MediaPath("videos/story/chapter/master.m3u8"),
                VideoFormat.Hls,
                new[]
                {
                    new VideoRendition(
                        "720P",
                        new MediaPath("videos/story/chapter/720P/index.m3u8"),
                        1280,
                        720,
                        3000000,
                        90000)
                });
            var video = new global::GameDeveloperKit.Story.Model.Command(
                "video",
                MediaCommandNames.PlayVideo,
                new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [MediaCommandNames.ClipArgument] = Value.FromString(videoReference.Primary.Value),
                    [MediaCommandNames.VideoFormatArgument] = Value.FromString("hls"),
                    [MediaCommandNames.VideoRenditionsArgument] = Value.FromString(
                        VideoReferenceCodec.SerializeRenditions(videoReference.Renditions))
                }));
            var image = new global::GameDeveloperKit.Story.Model.Command(
                "image",
                MediaCommandNames.ShowImage,
                new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [MediaCommandNames.ImageArgument] = Value.FromString("Images/ChapterCover")
                }));
            var audio = new global::GameDeveloperKit.Story.Model.Command(
                "audio",
                MediaCommandNames.PlayAudio,
                new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [MediaCommandNames.MediaSourceArgument] = Value.FromString(
                        MediaCommandNames.MediaSourceResource),
                    [MediaCommandNames.ClipArgument] = Value.FromString("Audio/ChapterBgm")
                }));
            var unlock = new global::GameDeveloperKit.Story.Model.Command(
                "unlock",
                StoryCommandNames.Unlock,
                new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [StoryCommandNames.UnlockIdArgument] = Value.FromString("chapter-2")
                }),
                true,
                new[] { MediaCommandNames.CompletedOutcome });
            mediaEpisode = new Episode(
                "media",
                "Media",
                "start",
                new[] { new EpisodeExit("done") },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("video"))),
                    new Step("video", StepKind.Command, new StepData(command: video, target: Target.Step("image"))),
                    new Step("image", StepKind.Command, new StepData(command: image, target: Target.Step("audio"))),
                    new Step("audio", StepKind.Command, new StepData(command: audio, target: Target.Step("unlock"))),
                    new Step("unlock", StepKind.Command, new StepData(command: unlock, target: Target.Step("end"))),
                    new Step("end", StepKind.End, new StepData(exitId: "done"))
                });
            var volume = new Volume(
                "volume",
                "Volume",
                new[] { currentEpisode, mediaEpisode },
                new Route(new[]
                {
                    RouteEdge.FromRoot("current", currentEpisode.EpisodeId),
                    RouteEdge.FromRoot("media", mediaEpisode.EpisodeId)
                }));
            return new Program(
                "story",
                "1",
                new[] { volume },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition(
                        MediaCommandNames.PlayVideo,
                        "Video",
                        false,
                        new[]
                        {
                            MediaCommandNames.ClipArgument,
                            MediaCommandNames.VideoFormatArgument,
                            MediaCommandNames.VideoRenditionsArgument
                        }),
                    new CommandDefinition(
                        MediaCommandNames.ShowImage,
                        "Image",
                        false,
                        new[] { MediaCommandNames.ImageArgument }),
                    new CommandDefinition(
                        MediaCommandNames.PlayAudio,
                        "Audio",
                        false,
                        new[]
                        {
                            MediaCommandNames.MediaSourceArgument,
                            MediaCommandNames.ClipArgument
                        }),
                    new CommandDefinition(
                        StoryCommandNames.Unlock,
                        "Unlock",
                        true,
                        new[] { StoryCommandNames.UnlockIdArgument },
                        new[] { MediaCommandNames.CompletedOutcome })
                }));
        }

        private static void AssertPosition(StoryPosition expected, StoryPosition actual)
        {
            Assert.AreEqual(expected.StoryId, actual.StoryId);
            Assert.AreEqual(expected.VolumeId, actual.VolumeId);
            Assert.AreEqual(expected.EpisodeId, actual.EpisodeId);
            Assert.AreEqual(expected.StepId, actual.StepId);
        }
    }
}

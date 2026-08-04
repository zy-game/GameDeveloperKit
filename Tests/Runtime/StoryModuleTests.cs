using System;
using System.Linq;
using GameDeveloperKit.Media;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Text;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryModuleTests : RuntimeTestBase
    {
        [Test]
        public void NodeSchemaRegistry_ExposesFiniteCurrentNodeSet()
        {
            CollectionAssert.DoesNotContain(Enum.GetNames(typeof(NodeKind)), "Command");
            CollectionAssert.DoesNotContain(Enum.GetNames(typeof(NodeKind)), "Logic");
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NodeKind.Start,
                    NodeKind.End,
                    NodeKind.Transition,
                    NodeKind.Parallel,
                    NodeKind.Wait,
                    NodeKind.Dialogue,
                    NodeKind.Narration,
                    NodeKind.PlayVideo,
                    NodeKind.ShowImage,
                    NodeKind.PlayAudio,
                    NodeKind.Unlock,
                    NodeKind.Choice
                },
                NodeSchemaRegistry.Schemas.Select(schema => schema.Kind));
            Assert.IsTrue(NodeSchemaRegistry.Get(NodeKind.Unlock).Parameters.Any(parameter =>
                parameter.Key == NodeSchemaRegistry.UnlockIdParameter && parameter.Required));
        }

        [Test]
        public void StoryProgram_WhenTypedInstructionsComplete_AdvancesInOrder()
        {
            var module = StartModuleWith(CreateMediaProgram());
            try
            {
                var frame = module.StartEpisode("story_media", "volume", "episode").CurrentFrame;
                AssertInstruction<StoryInstruction.PlayVideo>(frame, "video");

                frame = module.CompleteInstruction("video");
                AssertInstruction<StoryInstruction.ShowImage>(frame, "image");

                frame = module.CompleteInstruction("image");
                var audio = AssertInstruction<StoryInstruction.PlayAudio>(frame, "audio");
                Assert.AreEqual("audio/theme.ogg", audio.Reference.Path.Value);
                Assert.AreEqual(0.75f, audio.Volume);
                Assert.AreEqual(32, audio.Priority);

                frame = module.CompleteInstruction("audio");
                var unlock = AssertInstruction<StoryInstruction.Unlock>(frame, "unlock");
                Assert.AreEqual("chapter-2", unlock.UnlockId);

                frame = module.CompleteInstruction("unlock");
                Assert.IsTrue(frame.IsCompleted);
                Assert.AreEqual(4, module.CurrentRunner.History.Count);
                CollectionAssert.AreEqual(
                    new[] { "video", "image", "audio", "unlock" },
                    module.CurrentRunner.History.Select(item => item.ActionId));
            }
            finally
            {
                module.Shutdown();
            }
        }

        [Test]
        public void CompleteInstruction_WhenIdDoesNotMatch_ThrowsWithoutAdvancing()
        {
            var module = StartModuleWith(CreateMediaProgram());
            try
            {
                var before = module.StartEpisode("story_media", "volume", "episode").CurrentFrame;

                var exception = Assert.Throws<GameException>(() => module.CompleteInstruction("image"));

                StringAssert.Contains("image", exception.Message);
                Assert.AreSame(before, module.CurrentFrame);
                Assert.AreEqual(0, module.CurrentRunner.History.Count);
            }
            finally
            {
                module.Shutdown();
            }
        }

        [Test]
        public void Register_WhenInstructionPayloadIsMissing_RejectsProgram()
        {
            var episode = StoryProgramTestFactory.Episode(
                "episode",
                "Episode",
                "start",
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("video"))),
                    new Step("video", StepKind.PlayVideo)
                });
            var program = StoryProgramTestFactory.Program(
                "invalid_video", "1", episode.EpisodeId, new[] { episode });
            var module = new StoryModule();
            module.Startup();
            try
            {
                var exception = Assert.Throws<GameException>(() => module.Register(program));
                StringAssert.Contains("Story video reference cannot be null", exception.Message);
            }
            finally
            {
                module.Shutdown();
            }
        }

        [Test]
        public void StoryQueries_ReturnRegisteredDefinitionsPositionAndMediaCues()
        {
            var program = CreateMediaProgram();
            var module = StartModuleWith(program);
            try
            {
                Assert.AreEqual(1, module.GetVolumes(program.StoryId).Count);
                Assert.AreEqual(1, module.GetEpisodes(program.StoryId, "volume").Count);
                Assert.AreSame(program.Volumes[0], module.GetVolumes(program.StoryId)[0]);
                Assert.AreSame(program.Volumes[0].Episodes[0], module.GetEpisode(program.StoryId, "volume", "episode"));

                var cues = module.GetEpisodeMediaCues(program.StoryId, "volume", "episode");
                CollectionAssert.AreEqual(
                    new[] { "video", "image", "audio" },
                    cues.Select(cue => cue.StepId));
                Assert.IsInstanceOf<StoryInstruction.PlayVideo>(cues[0].Instruction);
                Assert.IsInstanceOf<StoryInstruction.ShowImage>(cues[1].Instruction);
                Assert.IsInstanceOf<StoryInstruction.PlayAudio>(cues[2].Instruction);

                Assert.IsFalse(module.TryGetCurrentPosition(out _));
                module.StartEpisode(program.StoryId, "volume", "episode");
                Assert.IsTrue(module.TryGetCurrentPosition(out var position));
                Assert.AreEqual("video", position.StepId);
                Assert.IsTrue(module.TryGetCurrentChapter(out var volume, out var episode, out var step));
                Assert.AreEqual("volume", volume.VolumeId);
                Assert.AreEqual("episode", episode.EpisodeId);
                Assert.AreEqual("video", step.StepId);
            }
            finally
            {
                module.Shutdown();
            }
        }

        [Test]
        public void StoryWait_WhenEvaluatedAndRestored_PreservesElapsedTime()
        {
            var episode = StoryProgramTestFactory.Episode(
                "episode",
                "Episode",
                "start",
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("wait"))),
                    new Step("wait", StepKind.Wait, new StepData(
                        waitSeconds: 1d,
                        target: Target.Step("end"))),
                    new Step("end", StepKind.End)
                });
            var program = StoryProgramTestFactory.Program(
                "story_wait", "1", episode.EpisodeId, new[] { episode });
            var module = StartModuleWith(program);
            try
            {
                var frame = module.StartEpisode(
                    program.StoryId, StoryProgramTestFactory.VolumeId, episode.EpisodeId).CurrentFrame;
                Assert.IsTrue(frame.WaitsForTime);

                frame = module.Evaluate(0.4d);
                Assert.IsTrue(frame.WaitsForTime);
                var snapshot = module.CreateSnapshot();
                Assert.AreEqual(0.4d, snapshot.WaitElapsed, 0.0001d);

                module.Restore(snapshot);
                frame = module.Evaluate(0.6d);
                Assert.IsTrue(frame.IsCompleted);
            }
            finally
            {
                module.Shutdown();
            }
        }

        [Test]
        public void StoryChoice_WhenSelected_CompletesWithDeclaredExit()
        {
            var choice = new Choice("take", "take_exit", Literal("Take"));
            var episode = new Episode(
                "episode",
                "Episode",
                "start",
                new[] { new EpisodeExit(choice.ExitId) },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("choice"))),
                    new Step("choice", StepKind.Choice, new StepData(choices: new[] { choice }))
                });
            var volume = new Volume(
                "volume",
                "Volume",
                new[] { episode },
                new Route(new[] { RouteEdge.FromRoot("root", episode.EpisodeId) }));
            var module = StartModuleWith(new Program("story_choice", "1", new[] { volume }));
            try
            {
                var frame = module.StartEpisode("story_choice", "volume", "episode").CurrentFrame;
                Assert.IsTrue(frame.WaitsForChoice);

                frame = module.Select(choice.ChoiceId);

                Assert.IsTrue(frame.IsCompleted);
                Assert.AreEqual(choice.ExitId, frame.CompletedExitId);
                Assert.AreEqual(EpisodeCompletionKind.Choice, frame.CompletedKind);
            }
            finally
            {
                module.Shutdown();
            }
        }

        [Test]
        public void StoryTransition_WhenRouteExists_AutomaticallyStartsTargetEpisode()
        {
            var source = new Episode(
                "source",
                "Source",
                "start",
                new[] { new EpisodeExit("next") },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("transition"))),
                    new Step("transition", StepKind.Transition, new StepData(exitId: "next"))
                });
            var target = StoryProgramTestFactory.Episode(
                "target",
                "Target",
                "start",
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("line"))),
                    new Step("line", StepKind.Line, new StepData(textKey: Literal("arrived")))
                });
            var volume = new Volume(
                "volume",
                "Volume",
                new[] { source, target },
                new Route(new[]
                {
                    RouteEdge.FromRoot("root", source.EpisodeId),
                    RouteEdge.FromExit("next_route", source.EpisodeId, "next", target.EpisodeId)
                }));
            var module = StartModuleWith(new Program("story_transition", "1", new[] { volume }));
            try
            {
                var frame = module.StartEpisode("story_transition", "volume", source.EpisodeId).CurrentFrame;

                Assert.AreEqual(target.EpisodeId, frame.Episode.EpisodeId);
                Assert.AreEqual("line", frame.AnchorStep.StepId);
                Assert.IsFalse(frame.IsCompleted);
            }
            finally
            {
                module.Shutdown();
            }
        }

        [Test]
        public void ParallelFrame_WhenInstructionCompletes_KeepsOtherBranchUntilContinue()
        {
            var parallel = new Step(
                "parallel",
                StepKind.Parallel,
                new StepData(branches: new[]
                {
                    new ParallelBranch("video_branch", "Video", Target.Step("video")),
                    new ParallelBranch("text_branch", "Text", Target.Step("line"))
                }));
            var episode = new Episode(
                "episode",
                "Episode",
                "start",
                Array.Empty<EpisodeExit>(),
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step(parallel.StepId))),
                    parallel,
                    VideoStep("video", "videos/story/parallel/master.m3u8", Target.Step("video_end")),
                    new Step("video_end", StepKind.End),
                    new Step("line", StepKind.Line, new StepData(
                        textKey: Literal("line"),
                        target: Target.Step("text_end"))),
                    new Step("text_end", StepKind.End)
                });
            var volume = new Volume(
                "volume",
                "Volume",
                new[] { episode },
                new Route(new[] { RouteEdge.FromRoot("root", episode.EpisodeId) }));
            var runner = new Runner(new Program("story_parallel", "1", new[] { volume }));

            var frame = runner.Start(volume.VolumeId, episode.EpisodeId);
            Assert.IsTrue(frame.WaitsForInstruction);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.IsTrue(frame.Tracks.Any(track => track.Kind == FrameTrackKind.Instruction));
            Assert.IsTrue(frame.Tracks.Any(track => track.Kind == FrameTrackKind.Text));

            frame = runner.CompleteInstruction("video");
            Assert.IsFalse(frame.WaitsForInstruction);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.AreEqual(FrameTrackKind.Text, frame.Tracks[0].Kind);

            frame = runner.Continue();
            Assert.IsTrue(frame.IsCompleted);
        }

        [Test]
        public void ProgramAsset_WhenRoundTripped_PreservesTypedInstructionPayloads()
        {
            var asset = ScriptableObject.CreateInstance<ProgramAsset>();
            try
            {
                asset.SetProgram(CreateMediaProgram());

                var restored = asset.ToProgram();
                var steps = restored.Volumes[0].Episodes[0].Steps;

                Assert.AreEqual(TargetKind.Step, FindStep(steps, "start").Data.Target.TargetKind);
                Assert.AreEqual("video", FindStep(steps, "start").Data.Target.StepId);
                Assert.AreEqual(StepKind.PlayVideo, FindStep(steps, "video").Kind);
                Assert.AreEqual("videos/story/master.m3u8", FindStep(steps, "video").Data.VideoReference.Primary.Value);
                Assert.AreEqual("Images/cover", FindStep(steps, "image").Data.ImageLocation);
                Assert.AreEqual("audio/theme.ogg", FindStep(steps, "audio").Data.AudioReference.Path.Value);
                Assert.AreEqual("chapter-2", FindStep(steps, "unlock").Data.UnlockId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static StoryModule StartModuleWith(Program program)
        {
            var module = new StoryModule();
            module.Startup();
            module.Register(program);
            return module;
        }

        private static Program CreateMediaProgram()
        {
            var episode = new Episode(
                "episode",
                "Episode",
                "start",
                new[] { new EpisodeExit("done") },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("video"))),
                    VideoStep("video", "videos/story/master.m3u8", Target.Step("image")),
                    new Step("image", StepKind.ShowImage, new StepData(
                        imageLocation: "Images/cover",
                        target: Target.Step("audio"))),
                    new Step("audio", StepKind.PlayAudio, new StepData(
                        audioReference: new AudioReference(new MediaPath("audio/theme.ogg")),
                        volume: 0.75f,
                        priority: 32,
                        target: Target.Step("unlock"))),
                    new Step("unlock", StepKind.Unlock, new StepData(
                        unlockId: "chapter-2",
                        target: Target.Step("end"))),
                    new Step("end", StepKind.End, new StepData(exitId: "done"))
                });
            var volume = new Volume(
                "volume",
                "Volume",
                new[] { episode },
                new Route(new[] { RouteEdge.FromRoot("root", episode.EpisodeId) }));
            return new Program("story_media", "1", new[] { volume });
        }

        private static Step VideoStep(string stepId, string path, Target target = null)
        {
            return new Step(
                stepId,
                StepKind.PlayVideo,
                new StepData(
                    videoReference: new VideoReference(new MediaPath(path), VideoFormat.Hls),
                    loop: false,
                    seekable: true,
                    target: target));
        }

        private static TInstruction AssertInstruction<TInstruction>(Frame frame, string instructionId)
            where TInstruction : StoryInstruction
        {
            Assert.IsTrue(frame.WaitsForInstruction);
            Assert.AreEqual(1, frame.Instructions.Count);
            Assert.AreEqual(instructionId, frame.Instructions[0].InstructionId);
            return (TInstruction)frame.Instructions[0];
        }

        private static Step FindStep(System.Collections.Generic.IReadOnlyList<Step> steps, string stepId)
        {
            return steps.Single(step => step.StepId == stepId);
        }

        private static string Literal(string value)
        {
            return TextReferenceCodec.Serialize(new TextReference(TextMode.Literal, value));
        }
    }
}

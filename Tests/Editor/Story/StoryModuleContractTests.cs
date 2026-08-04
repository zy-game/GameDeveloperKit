using System;
using System.Collections.Generic;
using GameDeveloperKit.Media;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;
using GameDeveloperKit.Story.Text;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryModuleContractTests
    {
        [TestCase(StepKind.PlayVideo, typeof(StoryInstruction.PlayVideo))]
        [TestCase(StepKind.ShowImage, typeof(StoryInstruction.ShowImage))]
        [TestCase(StepKind.PlayAudio, typeof(StoryInstruction.PlayAudio))]
        [TestCase(StepKind.Unlock, typeof(StoryInstruction.Unlock))]
        public void FrameInstructions_WhenFiniteKindIsUsed_ExposeTypedInstruction(
            StepKind kind,
            Type instructionType)
        {
            var step = CreateInstructionStep(kind);
            var episode = new Episode(
                "episode",
                "Episode",
                step.StepId,
                Array.Empty<EpisodeExit>(),
                new[] { step });
            var volume = new Volume("volume", "Volume", new[] { episode }, new Route());
            var program = new Program("story", "1", new[] { volume });

            var frame = Frame.CreateInstruction(program, volume, episode, step);

            Assert.AreEqual(FrameTrackKind.Instruction, frame.Tracks[0].Kind);
            Assert.AreEqual(instructionType, frame.Instructions[0].GetType());
            Assert.AreEqual(step.StepId, frame.Instructions[0].InstructionId);
            Assert.IsTrue(frame.WaitsForInstruction);
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
                Assert.Throws<NotSupportedException>(() =>
                    ((IList<Episode>)volumes[0].Episodes)[0] = currentEpisode);
                Assert.Throws<NotSupportedException>(() =>
                    ((IList<Step>)episodes[0].Steps)[0] = episodes[0].Steps[0]);
                Assert.Throws<NotSupportedException>(() =>
                    ((IList<EpisodeExit>)mediaEpisode.Exits)[0] = mediaEpisode.Exits[0]);
                Assert.Throws<NotSupportedException>(() =>
                    ((IList<RouteEdge>)volumes[0].Route.Edges)[0] = volumes[0].Route.Edges[0]);
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

        [Test]
        public void RuntimeModelCollections_WhenConstructed_AreReadOnly()
        {
            var point = new Placement(0.25f, 0.5f);
            var edgeLayout = new RouteEdgePlacement("edge", new[] { point });
            var episodeLayout = new EpisodePlacement("episode", point);
            var layout = new RouteLayout(
                "layout",
                LayoutOrientation.Landscape,
                null,
                point,
                new[] { episodeLayout },
                new[] { edgeLayout });
            var expression = Expression.CreateAnd(
                Expression.FromLiteral(Value.FromBoolean(true)),
                Expression.FromLiteral(Value.FromBoolean(false)));
            var parameter = new NodeParameterDefinition(
                "quality",
                "Quality",
                ParameterValueType.Option,
                options: new[] { "auto" });
            var schema = new NodeSchema(
                NodeKind.PlayVideo,
                NodeCategory.Action,
                "Video",
                true,
                new[] { new PortDefinition("in", "In", PortDirection.Input) },
                new[] { parameter });

            AssertReadOnly(layout.Episodes, episodeLayout);
            AssertReadOnly(layout.Edges, edgeLayout);
            AssertReadOnly(edgeLayout.ControlPoints, point);
            AssertReadOnly(expression.Inputs, expression.Inputs[0]);
            AssertReadOnly(parameter.Options, "auto");
            AssertReadOnly(schema.Ports, schema.Ports[0]);
            AssertReadOnly(schema.Parameters, parameter);
        }

        [Test]
        public void RuntimeExecutionCollections_WhenExposed_AreReadOnlySnapshots()
        {
            var historyEntry = new HistoryEntry("episode", "step", "completed", null, "action", null, 1f);
            var branch = new ParallelBranchSnapshot("branch", "episode", "step", false);
            var variableSource = new Dictionary<string, Value>
            {
                ["flag"] = Value.FromBoolean(true)
            };
            var historySource = new List<HistoryEntry> { historyEntry };
            var branchSource = new List<ParallelBranchSnapshot> { branch };
            var snapshot = new Snapshot(
                "story",
                "1",
                "volume",
                "episode",
                "step",
                1d,
                variableSource,
                historySource,
                false,
                parallelBranches: branchSource);

            var store = new VariableStore();
            store.Set("flag", Value.FromBoolean(true));
            var variableSnapshot = store.Snapshot();
            var context = new RuntimeContext(null, null, null, null, 1d, store, historySource);
            var choice = new Choice("choice", "exit", Literal("Choice"));
            var choiceSource = new List<Choice> { choice };
            var request = new InteractionRequest(InteractionRequestKind.Choice, null, choices: choiceSource);

            variableSource.Clear();
            historySource.Clear();
            branchSource.Clear();
            choiceSource.Clear();

            Assert.AreEqual(1, snapshot.Variables.Count);
            Assert.AreEqual(1, snapshot.History.Count);
            Assert.AreEqual(1, snapshot.ParallelBranches.Count);
            Assert.AreEqual(1, variableSnapshot.Count);
            Assert.AreEqual(1, context.History.Count);
            Assert.AreEqual(1, request.Choices.Count);

            AssertReadOnly(snapshot.History, historyEntry);
            AssertReadOnly(snapshot.ParallelBranches, branch);
            AssertReadOnly(context.History, historyEntry);
            AssertReadOnly(request.Choices, choice);
            AssertReadOnlyDictionary(snapshot.Variables, "flag", Value.FromBoolean(false));
            AssertReadOnlyDictionary(variableSnapshot, "flag", Value.FromBoolean(false));
            Assert.Throws<NotSupportedException>(() =>
                ((ICollection<NodeSchema>)NodeSchemaRegistry.Schemas).Add(NodeSchemaRegistry.Get(NodeKind.Start)));
        }

        [Test]
        public void RunnerHistory_WhenInstructionCompletes_CannotBeMutatedByCallers()
        {
            var program = CreateProgram(out _, out var mediaEpisode);
            var runner = new Runner(program);
            runner.Start(program.Volumes[0].VolumeId, mediaEpisode.EpisodeId);
            runner.CompleteInstruction("video");

            Assert.AreEqual(1, runner.History.Count);
            AssertReadOnly(runner.History, runner.History[0]);
        }

        private static Step CreateInstructionStep(StepKind kind)
        {
            switch (kind)
            {
                case StepKind.PlayVideo:
                    return new Step("video", kind, new StepData(
                        videoReference: new VideoReference(
                            new MediaPath("videos/story/master.m3u8"),
                            VideoFormat.Hls)));
                case StepKind.ShowImage:
                    return new Step("image", kind, new StepData(imageLocation: "Images/Cover"));
                case StepKind.PlayAudio:
                    return new Step("audio", kind, new StepData(
                        audioReference: new AudioReference(new MediaPath("audio/theme.ogg"))));
                case StepKind.Unlock:
                    return new Step("unlock", kind, new StepData(unlockId: "chapter-2"));
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
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
                    new Step("line", StepKind.Line, new StepData(textKey: Literal("current")))
                });
            mediaEpisode = new Episode(
                "media",
                "Media",
                "start",
                new[] { new EpisodeExit("done") },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("video"))),
                    new Step("video", StepKind.PlayVideo, new StepData(
                        videoReference: new VideoReference(
                            new MediaPath("videos/story/chapter/master.m3u8"),
                            VideoFormat.Hls),
                        target: Target.Step("image"))),
                    new Step("image", StepKind.ShowImage, new StepData(
                        imageLocation: "Images/ChapterCover",
                        target: Target.Step("audio"))),
                    new Step("audio", StepKind.PlayAudio, new StepData(
                        audioReference: new AudioReference(new MediaPath("audio/chapter-bgm.ogg")),
                        target: Target.Step("unlock"))),
                    new Step("unlock", StepKind.Unlock, new StepData(
                        unlockId: "chapter-2",
                        target: Target.Step("end"))),
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
            return new Program("story", "1", new[] { volume });
        }

        private static string Literal(string value)
        {
            return TextReferenceCodec.Serialize(new TextReference(TextMode.Literal, value));
        }

        private static void AssertReadOnly<T>(IReadOnlyList<T> values, T replacement)
        {
            Assert.Throws<NotSupportedException>(() => ((IList<T>)values)[0] = replacement);
        }

        private static void AssertReadOnlyDictionary<TKey, TValue>(
            IReadOnlyDictionary<TKey, TValue> values,
            TKey key,
            TValue replacement)
        {
            Assert.Throws<NotSupportedException>(() => ((IDictionary<TKey, TValue>)values)[key] = replacement);
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

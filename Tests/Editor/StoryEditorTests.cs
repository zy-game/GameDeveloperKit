using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story;
using GameDeveloperKit.StoryEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Playback;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Excel;
using GameDeveloperKit.StoryEditor.Playback;
using GameDeveloperKit.StoryEditor.Validation;
using GameDeveloperKit.StoryEditor.UI;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryEditorTests
    {
        private const string InvalidStreamingAssetsVideoPath = "Assets/Bundles/Story/videos/0.mp4";

        private readonly List<UnityEngine.Object> m_CreatedObjects = new List<UnityEngine.Object>();
        private readonly List<string> m_CreatedAssetPaths = new List<string>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < m_CreatedObjects.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(m_CreatedObjects[i]);
            }

            m_CreatedObjects.Clear();
            for (var i = 0; i < m_CreatedAssetPaths.Count; i++)
            {
                AssetDatabase.DeleteAsset(m_CreatedAssetPaths[i]);
            }

            m_CreatedAssetPaths.Clear();
        }

        [Test]
        public void ProgramCompiler_WhenAuthoringIsValid_BuildsStoryProgram()
        {
            var asset = CreateCompilerAsset();

            var program = ProgramCompiler.Compile(asset, out var report);
            var schema = program.CommandSchema.Definitions.First(x => x.Name == "play_video");
            var sourceArgument = schema.ArgumentDefinitions.First(x => x.Key == MediaCommandNames.MediaSourceArgument);
            var clipArgument = schema.ArgumentDefinitions.First(x => x.Key == "clip");
            var loopArgument = schema.ArgumentDefinitions.First(x => x.Key == "loop");

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);
            Assert.AreEqual("compiler_story", program.StoryId);
            Assert.AreEqual("chapter_01", program.EntryChapterId);
            Assert.AreEqual(1, program.Chapters.Count);
            Assert.AreEqual("start", program.Chapters[0].EntryStepId);
            Assert.AreEqual(ParameterValueType.Option, sourceArgument.ValueType);
            Assert.IsTrue(sourceArgument.Required);
            CollectionAssert.AreEqual(
                new[] { MediaCommandNames.VideoSourceCdn, MediaCommandNames.VideoSourceStreamingAssets },
                sourceArgument.Options.ToArray());
            Assert.AreEqual(ParameterValueType.String, clipArgument.ValueType);
            Assert.IsTrue(clipArgument.Required);
            Assert.AreEqual(ParameterValueType.Boolean, loopArgument.ValueType);
            Assert.IsFalse(loopArgument.Required);
            CollectionAssert.Contains(schema.ArgumentNames.ToList(), MediaCommandNames.MediaSourceArgument);
            CollectionAssert.Contains(schema.ArgumentNames.ToList(), MediaCommandNames.MediaIdArgument);
            CollectionAssert.Contains(schema.ArgumentNames.ToList(), MediaCommandNames.VideoFormatArgument);
            CollectionAssert.Contains(schema.ArgumentNames.ToList(), MediaCommandNames.VideoRenditionsArgument);
            CollectionAssert.Contains(schema.ArgumentNames.ToList(), "clip");
            CollectionAssert.Contains(schema.ArgumentNames.ToList(), "loop");
            CollectionAssert.DoesNotContain(schema.ArgumentNames.ToList(), "wait");

            var line = FindStep(program, "chapter_01", "line_intro");
            Assert.AreEqual(StepKind.Line, line.Kind);
            Assert.AreEqual("story.intro.line", line.Data.TextKey);
            Assert.AreEqual("npc", line.Data.Speaker);
            CollectionAssert.Contains(line.Tags, "intro");

            var choice = FindStep(program, "chapter_01", "line_intro_choices");
            Assert.AreEqual(StepKind.Choice, choice.Kind);
            Assert.AreEqual(2, choice.Choices.Count);
            Assert.AreEqual("choice_help", choice.Choices[0].ChoiceId);
            Assert.AreEqual("choice.help", choice.Choices[0].TextKey);
            Assert.AreEqual(ExpressionKind.Function, choice.Choices[0].Condition.Kind);
            Assert.AreEqual("can_help", choice.Choices[0].Condition.FunctionName);
            Assert.AreEqual("video", choice.Choices[0].Target.StepId);

            var command = FindStep(program, "chapter_01", "video");
            Assert.AreEqual(StepKind.Command, command.Kind);
            Assert.AreEqual("play_video", command.Data.Command.Name);
            Assert.AreEqual(MediaCommandNames.VideoSourceStreamingAssets, command.Data.Command.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
            Assert.AreEqual("videos/0.mp4", command.Data.Command.Arguments.GetString("clip"));
            Assert.AreEqual("mp4", command.Data.Command.Arguments.GetString(MediaCommandNames.VideoFormatArgument));
            Assert.IsTrue(VideoReferenceCodec.TryDeserializeRenditions(
                command.Data.Command.Arguments.GetString(MediaCommandNames.VideoRenditionsArgument),
                out var renditions,
                out var renditionError), renditionError);
            Assert.AreEqual(0, renditions.Count);
            Assert.IsTrue(command.Data.Command.Arguments.GetBoolean("loop"));
            Assert.IsTrue(command.Data.Command.WaitForCompletion);
            Assert.AreEqual("end", command.Data.Command.GetOutcomeTarget("completed").StepId);
        }

        [Test]
        public void Excel_WhenExportedAndImported_RoundTripsChapterNodesParametersAndEdges()
        {
            var source = CreateAsset();
            source.StoryId = "excel_round_trip";
            source.Version = "1";
            source.EntryChapterId = "chapter_01";
            source.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "start",
                new[]
                {
                    CreateNode("start", "开始", NodeKind.Start),
                    CreateNode("line", "对白", NodeKind.Dialogue, ("textKey", "story.excel.line"), ("speaker", "npc")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("start", "completed", "完成", "line"),
                    CreateEdge("line", "completed", "完成", "end"),
                }));

            var target = CreateAsset();
            target.StoryId = source.StoryId;
            target.Version = source.Version;
            target.EntryChapterId = source.EntryChapterId;
            var path = Path.Combine(Path.GetTempPath(), $"story-excel-{Guid.NewGuid():N}.xlsx");

            try
            {
                Exporter.Export(source, path);
                var report = Importer.Import(path, target);

                AssertNoErrors(report.Issues);
                Assert.AreEqual(1, target.Chapters.Count);
                var chapter = target.Chapters[0];
                Assert.AreEqual("chapter_01", chapter.ChapterId);
                Assert.AreEqual("第一章", chapter.Title);
                Assert.AreEqual("start", chapter.EntryNodeId);
                Assert.AreEqual(3, chapter.Nodes.Count);
                Assert.AreEqual(NodeKind.Start, chapter.Nodes.Single(x => x.NodeId == "start").NodeKind);
                var line = chapter.Nodes.Single(x => x.NodeId == "line");
                Assert.AreEqual(NodeKind.Dialogue, line.NodeKind);
                Assert.AreEqual("story.excel.line", line.Parameters.Single(x => x.Key == "textKey").Value);
                Assert.AreEqual("npc", line.Parameters.Single(x => x.Key == "speaker").Value);
                Assert.AreEqual(NodeKind.End, chapter.Nodes.Single(x => x.NodeId == "end").NodeKind);
                Assert.AreEqual(2, chapter.Edges.Count);
                Assert.IsTrue(chapter.Edges.Any(x => x.FromNodeId == "start" && x.TargetNodeId == "line"));
                Assert.IsTrue(chapter.Edges.Any(x => x.FromNodeId == "line" && x.TargetNodeId == "end"));
                AssertNoErrors(AuthoringValidator.Validate(target).Issues);
            }
            finally
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
        }

        [Test]
        public void NodeSchemaRegistry_WhenMediaNodesQueried_ExposeLoopParameter()
        {
            var video = NodeSchemaRegistry.Get(NodeKind.PlayVideo);
            var audio = NodeSchemaRegistry.Get(NodeKind.PlayAudio);
            CollectionAssert.DoesNotContain(video.Parameters.Select(x => x.Key).ToList(), MediaCommandNames.VideoSourceArgument);
            Assert.AreEqual(ParameterValueType.AssetReference, video.Parameters.First(x => x.Key == MediaCommandNames.ClipArgument).ValueType);
            Assert.AreEqual(ParameterValueType.Boolean, video.Parameters.First(x => x.Key == "loop").ValueType);
            Assert.AreEqual(ParameterValueType.Boolean, audio.Parameters.First(x => x.Key == "loop").ValueType);
            CollectionAssert.DoesNotContain(video.Parameters.Select(x => x.Key).ToList(), "playbackRole");
            CollectionAssert.DoesNotContain(video.Parameters.Select(x => x.Key).ToList(), "seekable");
            CollectionAssert.DoesNotContain(video.Parameters.Select(x => x.Key).ToList(), MediaCommandNames.VideoSeekPolicyArgument);
        }

        [Test]
        public void NodeSchemaRegistry_WhenQteNodeQueried_ExposesQteSchema()
        {
            var schema = NodeSchemaRegistry.Get(NodeKind.Qte);
            var parameters = schema.Parameters.ToDictionary(x => x.Key, x => x);

            Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.Qte));
            Assert.AreEqual(NodeCategory.Interaction, schema.Category);
            CollectionAssert.AreEqual(
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome
                },
                schema.Ports.Select(x => x.PortId).ToArray());
            Assert.AreEqual(ParameterValueType.String, parameters[InteractionCommandNames.InputActionIdArgument].ValueType);
            Assert.IsTrue(parameters[InteractionCommandNames.InputActionIdArgument].Required);
            Assert.AreEqual(ParameterValueType.Number, parameters[InteractionCommandNames.DurationSecondsArgument].ValueType);
            Assert.IsTrue(parameters[InteractionCommandNames.DurationSecondsArgument].Required);
            Assert.AreEqual(ParameterValueType.Number, parameters[InteractionCommandNames.RequiredCountArgument].ValueType);
            Assert.IsFalse(parameters[InteractionCommandNames.RequiredCountArgument].Required);
            Assert.AreEqual(ParameterValueType.String, parameters[InteractionCommandNames.PromptTextKeyArgument].ValueType);
            Assert.IsTrue(parameters[InteractionCommandNames.PromptTextKeyArgument].Required);
        }

        [Test]
        public void NodeSchemaRegistry_WhenUnlockNodeQueried_ExposesUnlockSchema()
        {
            var schema = NodeSchemaRegistry.Get(NodeKind.Unlock);
            var parameters = schema.Parameters.ToDictionary(x => x.Key, x => x);
            var puzzleType = parameters[InteractionCommandNames.PuzzleTypeArgument];

            Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.Unlock));
            Assert.AreEqual(NodeCategory.Interaction, schema.Category);
            CollectionAssert.AreEqual(
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome
                },
                schema.Ports.Select(x => x.PortId).ToArray());
            Assert.AreEqual(ParameterValueType.String, parameters[InteractionCommandNames.UnlockIdArgument].ValueType);
            Assert.IsTrue(parameters[InteractionCommandNames.UnlockIdArgument].Required);
            Assert.AreEqual(ParameterValueType.Option, puzzleType.ValueType);
            Assert.IsTrue(puzzleType.Required);
            CollectionAssert.AreEqual(
                new[]
                {
                    InteractionCommandNames.PuzzleTypeLineConnect,
                    InteractionCommandNames.PuzzleTypeNodeUnlock,
                    InteractionCommandNames.PuzzleTypeCustom
                },
                puzzleType.Options.ToArray());
            Assert.AreEqual(ParameterValueType.String, parameters[InteractionCommandNames.PromptTextKeyArgument].ValueType);
            Assert.IsTrue(parameters[InteractionCommandNames.PromptTextKeyArgument].Required);
        }

        [Test]
        public void ProgramCompiler_WhenQteNodeIsValid_BuildsQteCommand()
        {
            var asset = CreateQteCompilerAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var step = FindStep(program, "chapter_01", "qte");
            var command = step.Data.Command;
            var definition = program.CommandSchema.Definitions.First(x => x.Name == InteractionCommandNames.Qte);
            var argumentDefinitions = definition.ArgumentDefinitions.ToDictionary(x => x.Key, x => x);

            Assert.AreEqual(StepKind.Command, step.Kind);
            Assert.AreEqual(InteractionCommandNames.Qte, command.Name);
            Assert.IsTrue(command.WaitForCompletion);
            Assert.AreEqual("space", command.Arguments.GetString(InteractionCommandNames.InputActionIdArgument));
            Assert.AreEqual(3d, command.Arguments.GetNumber(InteractionCommandNames.DurationSecondsArgument));
            Assert.AreEqual(5d, command.Arguments.GetNumber(InteractionCommandNames.RequiredCountArgument));
            Assert.AreEqual("qte.break_free", command.Arguments.GetString(InteractionCommandNames.PromptTextKeyArgument));
            CollectionAssert.AreEqual(
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome
                },
                command.OutcomePorts.ToArray());
            Assert.AreEqual("qte_success", command.GetOutcomeTarget(InteractionCommandNames.SuccessOutcome).StepId);
            Assert.AreEqual("qte_fail", command.GetOutcomeTarget(InteractionCommandNames.FailOutcome).StepId);
            Assert.IsTrue(definition.WaitForCompletion);
            Assert.AreEqual(ParameterValueType.String, argumentDefinitions[InteractionCommandNames.InputActionIdArgument].ValueType);
            Assert.IsTrue(argumentDefinitions[InteractionCommandNames.InputActionIdArgument].Required);
            Assert.AreEqual(ParameterValueType.Number, argumentDefinitions[InteractionCommandNames.DurationSecondsArgument].ValueType);
            Assert.IsTrue(argumentDefinitions[InteractionCommandNames.DurationSecondsArgument].Required);
            Assert.AreEqual(ParameterValueType.Number, argumentDefinitions[InteractionCommandNames.RequiredCountArgument].ValueType);
            Assert.IsFalse(argumentDefinitions[InteractionCommandNames.RequiredCountArgument].Required);
            Assert.AreEqual(ParameterValueType.String, argumentDefinitions[InteractionCommandNames.PromptTextKeyArgument].ValueType);
            Assert.IsTrue(argumentDefinitions[InteractionCommandNames.PromptTextKeyArgument].Required);
            CollectionAssert.AreEqual(
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome
                },
                definition.OutcomePorts.ToArray());
        }

        [TestCase("0")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        public void ProgramCompiler_WhenQteDurationIsInvalid_ReturnsLocatedError(string durationSeconds)
        {
            var asset = CreateQteCompilerAsset(durationSeconds: durationSeconds);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains($"story:compiler_story/chapter:chapter_01/node:qte/field:{InteractionCommandNames.DurationSecondsArgument}", issues);
            StringAssert.Contains("QTE durationSeconds must be finite and greater than zero.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenQteRequiredCountIsInvalid_ReturnsLocatedError()
        {
            var asset = CreateQteCompilerAsset(requiredCount: "0");

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains($"story:compiler_story/chapter:chapter_01/node:qte/field:{InteractionCommandNames.RequiredCountArgument}", issues);
            StringAssert.Contains("QTE requiredCount must be finite and greater than zero.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenQteOutcomeTargetMissing_ReturnsLocatedError()
        {
            var asset = CreateQteCompilerAsset(includeFailOutcome: false);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains($"story:compiler_story/chapter:chapter_01/node:qte/outcome:{InteractionCommandNames.FailOutcome}", issues);
            StringAssert.Contains("QTE command must target both success and fail outcomes.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenQteHasUnsupportedOutcome_ReturnsLocatedError()
        {
            var asset = CreateQteCompilerAsset(includeTimeoutOutcome: true);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:qte/outcome:timeout", issues);
            StringAssert.Contains("QTE command only supports success and fail outcomes.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenUnlockNodeIsValid_BuildsUnlockCommand()
        {
            var asset = CreateUnlockCompilerAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var step = FindStep(program, "chapter_01", "unlock");
            var command = step.Data.Command;
            var definition = program.CommandSchema.Definitions.First(x => x.Name == InteractionCommandNames.Unlock);
            var argumentDefinitions = definition.ArgumentDefinitions.ToDictionary(x => x.Key, x => x);
            var puzzleType = argumentDefinitions[InteractionCommandNames.PuzzleTypeArgument];

            Assert.AreEqual(StepKind.Command, step.Kind);
            Assert.AreEqual(InteractionCommandNames.Unlock, command.Name);
            Assert.IsTrue(command.WaitForCompletion);
            Assert.AreEqual("chapter_01.door", command.Arguments.GetString(InteractionCommandNames.UnlockIdArgument));
            Assert.AreEqual(
                InteractionCommandNames.PuzzleTypeNodeUnlock,
                command.Arguments.GetString(InteractionCommandNames.PuzzleTypeArgument));
            Assert.AreEqual("unlock.door", command.Arguments.GetString(InteractionCommandNames.PromptTextKeyArgument));
            CollectionAssert.AreEqual(
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome
                },
                command.OutcomePorts.ToArray());
            Assert.AreEqual("unlock_success", command.GetOutcomeTarget(InteractionCommandNames.SuccessOutcome).StepId);
            Assert.AreEqual("unlock_fail", command.GetOutcomeTarget(InteractionCommandNames.FailOutcome).StepId);
            Assert.IsTrue(definition.WaitForCompletion);
            Assert.AreEqual(ParameterValueType.String, argumentDefinitions[InteractionCommandNames.UnlockIdArgument].ValueType);
            Assert.IsTrue(argumentDefinitions[InteractionCommandNames.UnlockIdArgument].Required);
            Assert.AreEqual(ParameterValueType.Option, puzzleType.ValueType);
            Assert.IsTrue(puzzleType.Required);
            CollectionAssert.AreEqual(
                new[]
                {
                    InteractionCommandNames.PuzzleTypeLineConnect,
                    InteractionCommandNames.PuzzleTypeNodeUnlock,
                    InteractionCommandNames.PuzzleTypeCustom
                },
                puzzleType.Options.ToArray());
            Assert.AreEqual(ParameterValueType.String, argumentDefinitions[InteractionCommandNames.PromptTextKeyArgument].ValueType);
            Assert.IsTrue(argumentDefinitions[InteractionCommandNames.PromptTextKeyArgument].Required);
            CollectionAssert.AreEqual(
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome
                },
                definition.OutcomePorts.ToArray());
        }

        [Test]
        public void ProgramCompiler_WhenUnlockPuzzleTypeIsInvalid_ReturnsLocatedError()
        {
            var asset = CreateUnlockCompilerAsset(puzzleType: "slide_lock");

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains($"story:compiler_story/chapter:chapter_01/node:unlock/field:{InteractionCommandNames.PuzzleTypeArgument}", issues);
            StringAssert.Contains("Command field must use a valid option.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenUnlockIdMissing_ReturnsLocatedError()
        {
            var asset = CreateUnlockCompilerAsset(unlockId: null);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains($"story:compiler_story/chapter:chapter_01/node:unlock/field:{InteractionCommandNames.UnlockIdArgument}", issues);
            StringAssert.Contains("Required command field is missing.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenUnlockPromptTextKeyMissing_ReturnsLocatedError()
        {
            var asset = CreateUnlockCompilerAsset(promptTextKey: null);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains($"story:compiler_story/chapter:chapter_01/node:unlock/field:{InteractionCommandNames.PromptTextKeyArgument}", issues);
            StringAssert.Contains("Required command field is missing.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenUnlockOutcomeTargetMissing_ReturnsLocatedError()
        {
            var asset = CreateUnlockCompilerAsset(includeFailOutcome: false);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains($"story:compiler_story/chapter:chapter_01/node:unlock/outcome:{InteractionCommandNames.FailOutcome}", issues);
            StringAssert.Contains("Unlock command must target both success and fail outcomes.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenUnlockHasUnsupportedOutcome_ReturnsLocatedError()
        {
            var asset = CreateUnlockCompilerAsset(includeUnsupportedOutcome: true);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:unlock/outcome:timeout", issues);
            StringAssert.Contains("Unlock command only supports success and fail outcomes.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenVideoTargetsQte_DoesNotWriteHiddenSeekPolicy()
        {
            var asset = CreateVideoQteTransitionAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "chapter_01", "video").Data.Command;
            Assert.IsFalse(command.Arguments.TryGetValue(MediaCommandNames.VideoSeekPolicyArgument, out _));
        }

        [Test]
        public void ProgramCompiler_WhenVideoTargetsUnlock_DoesNotWriteHiddenSeekPolicy()
        {
            var asset = CreateVideoUnlockTransitionAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "chapter_01", "video").Data.Command;
            Assert.IsFalse(command.Arguments.TryGetValue(MediaCommandNames.VideoSeekPolicyArgument, out _));
        }

        [Test]
        public void ProgramCompiler_WhenVideoIsLinearTransition_WritesHiddenSeekPolicy()
        {
            var asset = CreateTransitionVideoAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "chapter_01", "video").Data.Command;
            Assert.AreEqual(
                MediaCommandNames.VideoSeekPolicyTransition,
                command.Arguments.GetString(MediaCommandNames.VideoSeekPolicyArgument));
            var schema = program.CommandSchema.Definitions.First(x => x.Name == MediaCommandNames.PlayVideo);
            CollectionAssert.DoesNotContain(schema.ArgumentNames.ToList(), MediaCommandNames.VideoSeekPolicyArgument);
        }

        [Test]
        public void ProgramCompiler_WhenVideoTargetsParallel_WritesHiddenSeekPolicyOnlyForPreParallelVideo()
        {
            var asset = CreatePreParallelVideoAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var introCommand = FindStep(program, "chapter_01", "intro_video").Data.Command;
            var branchCommand = FindStep(program, "chapter_01", "branch_video").Data.Command;
            Assert.AreEqual(
                MediaCommandNames.VideoSeekPolicyTransition,
                introCommand.Arguments.GetString(MediaCommandNames.VideoSeekPolicyArgument));
            Assert.IsFalse(branchCommand.Arguments.TryGetValue(MediaCommandNames.VideoSeekPolicyArgument, out _));
        }

        [Test]
        public void ProgramCompiler_WhenVideoTargetsChoice_DoesNotWriteHiddenSeekPolicy()
        {
            var asset = CreateTransitionVideoAsset(videoTargetChoice: true);

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "chapter_01", "video").Data.Command;
            Assert.IsFalse(command.Arguments.TryGetValue(MediaCommandNames.VideoSeekPolicyArgument, out _));
        }

        [Test]
        public void ProgramCompiler_WhenVideoIsInsideParallel_DoesNotWriteHiddenSeekPolicy()
        {
            var asset = CreateParallelCompilerAsset(missingChoiceMerge: true);

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "chapter_01", "video").Data.Command;
            Assert.IsFalse(command.Arguments.TryGetValue(MediaCommandNames.VideoSeekPolicyArgument, out _));
        }

        [Test]
        public void ProgramCompiler_WhenVideoAndWaitChoiceAreParallel_DoesNotWriteHiddenSeekPolicy()
        {
            var asset = CreateParallelWaitChoiceAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "chapter_01", "video").Data.Command;
            Assert.IsFalse(command.Arguments.TryGetValue(MediaCommandNames.VideoSeekPolicyArgument, out _));

            var module = new StoryModule();
            module.Register(program);
            var frame = module.StartProgram("compiler_parallel_wait_choice").CurrentFrame;

            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForTime);

            frame = module.Evaluate(1.5d);

            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.AreEqual(1, frame.Choices.Count);
            Assert.AreEqual("choice", frame.Choices[0].ChoiceId);
            Assert.AreEqual("branch_interaction", frame.Choices[0].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForChoice);
            Assert.IsFalse(frame.WaitsForTime);
        }

        [Test]
        public void ProgramCompiler_WhenVideoLoops_DoesNotWriteHiddenSeekPolicy()
        {
            var asset = CreateTransitionVideoAsset(videoLoop: true);

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "chapter_01", "video").Data.Command;
            Assert.IsFalse(command.Arguments.TryGetValue(MediaCommandNames.VideoSeekPolicyArgument, out _));
        }

        [Test]
        public void StoryEditorGraph_WhenCompileSucceeds_ShowsSeekPolicyInfoDiagnostics()
        {
            var transitionAsset = CreateTransitionVideoAsset();
            var transitionWindow = CreateStoryEditorWindow(transitionAsset);
            var disabledAsset = CreateParallelWaitChoiceAsset();
            var disabledWindow = CreateStoryEditorWindow(disabledAsset);

            InvokePrivate(transitionWindow, "CompileProgram");
            InvokePrivate(disabledWindow, "CompileProgram");

            var transitionDiagnostic = GetGraphDiagnosticItems(transitionWindow).FirstOrDefault(x =>
                x.GraphDiagnostic.Severity == EditorGraphDiagnosticSeverity.Info &&
                string.Equals(x.GraphDiagnostic.NodeId, "video", StringComparison.Ordinal) &&
                x.GraphDiagnostic.Message.Contains("seek policy: transition"));
            var disabledDiagnostic = GetGraphDiagnosticItems(disabledWindow).FirstOrDefault(x =>
                x.GraphDiagnostic.Severity == EditorGraphDiagnosticSeverity.Info &&
                string.Equals(x.GraphDiagnostic.NodeId, "video", StringComparison.Ordinal) &&
                x.GraphDiagnostic.Message.Contains("seek policy: disabled"));

            Assert.IsNotNull(transitionDiagnostic);
            Assert.IsNotNull(disabledDiagnostic);
        }

        [Test]
        public void StoryEditorGraph_WhenGraphChanges_ClearsSeekPolicyDiagnostics()
        {
            var asset = CreateTransitionVideoAsset();
            var window = CreateStoryEditorWindow(asset);

            InvokePrivate(window, "CompileProgram");

            Assert.IsTrue(GetGraphDiagnosticItems(window).Any(x =>
                x.GraphDiagnostic.Severity == EditorGraphDiagnosticSeverity.Info &&
                string.Equals(x.GraphDiagnostic.NodeId, "video", StringComparison.Ordinal) &&
                x.GraphDiagnostic.Message.Contains("seek policy: transition")));

            InvokePrivate(window, "SetNodeFieldFromGraph", "video", "loop", "true");

            Assert.IsFalse(GetGraphDiagnosticItems(window).Any(x =>
                x.GraphDiagnostic.Severity == EditorGraphDiagnosticSeverity.Info &&
                string.Equals(x.GraphDiagnostic.NodeId, "video", StringComparison.Ordinal) &&
                x.GraphDiagnostic.Message.Contains("seek policy")));
        }

        [Test]
        public void StoryEditorPlaybackWindow_WhenTransitionVideoCurrent_ShowsSeekSlider()
        {
            var asset = CreateTransitionVideoAsset();
            var window = ScriptableObject.CreateInstance<PlaybackWindow>();
            m_CreatedObjects.Add(window);

            window.SetContext(asset, asset.EntryChapterId);

            var labels = FindVisualChildren<Label>(window.rootVisualElement).Select(x => x.text).ToList();
            var sliders = FindVisualChildren<Slider>(window.rootVisualElement)
                .Where(x => x.ClassListContains("story-playback__video-seek"))
                .ToList();
            var allText = string.Join("|", labels);

            Assert.IsTrue(labels.Any(x => string.Equals(x, "seek policy", StringComparison.Ordinal)), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "transition", StringComparison.Ordinal)), allText);
            Assert.AreEqual(1, sliders.Count);
        }

        [Test]
        public void StoryEditorPlaybackWindow_WhenVideoSeekPolicyDisabled_HidesSeekSlider()
        {
            var asset = CreateTransitionVideoAsset(videoTargetChoice: true);
            var window = ScriptableObject.CreateInstance<PlaybackWindow>();
            m_CreatedObjects.Add(window);

            window.SetContext(asset, asset.EntryChapterId);

            var labels = FindVisualChildren<Label>(window.rootVisualElement).Select(x => x.text).ToList();
            var sliders = FindVisualChildren<Slider>(window.rootVisualElement)
                .Where(x => x.ClassListContains("story-playback__video-seek"))
                .ToList();
            var allText = string.Join("|", labels);

            Assert.IsTrue(labels.Any(x => string.Equals(x, "seek policy", StringComparison.Ordinal)), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "disabled", StringComparison.Ordinal)), allText);
            Assert.AreEqual(0, sliders.Count);
        }

        [Test]
        public void ProgramCompiler_WhenVideoClipPathDoesNotMatchSource_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoClip: InvalidStreamingAssetsVideoPath);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:video/field:clip", issues);
            StringAssert.Contains("Video reference is invalid.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenCdnVideoReferenceCompiles_WritesFiveMediaArguments()
        {
            var asset = CreateCompilerAsset();
            var video = FindNode(asset, "video");
            var reference = new VideoReference(
                new MediaReference(MediaKind.Video, MediaSource.Cdn, "intro-cdn", "https://cdn.example.com/intro/master.m3u8"),
                VideoFormat.Hls,
                new[]
                {
                    new VideoRendition("1080p", "intro-cdn", "https://cdn.example.com/intro/1080.m3u8", 1920, 1080, 6000000, 90000)
                });
            AddOrSetParameter(video, MediaCommandNames.ClipArgument, VideoReferenceCodec.Serialize(reference));

            var program = ProgramCompiler.Compile(asset, out var report);
            var command = FindStep(program, "chapter_01", "video").Data.Command;

            AssertNoErrors(report.Issues);
            Assert.AreEqual(MediaCommandNames.VideoSourceCdn, command.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
            Assert.AreEqual("intro-cdn", command.Arguments.GetString(MediaCommandNames.MediaIdArgument));
            Assert.AreEqual("https://cdn.example.com/intro/master.m3u8", command.Arguments.GetString(MediaCommandNames.ClipArgument));
            Assert.AreEqual("hls", command.Arguments.GetString(MediaCommandNames.VideoFormatArgument));
            Assert.IsTrue(VideoReferenceCodec.TryDeserializeRenditions(
                command.Arguments.GetString(MediaCommandNames.VideoRenditionsArgument),
                out var renditions,
                out var error), error);
            Assert.AreEqual(1, renditions.Count);
            Assert.IsFalse(command.Arguments.TryGetValue(MediaCommandNames.VideoSourceArgument, out _));
        }

        [Test]
        public void ProgramCompiler_WhenLegacyStreamingVideoCompiles_ReportsMigrationWarning()
        {
            var asset = CreateCompilerAsset(videoClip: "Assets/StreamingAssets/story/intro.mp4");

            var program = ProgramCompiler.Compile(asset, out var report);
            var command = FindStep(program, "chapter_01", "video").Data.Command;
            var issues = FormatIssues(report.Issues);

            AssertNoErrors(report.Issues);
            Assert.AreEqual("story/intro.mp4", command.Arguments.GetString(MediaCommandNames.ClipArgument));
            Assert.AreEqual(MediaCommandNames.VideoSourceStreamingAssets, command.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
            StringAssert.Contains("Legacy StreamingAssets video reference", issues);
        }

        [Test]
        public void ProgramCompiler_WhenVideoSourceOptionIsInvalid_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoSource: "asset_bundle");

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:video/field:clip", issues);
            StringAssert.Contains("unsupported", issues);
        }

        [Test]
        public void ProgramCompiler_WhenVideoSourceMissing_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoSource: null);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:video/field:clip", issues);
            StringAssert.Contains("missing or unsupported", issues);
        }

        [Test]
        public void ProgramCompiler_WhenCommandRequiredFieldMissing_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoClip: null);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:video/field:clip", issues);
            StringAssert.Contains("Required video reference is missing.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenCommandBooleanFieldIsInvalid_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoLoop: "maybe");

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:video/field:loop", issues);
            StringAssert.Contains("Command field must be a boolean.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenWaitDurationIsInvalid_ReturnsLocatedError()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";
            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "wait",
                new[]
                {
                    CreateNode("wait", "等待", NodeKind.Wait, ("duration", "fast")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("wait", "completed", "完成", "end"),
                }));

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:wait/field:duration", issues);
            StringAssert.Contains("Wait duration must be a number.", issues);
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        [TestCase("-0.1")]
        public void ProgramCompiler_WhenWaitDurationIsNotFiniteOrNegative_ReturnsLocatedError(string duration)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";
            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "wait",
                new[]
                {
                    CreateNode("wait", "等待", NodeKind.Wait, ("duration", duration)),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("wait", "completed", "完成", "end"),
                }));

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            StringAssert.Contains("Wait duration must be finite and non-negative.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenWaitOwnsChoiceItems_BuildsSyntheticChoiceStep()
        {
            var asset = CreateWaitChoiceCompilerAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            var wait = FindStep(program, "chapter_01", "wait_choice");
            var choice = FindStep(program, "chapter_01", "wait_choice_choices");

            Assert.AreEqual(StepKind.Wait, wait.Kind);
            Assert.AreEqual("wait_choice_choices", wait.Data.Target.StepId);
            Assert.AreEqual(StepKind.Choice, choice.Kind);
            Assert.AreEqual(2, choice.Choices.Count);
            Assert.AreEqual("choice_a", choice.Choices[0].ChoiceId);
            Assert.AreEqual("choice.a", choice.Choices[0].TextKey);
            Assert.AreEqual("after_a", choice.Choices[0].Target.StepId);
            Assert.AreEqual("choice_b", choice.Choices[1].ChoiceId);
            Assert.AreEqual("choice.b", choice.Choices[1].TextKey);
            Assert.AreEqual("after_b", choice.Choices[1].Target.StepId);

            var module = new StoryModule();
            module.Register(program);
            var frame = module.StartProgram("compiler_wait_choice").CurrentFrame;

            Assert.AreEqual("wait_choice", frame.AnchorStep.StepId);
            Assert.IsTrue(frame.WaitsForTime);
            Assert.AreEqual(0, frame.Choices.Count);

            frame = module.Evaluate(1.5d);

            Assert.AreEqual("wait_choice_choices", frame.AnchorStep.StepId);
            Assert.IsTrue(frame.WaitsForChoice);
            Assert.AreEqual(2, frame.Choices.Count);
        }

        [Test]
        public void ProgramCompiler_WhenWaitMixesChoiceItemsAndDirectTarget_ReturnsLocatedError()
        {
            var asset = CreateWaitChoiceCompilerAsset(mixDirectTarget: true);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_wait_choice/chapter:chapter_01/node:wait_choice/port:completed", issues);
            StringAssert.Contains("Wait completed output cannot mix choice items and direct flow targets.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenChoiceItemMissingSelectedTarget_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(includeChoiceHelpSelected: false);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:choice_help/port:selected", issues);
            StringAssert.Contains("Choice item node must have exactly one selected target.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenChoiceItemHasMultipleSelectedTargets_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(addExtraChoiceHelpSelected: true);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:choice_help/port:selected", issues);
            StringAssert.Contains("Choice item node must have exactly one selected target.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenChoiceItemMissingTextKey_UsesFallbackWithWarning()
        {
            var asset = CreateCompilerAsset(choiceHelpHasTextKey: false);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:choice_help/field:textKey", issues);
            StringAssert.Contains("Choice item textKey is missing", issues);

            var choice = FindStep(program, "chapter_01", "line_intro_choices");
            Assert.AreEqual("救人", choice.Choices[0].TextKey);
        }

        [Test]
        public void ProgramCompiler_WhenEdgeTargetIsMissing_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(helpTargetNodeId: "missing_step");

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:choice_help/port:selected", issues);
            StringAssert.Contains("Target step does not exist.", issues);
            StringAssert.Contains("step:missing_step", issues);
        }

        [Test]
        public void ProgramCompiler_WhenOldAuthoringNodeExists_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset();
            asset.Chapters[0].Nodes.Add(CreateNode("old_node", "旧节点", (NodeKind)999));
            asset.Chapters[0].Edges.Add(CreateEdge("choice_help", "selected", "选择后", "old_node", "edge_help_old_node"));

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:old_node", issues);
            StringAssert.Contains("Node kind is no longer supported in Story default authoring path.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenParallelMergeAuthoringIsValid_BuildsParallelSteps()
        {
            var asset = CreateParallelCompilerAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var parallel = FindStep(program, "chapter_01", "parallel");
            var video = FindStep(program, "chapter_01", "video");
            var narration = FindStep(program, "chapter_01", "narration");
            var choice = FindStep(program, "chapter_01", "merge_choices");
            var merge = FindStep(program, "chapter_01", "merge");

            Assert.AreEqual(StepKind.Parallel, parallel.Kind);
            Assert.AreEqual(2, parallel.Data.Branches.Count);
            Assert.AreEqual("branch_video", parallel.Data.Branches[0].BranchId);
            Assert.AreEqual("video", parallel.Data.Branches[0].Entry.StepId);
            Assert.AreEqual("branch_dialogue", parallel.Data.Branches[1].BranchId);
            Assert.AreEqual("after_merge", choice.Choices[0].Target.StepId);
            Assert.AreEqual("merge", video.Data.Target.StepId);
            Assert.AreEqual("merge", narration.Data.Target.StepId);
            Assert.AreEqual(StepKind.Merge, merge.Kind);
            Assert.AreEqual("parallel", merge.Data.ParallelStepId);
            Assert.AreEqual("merge_choices", merge.Data.Target.StepId);
        }

        [Test]
        public void ProgramCompiler_WhenParallelProgramRuns_ProducesCombinedFrameAndMerges()
        {
            var asset = CreateParallelCompilerAsset();
            var program = ProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.AreEqual("video", frame.Tracks[0].Step.StepId);
            Assert.AreEqual("narration", frame.Tracks[1].Step.StepId);

            frame = runner.Continue();
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.AreEqual("video", frame.Tracks[0].Step.StepId);

            frame = runner.CompleteCommand("video", "completed");
            Assert.AreEqual("merge_choices", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Choices.Count);

            frame = runner.Select("choice");
            Assert.AreEqual("after_merge", frame.AnchorStep.StepId);
        }

        [Test]
        public void ProgramCompiler_WhenThreeBranchParallelRuns_CombinesImageAudioTextThenChoiceAfterMerge()
        {
            var asset = CreateThreeBranchParallelAsset();
            var program = ProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");
            var frame = runner.CurrentFrame;

            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(3, frame.Tracks.Count);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.AreEqual("image", frame.Tracks[0].Step.StepId);
            Assert.AreEqual("branch_image", frame.Tracks[0].BranchId);
            Assert.AreEqual("audio", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_audio", frame.Tracks[1].BranchId);
            Assert.AreEqual("line", frame.Tracks[2].Step.StepId);
            Assert.AreEqual("branch_text", frame.Tracks[2].BranchId);

            frame = runner.Continue();
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);

            frame = runner.CompleteCommand("image", "completed");
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.AreEqual("audio", frame.Tracks[0].Step.StepId);

            frame = runner.CompleteCommand("audio", "completed");
            Assert.AreEqual("merge_choices", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Choices.Count);

            frame = runner.Select("choice");
            Assert.AreEqual("after_merge", frame.AnchorStep.StepId);
        }

        [Test]
        public void ProgramCompiler_WhenParallelTrackEndsNaturally_BuildsParallelWithoutWaitNode()
        {
            var asset = CreateParallelCompilerAsset(missingChoiceMerge: true);

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var parallel = FindStep(program, "chapter_01", "parallel");
            var narration = FindStep(program, "chapter_01", "narration");

            Assert.AreEqual(StepKind.Parallel, parallel.Kind);
            Assert.AreEqual(2, parallel.Data.Branches.Count);
            Assert.AreEqual(TargetKind.StoryEnd, narration.Data.Target.TargetKind);
        }

        [Test]
        public void ProgramCompiler_WhenParallelTracksEndNaturally_CompletesAfterAllTracks()
        {
            var asset = CreateParallelCompilerAsset(missingChoiceMerge: true);
            var program = ProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);

            frame = runner.Continue();
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Tracks.Count);

            frame = runner.CompleteCommand("video", "completed");
            Assert.IsTrue(frame.IsCompleted);
            Assert.IsTrue(runner.Completed);
        }

        [Test]
        public void ProgramCompiler_WhenParallelTrackTargetsAnotherParallel_TransitionsImmediately()
        {
            var asset = CreateParallelCompilerAsset(nestedParallel: true);

            var program = ProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("nested_parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.IsTrue(frame.Tracks.Any(x => x.Step.StepId == "nested_line"));
            Assert.IsTrue(frame.Tracks.Any(x => x.Step.StepId == "nested_line_b"));
            Assert.IsFalse(frame.Tracks.Any(x => x.Step.StepId == "video"));
        }

        [Test]
        public void ProgramCompiler_WhenParallelTrackJumpsChapter_CompilesAndTransitions()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";
            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "parallel",
                new[]
                {
                    CreateNode("parallel", "并行", NodeKind.Parallel),
                    CreateNode(
                        "video",
                        "播放视频",
                        NodeKind.PlayVideo,
                        (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                        ("clip", SampleGraphFixture.IntroVideoPath),
                        ("wait", "true")),
                    CreateNode("line", "旁白", NodeKind.Narration, ("textKey", "parallel.line")),
                    CreateNode("jump_next", "跳转章节", NodeKind.JumpChapter, ("chapterId", "chapter_02")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("parallel", "branch_video", "视频轨", "video"),
                    CreateEdge("parallel", "branch_text", "文本轨", "line"),
                    CreateEdge("video", "completed", "完成", "jump_next"),
                }));
            asset.Chapters.Add(CreateChapter(
                "chapter_02",
                "第二章",
                "target_line",
                new[]
                {
                    CreateNode("target_line", "目标对白", NodeKind.Narration, ("textKey", "chapter.02.line")),
                    CreateNode("target_end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("target_line", "completed", "完成", "target_end"),
                }));

            var program = ProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);

            frame = runner.Continue();
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Tracks.Count);

            frame = runner.CompleteCommand("video", "completed");
            Assert.AreEqual("target_line", frame.AnchorStep.StepId);
            Assert.AreEqual("chapter_02", frame.Chapter.ChapterId);
        }

        [Test]
        public void ProgramCompiler_WhenParallelLineHasChoice_ShowsChoiceInsideParallelFrame()
        {
            var asset = CreateParallelCompilerAsset(choiceInsideParallel: true);

            var program = ProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var narration = FindStep(program, "chapter_01", "narration");
            var syntheticChoice = FindStep(program, "chapter_01", "narration_choices");
            Assert.AreEqual("narration_choices", narration.Data.Target.StepId);
            Assert.AreEqual(StepKind.Choice, syntheticChoice.Kind);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.AreEqual(1, frame.Choices.Count);
            Assert.IsTrue(frame.WaitsForChoice);

            frame = runner.Select("choice");
            Assert.AreEqual("after_merge", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.AreEqual("after_merge", frame.Tracks[0].Step.StepId);
            Assert.IsFalse(frame.Tracks.Any(x => x.Step.StepId == "video"));

            frame = runner.Continue();
            Assert.IsTrue(frame.IsCompleted);
            Assert.IsTrue(runner.Completed);
        }

        [Test]
        public void ProgramCompiler_WhenParallelChoiceSelected_DoesNotPlayUnselectedTarget()
        {
            var asset = CreateParallelChoiceTargetAsset();
            var program = ProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);

            var selectedAudio = FindStep(program, "chapter_01", "selected_audio");
            var unselectedImage = FindStep(program, "chapter_01", "unselected_image");
            Assert.AreEqual(TargetKind.StoryEnd, selectedAudio.Data.Target.TargetKind);
            Assert.AreEqual(TargetKind.StoryEnd, unselectedImage.Data.Target.TargetKind);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.AreEqual(2, frame.Choices.Count);

            frame = runner.Select("choice_a");
            Assert.AreEqual("selected_audio", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.IsTrue(frame.Tracks.Any(x => x.Step.StepId == "selected_audio"));
            Assert.IsFalse(frame.Tracks.Any(x => x.Step.StepId == "unselected_image"));

            frame = runner.Continue();
            Assert.IsFalse(frame.Tracks.Any(x => x.Step.StepId == "unselected_image"));
        }

        [Test]
        public void ProgramCompiler_WhenParallelChoiceSelected_DoesNotWaitForOtherTracks()
        {
            var asset = CreateParallelChoiceTargetAsset();
            var program = ProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.IsTrue(frame.WaitsForChoice);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.Tracks.Any(x => x.Step.StepId == "video"));

            frame = runner.Select("choice_a");

            Assert.AreEqual("selected_audio", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.AreEqual("selected_audio", frame.Tracks[0].Step.StepId);
            Assert.IsFalse(frame.Tracks.Any(x => x.Step.StepId == "video"));
            Assert.IsFalse(frame.Choices.Any(x => string.Equals(x.ChoiceId, "choice_b", StringComparison.Ordinal)));
        }

        [Test]
        public void ProgramCompiler_WhenParallelChoiceTargetStartsAnotherParallel_DoesNotTreatItAsNestedParallel()
        {
            var asset = CreateParallelChoiceTargetParallelAsset();
            var program = ProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.AreEqual(2, frame.Choices.Count);

            frame = runner.Select("choice_a");
            Assert.AreEqual("after_choice_parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.IsTrue(frame.Tracks.Any(x => x.Step.StepId == "after_audio"));
            Assert.IsTrue(frame.Tracks.Any(x => x.Step.StepId == "after_video"));
            Assert.IsFalse(frame.Tracks.Any(x => x.Step.StepId == "unselected_image"));
        }

        [Test]
        public void ProgramCompiler_WhenMergeHasMixedParallelOwners_ReturnsLocatedError()
        {
            var asset = CreateParallelCompilerAsset(mixedMergeOwners: true);

            var program = ProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:merge", issues);
            StringAssert.Contains("Merge node cannot belong to multiple Parallel blocks.", issues);
        }

        [Test]
        public void AuthoringAsset_WhenDefaultsEnsured_KeepsSingleChapterStartAndEnd()
        {
            var asset = CreateAsset();
            asset.StoryId = "story";
            asset.EntryChapterId = "chapter_01";

            var chapter = new AuthoringChapter
            {
                ChapterId = "chapter_01",
                Title = "第一章",
                EntryNodeId = "wrong_entry"
            };
            chapter.Nodes.Add(new AuthoringNode { NodeId = "line", Title = "对白", NodeKind = NodeKind.Dialogue });
            chapter.Nodes.Add(new AuthoringNode { NodeId = "start_a", Title = "开始 A", NodeKind = NodeKind.Start });
            chapter.Nodes.Add(new AuthoringNode { NodeId = "start_b", Title = "开始 B", NodeKind = NodeKind.Start });
            chapter.Nodes.Add(new AuthoringNode { NodeId = "end_a", Title = "结束 A", NodeKind = NodeKind.End });
            chapter.Nodes.Add(new AuthoringNode { NodeId = "end_b", Title = "结束 B", NodeKind = NodeKind.End });
            chapter.Edges.Add(CreateEdge("start_b", "completed", "完成", "line", "edge_duplicate_start"));
            chapter.Edges.Add(CreateEdge("line", "completed", "完成", "end_b", "edge_duplicate_end"));
            asset.Chapters.Add(chapter);

            asset.EnsureDefaults();

            var starts = chapter.Nodes.Where(x => x.NodeKind == NodeKind.Start).ToList();
            var ends = chapter.Nodes.Where(x => x.NodeKind == NodeKind.End).ToList();
            Assert.AreEqual(1, starts.Count);
            Assert.AreEqual(1, ends.Count);
            Assert.AreEqual("start_a", starts[0].NodeId);
            Assert.AreEqual("end_a", ends[0].NodeId);
            Assert.AreEqual(starts[0].NodeId, chapter.EntryNodeId);
            Assert.IsFalse(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "start_b", StringComparison.Ordinal) ||
                string.Equals(x.TargetNodeId, "start_b", StringComparison.Ordinal) ||
                string.Equals(x.FromNodeId, "end_b", StringComparison.Ordinal) ||
                string.Equals(x.TargetNodeId, "end_b", StringComparison.Ordinal)));
        }

        [Test]
        public void StoryEditorLayout_WhenBuilt_UsesGraphFirstChineseShell()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);

            var root = window.rootVisualElement.Q(className: "story-editor");
            var treeRows = window.rootVisualElement.Query<VisualElement>(className: "story-editor__tree-row").ToList();
            var treeLabels = treeRows.Select(GetVisualText).ToList();
            var paletteItems = window.rootVisualElement.Query<VisualElement>(className: "editor-node-graph-palette__item").ToList();
            var paletteLabels = paletteItems
                .SelectMany(x => FindVisualChildren<Label>(x))
                .Select(x => x.text)
                .ToList();
            var inspector = window.rootVisualElement.Q<ScrollView>(className: "story-editor__inspector");
            var allText = string.Join("|", FindVisualChildren<Label>(window.rootVisualElement).Select(x => x.text)
                .Concat(window.rootVisualElement.Query<Button>().ToList().Select(x => x.text))
                .Concat(window.rootVisualElement.Query<TextField>().ToList().Select(x => x.label))
                .Concat(window.rootVisualElement.Query<DropdownField>().ToList().Select(x => x.label)));

            Assert.IsNotNull(root);
            Assert.IsNotNull(window.rootVisualElement.Q(className: "editor-node-graph"));
            Assert.IsNull(inspector);
            Assert.IsTrue(FindVisualChildren<VisualElement>(window.rootVisualElement).Any(x => string.Equals(x.GetType().Name, "EditorNodeGraphMiniMap", StringComparison.Ordinal)));
            Assert.IsFalse(treeLabels.Any(x => string.Equals(x, "剧情  story", StringComparison.Ordinal)), string.Join(",", treeLabels));
            Assert.IsTrue(treeLabels.Any(x => x.Contains("章节")), string.Join(",", treeLabels));
            CollectionAssert.Contains(paletteLabels, "对白");
            CollectionAssert.Contains(paletteLabels, "播放视频");
            CollectionAssert.Contains(paletteLabels, "并行");
            CollectionAssert.Contains(paletteLabels, "等待全部完成");
            CollectionAssert.DoesNotContain(paletteLabels, "标记检查");
            CollectionAssert.DoesNotContain(paletteLabels, "注释");
            Assert.IsFalse(window.rootVisualElement.Query<Button>(className: "editor-node-graph-palette__item").ToList().Any());
            Assert.IsFalse(allText.IndexOf("unit", StringComparison.OrdinalIgnoreCase) >= 0, allText);
            Assert.IsFalse(allText.IndexOf("payload", StringComparison.OrdinalIgnoreCase) >= 0, allText);
            Assert.IsFalse(allText.IndexOf("owner", StringComparison.OrdinalIgnoreCase) >= 0, allText);
        }

        [Test]
        public void StoryEditorToolbar_WhenBuilt_UsesMinimalActions()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);

            var buttons = window.rootVisualElement.Query<Button>().ToList().Select(x => x.text).ToList();

            CollectionAssert.Contains(buttons, "新建");
            CollectionAssert.Contains(buttons, "打开");
            CollectionAssert.Contains(buttons, "保存");
            CollectionAssert.Contains(buttons, "编译");
            CollectionAssert.DoesNotContain(buttons, "打开样例");
            CollectionAssert.DoesNotContain(buttons, "播放窗口");
            CollectionAssert.DoesNotContain(buttons, "新增章节");
            CollectionAssert.DoesNotContain(buttons, "添加节点");
            CollectionAssert.DoesNotContain(buttons, "删除选中");
        }

        [Test]
        public void StoryEditorCompile_WhenAssetIsProjectAsset_ExportsRuntimeProgramAsset()
        {
            const string authoringPath = "Assets/Bundles/Story/__StoryEditorCompileExportTest.asset";
            const string programPath = "Assets/Bundles/Story/compiler_story.runtime.asset";
            AssetDatabase.DeleteAsset(authoringPath);
            AssetDatabase.DeleteAsset(programPath);
            m_CreatedAssetPaths.Add(authoringPath);
            m_CreatedAssetPaths.Add(programPath);
            EnsureFolder(Path.GetDirectoryName(authoringPath)?.Replace('\\', '/'));

            var asset = CreateCompilerAsset();
            AssetDatabase.CreateAsset(asset, authoringPath);
            m_CreatedObjects.Remove(asset);
            var runtimeAsset = ScriptableObject.CreateInstance<ProgramAsset>();
            AssetDatabase.CreateAsset(runtimeAsset, programPath);
            asset.RuntimeProgramAssetPath = programPath;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var window = CreateStoryEditorWindow(asset);
            InvokePrivate(window, "CompileProgram");

            var exported = AssetDatabase.LoadAssetAtPath<ProgramAsset>(programPath);
            Assert.IsNotNull(exported);
            Assert.AreEqual(programPath, asset.RuntimeProgramAssetPath);
            Assert.AreEqual("compiler_story", exported.ToProgram().StoryId);
        }

        [Test]
        public void StoryEditorBlackboard_WhenBuilt_ShowsChapterPlayButton()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);

            var blackboard = window.rootVisualElement.Q(className: "editor-node-graph__blackboard");
            var buttons = blackboard.Query<Button>().ToList().Select(x => x.text).ToList();
            var labels = FindVisualChildren<Label>(blackboard).Select(x => x.text).ToList();

            Assert.AreNotEqual(DisplayStyle.None, blackboard.resolvedStyle.display);
            CollectionAssert.Contains(buttons, "播放章节");
            Assert.IsTrue(labels.Any(x => x.Contains("当前章节")), string.Join("|", labels));
        }

        [Test]
        public void StoryEditorChapterAction_WhenQuickCheckRuns_PreviewsCurrentChapter()
        {
            var asset = CreateSemanticGraphAsset();
            AddOrSetParameter(FindNode(asset, "video"), MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets);
            AddOrSetParameter(FindNode(asset, "video"), "clip", SampleGraphFixture.IntroVideoPath);
            var window = CreateStoryEditorWindow(asset);

            InvokePrivate(window, "PlaySelectedChapter");

            var labels = FindVisualChildren<Label>(window.rootVisualElement).Select(x => x.text).ToList();
            Assert.IsTrue(labels.Any(x => x.Contains("播放通过")), string.Join("|", labels));
        }

        [Test]
        public void StoryEditorBlackboardPlay_WhenInvoked_OpensPlaybackWindowForCurrentChapter()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                LogAssert.Expect(LogType.Error, "No graphic device is available to initialize the view.");
                LogAssert.Expect(LogType.Error, "No graphic device is available to show the window.");
                LogAssert.Expect(LogType.Error, "No graphic device is available to initialize the view.");
            }

            InvokePrivate(window, "OpenPlaybackWindow");
            var playbackWindow = Resources.FindObjectsOfTypeAll<PlaybackWindow>()
                .FirstOrDefault(x => ReferenceEquals(GetPrivateField<AuthoringAsset>(x, "m_Asset"), asset));
            if (playbackWindow != null)
            {
                m_CreatedObjects.Add(playbackWindow);
            }

            Assert.IsNotNull(playbackWindow);
            Assert.AreSame(asset, GetPrivateField<AuthoringAsset>(playbackWindow, "m_Asset"));
            Assert.AreEqual(asset.EntryChapterId, GetPrivateField<string>(playbackWindow, "m_ChapterId"));
        }

        [Test]
        public void StoryPlayback_WhenPlayerViewPrefabExists_UsesRuntimeAssembly()
        {
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameDeveloperKit/Runtime/Story/Playback/PlayerView.prefab");
            Assert.IsNotNull(prefabRoot);
            var prefab = prefabRoot.GetComponent<PlayerView>();
            Assert.IsNotNull(prefab);
            Assert.IsFalse(prefab.gameObject.scene.IsValid());
            Assert.AreEqual("GameDeveloperKit.Runtime", typeof(PlayerView).Assembly.GetName().Name);

            var surface = prefab.CreateDefaultSurfaceView();
            Assert.IsNotNull(surface.VideoSeek);
            Assert.IsNotNull(surface.VideoSeek.Root);
            Assert.IsNotNull(surface.VideoSeek.Slider);
            Assert.IsNotNull(surface.VideoSeek.PauseButton);
            Assert.IsFalse(surface.VideoSeek.Root.gameObject.activeSelf);
        }

        [Test]
        public void StoryEditorGraph_WhenNodeSelected_ShowsInlineSchemaFieldsNotLegacyFields()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);

            var videoNode = FindStoryEditorNodeView(window, "video");
            var inspector = window.rootVisualElement.Q<ScrollView>(className: "story-editor__inspector");
            var textFields = videoNode.Query<TextField>().ToList();
            var objectFields = videoNode.Query<ObjectField>().ToList();
            var dropdowns = videoNode.Query<DropdownField>().ToList();
            var toggles = videoNode.Query<Toggle>().ToList();
            var labels = textFields.Select(x => x.label).ToList();
            var tooltips = textFields.Select(x => x.tooltip)
                .Concat(objectFields.Select(x => x.tooltip))
                .Concat(toggles.Select(x => x.tooltip))
                .Concat(videoNode.Query<VisualElement>(className: "editor-node-graph-node__field").ToList().Select(x => x.tooltip))
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .ToList();
            var nodeText = string.Join("|", FindVisualChildren<Label>(videoNode).Select(x => x.text)
                .Concat(textFields.Select(x => x.label))
                .Concat(objectFields.Select(x => x.label))
                .Concat(toggles.Select(x => x.label)));

            Assert.IsNull(inspector);
            CollectionAssert.DoesNotContain(labels, "标题");
            Assert.IsFalse(dropdowns.Any(x => x.label == "来源 *"));
            Assert.IsFalse(objectFields.Any(x => x.label == "视频 *"));
            CollectionAssert.Contains(videoNode.Query<Button>().ToList().Select(x => x.text).ToList(), "选择视频");
            CollectionAssert.Contains(videoNode.Query<Button>().ToList().Select(x => x.text).ToList(), "清除");
            CollectionAssert.Contains(toggles.Select(x => x.label).ToList(), "等待完成");
            Assert.IsTrue(tooltips.Any(x => x.Contains("参数键：clip")));
            Assert.IsFalse(videoNode.Query<EnumField>().ToList().Any(x => x.label == "节点类型"));
            Assert.IsFalse(nodeText.IndexOf("Payload", StringComparison.OrdinalIgnoreCase) >= 0, nodeText);
            Assert.IsFalse(nodeText.IndexOf("Owner", StringComparison.OrdinalIgnoreCase) >= 0, nodeText);
        }

        [Test]
        public void StoryEditorGraph_WhenAssetFieldWritesStableId_StoresStringParameterOnly()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var video = asset.Chapters[0].Nodes.First(x => x.NodeId == "video");

            InvokePrivate(window, "SetNodeFieldFromGraph", "video", "clip", SampleGraphFixture.IntroVideoPath);

            Assert.AreEqual(SampleGraphFixture.IntroVideoPath, video.Parameters.First(x => x.Key == "clip").Value);
        }

        [Test]
        public void StoryEditorGraph_WhenJumpChapterNodeSelected_UsesDropdownAndExcludesCurrentChapter()
        {
            var asset = CreateSemanticGraphAsset();
            var chapter = asset.Chapters[0];
            chapter.Nodes.Add(CreateNode("jump", "跳转章节", NodeKind.JumpChapter, ("chapterId", "chapter_02")));
            var window = CreateStoryEditorWindow(asset);

            var jumpNode = FindStoryEditorNodeView(window, "jump");
            var dropdown = jumpNode.Query<DropdownField>().ToList().FirstOrDefault(x => x.label == "章节 *");

            Assert.IsNotNull(dropdown);
            CollectionAssert.AreEqual(new[] { "第二章 (chapter_02)" }, dropdown.choices);
            Assert.AreEqual("第二章 (chapter_02)", dropdown.value);
        }

        [Test]
        public void StoryEditorGraph_WhenRequiredFieldMissing_ShowsFieldDiagnosticAndChineseSummary()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);

            var videoNode = FindStoryEditorNodeView(window, "video");
            var field = videoNode.Query<VisualElement>(className: "editor-node-graph-node__field").ToList()
                .FirstOrDefault(x => FindVisualChildren<Button>(x).Any(button => button.text == "选择视频"));
            var summary = GetIssueRows(window)
                .FirstOrDefault(x =>
                    GetVisualText(x).Contains("必填命令字段未填写") &&
                    x.tooltip.Contains("field:clip"));

            Assert.IsNotNull(field);
            Assert.IsNotNull(summary);
            Assert.IsTrue(videoNode.ClassListContains("editor-node-graph-node--diagnostic-error"));
            Assert.IsTrue(field.ClassListContains("editor-node-graph-node__field--diagnostic-error"));
            StringAssert.Contains("必填命令字段未填写", GetVisualText(summary));
            StringAssert.Contains("field:clip", summary.tooltip);
        }

        [Test]
        public void StoryEditorGraph_WhenNumberFieldInvalid_ShowsFieldDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Chapters[0].Nodes.Add(CreateNode("wait_invalid", "等待", NodeKind.Wait, ("duration", "fast")));
            var window = CreateStoryEditorWindow(asset);

            var waitNode = FindStoryEditorNodeView(window, "wait_invalid");
            var duration = waitNode.Query<FloatField>().ToList().First(x => x.label == "时长");
            var summaryText = GetIssueSummaryText(window);

            Assert.IsTrue(duration.ClassListContains("editor-node-graph-node__field--diagnostic-error"));
            StringAssert.Contains("当前值不是数字", duration.tooltip);
            StringAssert.Contains("字段必须填写数字", summaryText);
        }

        [Test]
        public void StoryEditorGraph_WhenBooleanFieldInvalid_ShowsFieldDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            var video = asset.Chapters[0].Nodes.First(x => x.NodeId == "video");
            AddOrSetParameter(video, MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets);
            AddOrSetParameter(video, "clip", SampleGraphFixture.IntroVideoPath);
            AddOrSetParameter(video, "wait", "maybe");
            var window = CreateStoryEditorWindow(asset);

            var videoNode = FindStoryEditorNodeView(window, "video");
            var wait = videoNode.Query<Toggle>().ToList().First(x => x.label == "等待完成");
            var summaryText = GetIssueSummaryText(window);

            Assert.IsTrue(wait.ClassListContains("editor-node-graph-node__field--diagnostic-error"));
            StringAssert.Contains("只能填写 true 或 false", wait.tooltip);
            StringAssert.Contains("字段必须填写布尔值", summaryText);
        }

        [Test]
        public void StoryEditorGraph_WhenVideoSourceOptionIsInvalid_ShowsFieldDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            var video = asset.Chapters[0].Nodes.First(x => x.NodeId == "video");
            AddOrSetParameter(video, MediaCommandNames.VideoSourceArgument, "asset_bundle");
            AddOrSetParameter(video, "clip", SampleGraphFixture.IntroVideoPath);
            var window = CreateStoryEditorWindow(asset);

            var videoNode = FindStoryEditorNodeView(window, "video");
            var field = videoNode.Query<VisualElement>(className: "editor-node-graph-node__field").ToList()
                .FirstOrDefault(x => FindVisualChildren<Button>(x).Any(button => button.text == "更换视频"));
            var summaryText = GetIssueSummaryText(window);

            Assert.IsNotNull(field);
            Assert.IsTrue(field.ClassListContains("editor-node-graph-node__field--diagnostic-error"));
            StringAssert.Contains("视频只支持 CDN", field.tooltip);
            StringAssert.Contains("视频引用无效", summaryText);
        }

        [Test]
        public void StoryEditorGraph_WhenVideoClipPathDoesNotMatchSource_ShowsErrorDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            var video = asset.Chapters[0].Nodes.First(x => x.NodeId == "video");
            AddOrSetParameter(video, MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets);
            AddOrSetParameter(video, "clip", InvalidStreamingAssetsVideoPath);
            var window = CreateStoryEditorWindow(asset);

            var videoNode = FindStoryEditorNodeView(window, "video");
            var clipField = videoNode.Query<VisualElement>(className: "editor-node-graph-node__field").ToList()
                .FirstOrDefault(x => FindVisualChildren<Button>(x).Any(button => button.text == "更换视频"));
            var summaryText = GetIssueSummaryText(window);

            Assert.IsNotNull(clipField);
            Assert.IsTrue(clipField.ClassListContains("editor-node-graph-node__field--diagnostic-error"));
            StringAssert.Contains("视频只支持 CDN", clipField.tooltip);
            StringAssert.Contains("视频引用无效", summaryText);
        }

        [Test]
        public void StoryEditorGraph_WhenChoiceMissingSelectedTarget_ShowsPortDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Chapters[0].Edges.RemoveAll(x => x.FromNodeId == "choice" && x.FromPortId == "selected");
            var window = CreateStoryEditorWindow(asset);

            var choiceNode = FindStoryEditorNodeView(window, "choice");
            var selectedPort = choiceNode.Query<VisualElement>(className: "editor-node-graph-node__port-dot").ToList()
                .First(x => x.userData is EditorGraphPortRef port && port.PortId == "selected");
            var summaryText = GetIssueSummaryText(window);

            Assert.IsTrue(choiceNode.ClassListContains("editor-node-graph-node--diagnostic-error"));
            Assert.IsTrue(selectedPort.ClassListContains("editor-node-graph-node__port-dot--diagnostic-error"));
            StringAssert.Contains("需要且只能有一条选择后的目标连线", selectedPort.tooltip);
            StringAssert.Contains("选项必须且只能连接一个", summaryText);
        }

        [Test]
        public void StoryEditorGraph_WhenLineMixesChoiceAndDirectTargets_ShowsCompletedPortDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Chapters[0].Edges.Add(CreateEdge("line_intro", "completed", "完成", "mini_game", "edge_line_intro_direct"));
            var window = CreateStoryEditorWindow(asset);

            var lineNode = FindStoryEditorNodeView(window, "line_intro");
            var completedPort = lineNode.Query<VisualElement>(className: "editor-node-graph-node__port-dot").ToList()
                .First(x => x.userData is EditorGraphPortRef port && port.PortId == "completed");
            var summaryText = GetIssueSummaryText(window);

            Assert.IsTrue(completedPort.ClassListContains("editor-node-graph-node__port-dot--diagnostic-error"));
            StringAssert.Contains("completed 端口不能再直连普通节点", completedPort.tooltip);
            StringAssert.Contains("完成端口不能同时连接选项和普通流程", summaryText);
        }

        [Test]
        public void StoryEditorGraph_WhenWaitMixesChoiceAndDirectTargets_ShowsCompletedPortDiagnostic()
        {
            var asset = CreateWaitChoiceCompilerAsset(mixDirectTarget: true);
            var window = CreateStoryEditorWindow(asset);

            var waitNode = FindStoryEditorNodeView(window, "wait_choice");
            var completedPort = waitNode.Query<VisualElement>(className: "editor-node-graph-node__port-dot").ToList()
                .First(x => x.userData is EditorGraphPortRef port && port.PortId == "completed");
            var summaryText = GetIssueSummaryText(window);

            Assert.IsTrue(completedPort.ClassListContains("editor-node-graph-node__port-dot--diagnostic-error"));
            StringAssert.Contains("completed 端口不能再直连普通节点", completedPort.tooltip);
            StringAssert.Contains("完成端口不能同时连接选项和普通流程", summaryText);
        }

        [Test]
        public void StoryEditorGraph_WhenEdgeUsesUnknownOutputPort_PrioritizesWireDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Chapters[0].Edges.Add(CreateEdge("video", "unknown", "未知", "line_intro", "edge_video_unknown"));
            var window = CreateStoryEditorWindow(asset);

            var diagnostic = GetGraphDiagnosticItems(window).First(x => x.Source.Contains("edge:edge_video_unknown"));

            Assert.AreEqual(EditorGraphDiagnosticTargetKind.Wire, diagnostic.GraphDiagnostic.TargetKind);
            Assert.AreEqual("edge_video_unknown", diagnostic.GraphDiagnostic.WireId);
            Assert.AreEqual("输出端口未在节点 schema 中声明。", diagnostic.GraphDiagnostic.Message);
        }

        [Test]
        public void StoryEditorGraph_WhenEdgeTargetIsMissing_ShowsPortDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Chapters[0].Edges.Add(CreateEdge("video", "completed", "完成", "missing", "edge_video_missing"));
            var window = CreateStoryEditorWindow(asset);

            var diagnostic = GetGraphDiagnosticItems(window).First(x => x.Source.Contains("edge:edge_video_missing"));

            Assert.AreEqual(EditorGraphDiagnosticTargetKind.Port, diagnostic.GraphDiagnostic.TargetKind);
            Assert.AreEqual("video", diagnostic.GraphDiagnostic.NodeId);
            Assert.AreEqual("completed", diagnostic.GraphDiagnostic.PortId);
            StringAssert.Contains("连线目标节点不存在", diagnostic.GraphDiagnostic.Message);
        }

        [Test]
        public void StoryEditorGraph_WhenParallelTrackEndsNaturally_DoesNotShowWaitNodeDiagnostic()
        {
            var asset = CreateParallelCompilerAsset(missingChoiceMerge: true);
            var window = CreateStoryEditorWindow(asset);

            var diagnostics = GetGraphDiagnosticItems(window);

            Assert.IsFalse(
                diagnostics.Any(x =>
                    x.GraphDiagnostic.NodeId == "parallel" &&
                    x.GraphDiagnostic.PortId == "branch_dialogue" &&
                    x.GraphDiagnostic.Severity == EditorGraphDiagnosticSeverity.Error),
                string.Join(Environment.NewLine, diagnostics.Select(x => x.GraphDiagnostic.Message)));
        }

        [Test]
        public void StoryEditorGraph_WhenSummaryClicked_SelectsDiagnosticNode()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var item = GetGraphDiagnosticItems(window).First(x =>
                x.GraphDiagnostic.Message.Contains("必填命令字段未填写") && x.Source.Contains("node:video"));

            InvokePrivate(window, "FocusDiagnostic", item.RawItem);

            var selectedNode = GetPrivateField<AuthoringNode>(window, "m_SelectedNode");
            var videoNode = FindStoryEditorNodeView(window, "video");
            Assert.AreEqual("video", selectedNode.NodeId);
            Assert.IsTrue(videoNode.ClassListContains("editor-node-graph-node--selected"));
        }

        [Test]
        public void StoryEditorGraph_WhenSummaryClickedForWireIssue_SelectsDiagnosticWire()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Chapters[0].Edges.Add(CreateEdge("video", "unknown", "未知", "line_intro", "edge_video_unknown"));
            var window = CreateStoryEditorWindow(asset);
            var item = GetGraphDiagnosticItems(window).First(x => x.Source.Contains("edge:edge_video_unknown"));

            InvokePrivate(window, "FocusDiagnostic", item.RawItem);

            var selectedEdge = GetPrivateField<AuthoringEdge>(window, "m_SelectedEdge");
            Assert.IsNotNull(selectedEdge);
            Assert.AreEqual("edge_video_unknown", selectedEdge.EdgeId);
        }

        [Test]
        public void StoryEditorGraph_WhenNodesAddedFromTemplate_CreatesAndConnectsReadableEdge()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var chapter = asset.Chapters[0];

            InvokePrivate(window, "SetNodeFieldFromGraph", "video", MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets);
            InvokePrivate(window, "SetNodeFieldFromGraph", "video", "clip", SampleGraphFixture.IntroVideoPath);
            var command = InvokePrivate<AuthoringNode>(
                window,
                "AddNodeAt",
                new Vector2(360f, 140f),
                NodeKind.PlayAudio,
                FindNode(asset, "choice"),
                "selected",
                "选择后");

            Assert.IsNotNull(command);
            Assert.AreEqual(NodeKind.PlayAudio, command.NodeKind);
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "choice", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, command.NodeId, StringComparison.Ordinal) &&
                string.Equals(x.FromPortLabel, "选择后", StringComparison.Ordinal)));
            Assert.IsFalse(chapter.Edges.Any(x => x.FromPortLabel.StartsWith("next_", StringComparison.Ordinal)), string.Join(",", chapter.Edges.Select(x => x.FromPortLabel)));
        }

        [Test]
        public void StoryEditorGraph_WhenChapterOpened_KeepsStartEndFixed()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var chapter = asset.Chapters[0];
            var originalCount = chapter.Nodes.Count;
            var end = chapter.Nodes.First(x => x.NodeKind == NodeKind.End);

            var deniedStart = InvokePrivate<AuthoringNode>(window, "AddNodeAt", Vector2.zero, NodeKind.Start, null, null, null);
            var deniedEnd = InvokePrivate<AuthoringNode>(window, "AddNodeAt", Vector2.zero, NodeKind.End, null, null, null);
            InvokePrivate(window, "SelectNode", end);
            InvokePrivate(window, "RemoveSelection");

            Assert.AreEqual(1, chapter.Nodes.Count(x => x.NodeKind == NodeKind.Start));
            Assert.AreEqual(1, chapter.Nodes.Count(x => x.NodeKind == NodeKind.End));
            Assert.AreEqual(chapter.Nodes.First(x => x.NodeKind == NodeKind.Start).NodeId, chapter.EntryNodeId);
            Assert.AreEqual(originalCount, chapter.Nodes.Count);
            Assert.IsNull(deniedStart);
            Assert.IsNull(deniedEnd);
            Assert.IsTrue(chapter.Nodes.Contains(end));
        }

        [Test]
        public void StoryEditorGraph_WhenMultipleNodesSelectedFromGraph_DeletesSelectedNodes()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var chapter = asset.Chapters[0];

            InvokePrivate(window, "SelectNodesFromGraph", (object)new[] { "video", "choice" });
            InvokePrivate(window, "DeleteSelectionFromGraph");

            Assert.IsFalse(chapter.Nodes.Any(x => x.NodeId == "video"));
            Assert.IsFalse(chapter.Nodes.Any(x => x.NodeId == "choice"));
            Assert.IsFalse(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video", StringComparison.Ordinal) ||
                string.Equals(x.TargetNodeId, "video", StringComparison.Ordinal) ||
                string.Equals(x.FromNodeId, "choice", StringComparison.Ordinal) ||
                string.Equals(x.TargetNodeId, "choice", StringComparison.Ordinal)));
        }

        [Test]
        public void StoryEditorGraphAdapter_WhenTemplatesBuilt_IncludesInteractionPatterns()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var adapter = GetPrivateField<IEditorNodeGraphAdapter>(window, "m_GraphAdapter");

            var templates = adapter.Templates.ToList();

            Assert.IsTrue(templates.Any(x => string.Equals(x.TemplateId, "story.pattern.video_wait_choice", StringComparison.Ordinal) && string.Equals(x.DisplayName, "视频中途选项", StringComparison.Ordinal)));
            Assert.IsTrue(templates.Any(x => string.Equals(x.TemplateId, "story.pattern.video_wait_qte", StringComparison.Ordinal) && string.Equals(x.DisplayName, "视频中途 QTE", StringComparison.Ordinal)));
            Assert.IsTrue(templates.Any(x => string.Equals(x.TemplateId, "story.pattern.video_wait_unlock", StringComparison.Ordinal) && string.Equals(x.DisplayName, "视频中途 Unlock", StringComparison.Ordinal)));
            Assert.IsTrue(templates.Any(x => string.Equals(x.TemplateId, NodeKind.PlayVideo.ToString(), StringComparison.Ordinal)));
        }

        [Test]
        public void StoryEditorGraph_WhenVideoWaitChoicePatternCreated_BuildsTemplateGraph()
        {
            var asset = CreateAsset();
            var window = CreateStoryEditorWindow(asset);
            var adapter = GetPrivateField<IEditorNodeGraphAdapter>(window, "m_GraphAdapter");
            var template = adapter.Templates.First(x => string.Equals(x.TemplateId, "story.pattern.video_wait_choice", StringComparison.Ordinal));
            var chapter = asset.Chapters[0];

            adapter.CreateNode(template, new Vector2(240f, 180f), new EditorGraphPortRef(chapter.EntryNodeId, "completed"));
            InvokePrivate(window, "SetNodeFieldFromGraph", "video_wait_choice_video", MediaCommandNames.ClipArgument, SampleGraphFixture.IntroVideoPath);

            Assert.IsNotNull(FindNode(asset, "video_wait_choice_parallel"));
            Assert.IsNotNull(FindNode(asset, "video_wait_choice_video"));
            Assert.IsNotNull(FindNode(asset, "video_wait_choice_wait"));
            Assert.IsNotNull(FindNode(asset, "video_wait_choice_option_a"));
            Assert.IsNotNull(FindNode(asset, "video_wait_choice_option_b"));
            Assert.IsNotNull(FindNode(asset, "video_wait_choice_option_a_target"));
            Assert.IsNotNull(FindNode(asset, "video_wait_choice_option_b_target"));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, chapter.EntryNodeId, StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_choice_parallel", StringComparison.Ordinal)));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video_wait_choice_parallel", StringComparison.Ordinal) &&
                string.Equals(x.FromPortId, "branch_1", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_choice_video", StringComparison.Ordinal)));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video_wait_choice_parallel", StringComparison.Ordinal) &&
                string.Equals(x.FromPortId, "branch_2", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_choice_wait", StringComparison.Ordinal)));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video_wait_choice_wait", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_choice_option_a", StringComparison.Ordinal)));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video_wait_choice_wait", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_choice_option_b", StringComparison.Ordinal)));

            var program = ProgramCompiler.Compile(asset, out var report);
            var choice = FindStep(program, chapter.ChapterId, "video_wait_choice_wait_choices");

            AssertNoErrors(report.Issues);
            Assert.AreEqual(StepKind.Choice, choice.Kind);
            Assert.AreEqual(2, choice.Choices.Count);
            Assert.AreEqual("video_wait_choice_option_a_target", choice.Choices[0].Target.StepId);
            Assert.AreEqual("video_wait_choice_option_b_target", choice.Choices[1].Target.StepId);
        }

        [Test]
        public void StoryEditorGraph_WhenVideoWaitQtePatternCreated_CompilesCommandOutcomes()
        {
            var asset = CreateAsset();
            var window = CreateStoryEditorWindow(asset);
            var adapter = GetPrivateField<IEditorNodeGraphAdapter>(window, "m_GraphAdapter");
            var template = adapter.Templates.First(x => string.Equals(x.TemplateId, "story.pattern.video_wait_qte", StringComparison.Ordinal));

            adapter.CreateNode(template, new Vector2(240f, 180f), default(EditorGraphPortRef));
            InvokePrivate(window, "SetNodeFieldFromGraph", "video_wait_qte_video", MediaCommandNames.ClipArgument, SampleGraphFixture.IntroVideoPath);

            var chapter = asset.Chapters[0];
            Assert.IsNotNull(FindNode(asset, "video_wait_qte_parallel"));
            Assert.IsNotNull(FindNode(asset, "video_wait_qte_video"));
            Assert.IsNotNull(FindNode(asset, "video_wait_qte_wait"));
            Assert.IsNotNull(FindNode(asset, "video_wait_qte"));
            Assert.IsNotNull(FindNode(asset, "video_wait_qte_success"));
            Assert.IsNotNull(FindNode(asset, "video_wait_qte_fail"));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video_wait_qte_parallel", StringComparison.Ordinal) &&
                string.Equals(x.FromPortId, "branch_1", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_qte_video", StringComparison.Ordinal)));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video_wait_qte_parallel", StringComparison.Ordinal) &&
                string.Equals(x.FromPortId, "branch_2", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_qte_wait", StringComparison.Ordinal)));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video_wait_qte_wait", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_qte", StringComparison.Ordinal)));

            var program = ProgramCompiler.Compile(asset, out var report);
            var step = FindStep(program, chapter.ChapterId, "video_wait_qte");

            AssertNoErrors(report.Issues);
            Assert.AreEqual(StepKind.Command, step.Kind);
            Assert.AreEqual(InteractionCommandNames.Qte, step.Data.Command.Name);
            Assert.AreEqual("video_wait_qte_success", step.Data.Command.GetOutcomeTarget(InteractionCommandNames.SuccessOutcome).StepId);
            Assert.AreEqual("video_wait_qte_fail", step.Data.Command.GetOutcomeTarget(InteractionCommandNames.FailOutcome).StepId);
        }

        [Test]
        public void StoryEditorGraph_WhenVideoWaitUnlockPatternCreated_CompilesCommandOutcomes()
        {
            var asset = CreateAsset();
            var window = CreateStoryEditorWindow(asset);
            var adapter = GetPrivateField<IEditorNodeGraphAdapter>(window, "m_GraphAdapter");
            var template = adapter.Templates.First(x => string.Equals(x.TemplateId, "story.pattern.video_wait_unlock", StringComparison.Ordinal));

            adapter.CreateNode(template, new Vector2(240f, 180f), default(EditorGraphPortRef));
            InvokePrivate(window, "SetNodeFieldFromGraph", "video_wait_unlock_video", MediaCommandNames.ClipArgument, SampleGraphFixture.IntroVideoPath);

            var chapter = asset.Chapters[0];
            Assert.IsNotNull(FindNode(asset, "video_wait_unlock_parallel"));
            Assert.IsNotNull(FindNode(asset, "video_wait_unlock_video"));
            Assert.IsNotNull(FindNode(asset, "video_wait_unlock_wait"));
            Assert.IsNotNull(FindNode(asset, "video_wait_unlock"));
            Assert.IsNotNull(FindNode(asset, "video_wait_unlock_success"));
            Assert.IsNotNull(FindNode(asset, "video_wait_unlock_fail"));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video_wait_unlock_parallel", StringComparison.Ordinal) &&
                string.Equals(x.FromPortId, "branch_1", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_unlock_video", StringComparison.Ordinal)));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video_wait_unlock_parallel", StringComparison.Ordinal) &&
                string.Equals(x.FromPortId, "branch_2", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_unlock_wait", StringComparison.Ordinal)));
            Assert.IsTrue(chapter.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video_wait_unlock_wait", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, "video_wait_unlock", StringComparison.Ordinal)));

            var program = ProgramCompiler.Compile(asset, out var report);
            var step = FindStep(program, chapter.ChapterId, "video_wait_unlock");

            AssertNoErrors(report.Issues);
            Assert.AreEqual(StepKind.Command, step.Kind);
            Assert.AreEqual(InteractionCommandNames.Unlock, step.Data.Command.Name);
            Assert.AreEqual("video_wait_unlock_success", step.Data.Command.GetOutcomeTarget(InteractionCommandNames.SuccessOutcome).StepId);
            Assert.AreEqual("video_wait_unlock_fail", step.Data.Command.GetOutcomeTarget(InteractionCommandNames.FailOutcome).StepId);
        }

        [Test]
        public void StoryEditorGraphAdapter_WhenPortsAreInvalid_ReturnsChineseReason()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Chapters[0].Nodes.Add(CreateNode("choice_extra", "备用选项", NodeKind.Choice));
            asset.Chapters[0].Nodes.Add(CreateNode("merge", "等待全部完成", NodeKind.Merge));
            asset.Chapters[0].Nodes.Add(CreateNode("wait", "等待", NodeKind.Wait, ("duration", "1")));
            asset.Chapters[0].Nodes.Add(CreateNode("narration", "旁白", NodeKind.Narration, ("textKey", "story.narration")));
            var window = CreateStoryEditorWindow(asset);
            var adapter = GetPrivateField<IEditorNodeGraphAdapter>(window, "m_GraphAdapter");

            var videoToChoice = adapter.CanConnect(new EditorGraphPortRef("video", "completed"), new EditorGraphPortRef("choice_extra", "in"));
            var lineToChoice = adapter.CanConnect(new EditorGraphPortRef("line_intro", "completed"), new EditorGraphPortRef("choice_extra", "in"));
            var waitToChoice = adapter.CanConnect(new EditorGraphPortRef("wait", "completed"), new EditorGraphPortRef("choice_extra", "in"));
            var lineToMerge = adapter.CanConnect(new EditorGraphPortRef("line_intro", "completed"), new EditorGraphPortRef("merge", "in"));
            var narrationToMerge = adapter.CanConnect(new EditorGraphPortRef("narration", "completed"), new EditorGraphPortRef("merge", "in"));
            var choiceToEnd = adapter.CanConnect(new EditorGraphPortRef("choice", "selected"), new EditorGraphPortRef("end", "in"));
            var choiceUnknown = adapter.CanConnect(new EditorGraphPortRef("choice", "help"), new EditorGraphPortRef("mini_game", "in"));
            var endOutput = adapter.CanConnect(new EditorGraphPortRef("end", "completed"), new EditorGraphPortRef("mini_game", "in"));

            Assert.IsFalse(videoToChoice.Allowed);
            StringAssert.Contains("选项节点只能接在对白、旁白、等待或等待全部完成的完成端口后", videoToChoice.Message);
            Assert.IsTrue(lineToChoice.Allowed, lineToChoice.Message);
            Assert.IsTrue(waitToChoice.Allowed, waitToChoice.Message);
            Assert.IsTrue(lineToMerge.Allowed, lineToMerge.Message);
            Assert.IsTrue(narrationToMerge.Allowed, narrationToMerge.Message);
            Assert.IsTrue(choiceToEnd.Allowed, choiceToEnd.Message);
            Assert.IsFalse(choiceUnknown.Allowed);
            StringAssert.Contains("选项节点只能从“选择后”端口", choiceUnknown.Message);
            Assert.IsFalse(endOutput.Allowed);
            StringAssert.Contains("结束节点没有输出端口", endOutput.Message);
        }

        [Test]
        public void StoryEditorGraph_WhenLineSwitchesBetweenChoiceAndDirectMode_ReplacesCompletedEdges()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Chapters[0].Nodes.Add(CreateNode("merge", "等待全部完成", NodeKind.Merge));
            var window = CreateStoryEditorWindow(asset);
            var chapter = asset.Chapters[0];
            chapter.Edges.RemoveAll(x => x.FromNodeId == "line_intro" && x.FromPortId == "completed");
            chapter.Edges.Add(CreateEdge("line_intro", "completed", "完成", "mini_game", "edge_line_intro_direct"));

            InvokePrivate(
                window,
                "ConnectFromGraph",
                new EditorGraphPortRef("line_intro", "completed"),
                new EditorGraphPortRef("choice", "in"));
            var choiceModeEdges = chapter.Edges
                .Where(x => x.FromNodeId == "line_intro" && x.FromPortId == "completed")
                .ToList();

            Assert.AreEqual(1, choiceModeEdges.Count);
            Assert.AreEqual("choice", choiceModeEdges[0].TargetNodeId);

            InvokePrivate(
                window,
                "ConnectFromGraph",
                new EditorGraphPortRef("line_intro", "completed"),
                new EditorGraphPortRef("merge", "in"));
            var afterDirect = chapter.Edges
                .Where(x => x.FromNodeId == "line_intro" && x.FromPortId == "completed")
                .ToList();

            Assert.AreEqual(1, afterDirect.Count);
            Assert.AreEqual("merge", afterDirect[0].TargetNodeId);
        }

        [Test]
        public void StoryRuntime_WhenScanned_DoesNotReferenceEditorPlaybackOrConcreteMediaTypes()
        {
            var files = Directory.GetFiles(FrameworkFilePath("Runtime/Story"), "*.cs", SearchOption.AllDirectories);
            var source = string.Join(Environment.NewLine, files.Select(System.IO.File.ReadAllText));

            Assert.IsFalse(source.Contains("EditorNodeGraph"), "Story runtime must not reference editor graph kit.");
            Assert.IsFalse(source.Contains("UnityEditor"), "Story runtime must not reference UnityEditor.");
            Assert.IsFalse(source.Contains("AssetDatabase"), "Story runtime must not reference AssetDatabase.");
            Assert.IsFalse(source.Contains("ObjectField"), "Story runtime must not reference UI Toolkit ObjectField.");
            Assert.IsFalse(source.Contains("UIElements"), "Story runtime must not reference UI Toolkit.");
            Assert.IsFalse(source.Contains("VideoClip"), "Story runtime must not reference concrete video clip types.");
            Assert.IsFalse(source.Contains("PlaybackWindow"), "Story runtime must not reference the editor playback window.");
        }

        private AuthoringAsset CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            m_CreatedObjects.Add(asset);
            return asset;
        }

        private AuthoringAsset CreateCompilerAsset(
            string helpTargetNodeId = "video",
            string videoSource = MediaCommandNames.VideoSourceStreamingAssets,
            string videoClip = SampleGraphFixture.IntroVideoPath,
            string videoLoop = "true",
            bool includeChoiceHelpSelected = true,
            bool addExtraChoiceHelpSelected = false,
            bool choiceHelpHasTextKey = true)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            var videoParameters = new List<(string key, string value)>();
            if (videoSource != null)
            {
                videoParameters.Add((MediaCommandNames.VideoSourceArgument, videoSource));
            }

            if (videoClip != null)
            {
                videoParameters.Add(("clip", videoClip));
            }

            if (videoLoop != null)
            {
                videoParameters.Add(("loop", videoLoop));
            }

            var choiceParameters = choiceHelpHasTextKey
                ? new[] { ("textKey", "choice.help") }
                : Array.Empty<(string key, string value)>();

            var chapter = CreateChapter(
                "chapter_01",
                "第一章",
                "start",
                new[]
                {
                    CreateNode("start", "开始", NodeKind.Start),
                    CreateNode("line_intro", "开场对白", NodeKind.Dialogue, ("textKey", "story.intro.line"), ("speaker", "npc"), ("tags", "intro")),
                    CreateNode("choice_help", "救人", NodeKind.Choice, choiceParameters),
                    CreateNode("choice_leave", "离开", NodeKind.Choice, ("textKey", "choice.leave")),
                    CreateNode("video", "播放视频", NodeKind.PlayVideo, videoParameters.ToArray()),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("start", "completed", "完成", "line_intro"),
                    CreateEdge("line_intro", "completed", "完成", "choice_help", "edge_line_help", CreateCondition("can_help")),
                    CreateEdge("line_intro", "completed", "完成", "choice_leave", "edge_line_leave"),
                    CreateEdge("video", "completed", "完成", "end"),
                    CreateStoryEndEdge("choice_leave", "selected", "选择后"),
                });

            if (includeChoiceHelpSelected)
            {
                chapter.Edges.Add(CreateEdge("choice_help", "selected", "选择后", helpTargetNodeId, "edge_help_selected"));
            }

            if (addExtraChoiceHelpSelected)
            {
                chapter.Edges.Add(CreateStoryEndEdge("choice_help", "selected", "选择后", "edge_help_selected_extra"));
            }

            asset.Chapters.Add(chapter);
            return asset;
        }

        private AuthoringAsset CreateQteCompilerAsset(
            string durationSeconds = "3",
            string requiredCount = "5",
            bool includeFailOutcome = true,
            bool includeTimeoutOutcome = false)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";
            var edges = new List<AuthoringEdge>
            {
                CreateEdge("start", "completed", "完成", "qte"),
                CreateEdge("qte", InteractionCommandNames.SuccessOutcome, "成功", "qte_success"),
                CreateEdge("qte_success", "completed", "完成", "end"),
                CreateEdge("qte_fail", "completed", "完成", "end"),
            };
            if (includeFailOutcome)
            {
                edges.Add(CreateEdge("qte", InteractionCommandNames.FailOutcome, "失败", "qte_fail"));
            }

            if (includeTimeoutOutcome)
            {
                edges.Add(CreateEdge("qte", "timeout", "超时", "qte_fail"));
            }

            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "start",
                new[]
                {
                    CreateNode("start", "开始", NodeKind.Start),
                    CreateNode(
                        "qte",
                        "挣脱 QTE",
                        NodeKind.Qte,
                        (InteractionCommandNames.InputActionIdArgument, "space"),
                        (InteractionCommandNames.DurationSecondsArgument, durationSeconds),
                        (InteractionCommandNames.RequiredCountArgument, requiredCount),
                        (InteractionCommandNames.PromptTextKeyArgument, "qte.break_free")),
                    CreateNode("qte_success", "成功", NodeKind.Narration, ("textKey", "qte.success")),
                    CreateNode("qte_fail", "失败", NodeKind.Narration, ("textKey", "qte.fail")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                edges));

            return asset;
        }

        private AuthoringAsset CreateUnlockCompilerAsset(
            string unlockId = "chapter_01.door",
            string puzzleType = InteractionCommandNames.PuzzleTypeNodeUnlock,
            string promptTextKey = "unlock.door",
            bool includeFailOutcome = true,
            bool includeUnsupportedOutcome = false)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            var unlockParameters = new List<(string key, string value)>();
            if (unlockId != null)
            {
                unlockParameters.Add((InteractionCommandNames.UnlockIdArgument, unlockId));
            }

            if (puzzleType != null)
            {
                unlockParameters.Add((InteractionCommandNames.PuzzleTypeArgument, puzzleType));
            }

            if (promptTextKey != null)
            {
                unlockParameters.Add((InteractionCommandNames.PromptTextKeyArgument, promptTextKey));
            }

            var edges = new List<AuthoringEdge>
            {
                CreateEdge("start", "completed", "完成", "unlock"),
                CreateEdge("unlock", InteractionCommandNames.SuccessOutcome, "成功", "unlock_success"),
                CreateEdge("unlock_success", "completed", "完成", "end"),
                CreateEdge("unlock_fail", "completed", "完成", "end"),
            };
            if (includeFailOutcome)
            {
                edges.Add(CreateEdge("unlock", InteractionCommandNames.FailOutcome, "失败", "unlock_fail"));
            }

            if (includeUnsupportedOutcome)
            {
                edges.Add(CreateEdge("unlock", "timeout", "超时", "unlock_fail"));
            }

            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "start",
                new[]
                {
                    CreateNode("start", "开始", NodeKind.Start),
                    CreateNode("unlock", "门锁", NodeKind.Unlock, unlockParameters.ToArray()),
                    CreateNode("unlock_success", "已解锁", NodeKind.Narration, ("textKey", "unlock.success")),
                    CreateNode("unlock_fail", "未解锁", NodeKind.Narration, ("textKey", "unlock.fail")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                edges));

            return asset;
        }

        private AuthoringAsset CreateVideoQteTransitionAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";
            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "start",
                new[]
                {
                    CreateNode("start", "开始", NodeKind.Start),
                    CreateNode(
                        "video",
                        "互动视频",
                        NodeKind.PlayVideo,
                        (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                        (MediaCommandNames.ClipArgument, SampleGraphFixture.IntroVideoPath),
                        ("wait", "true"),
                        ("loop", "false")),
                    CreateNode(
                        "qte",
                        "挣脱 QTE",
                        NodeKind.Qte,
                        (InteractionCommandNames.InputActionIdArgument, "space"),
                        (InteractionCommandNames.DurationSecondsArgument, "3"),
                        (InteractionCommandNames.RequiredCountArgument, "5"),
                        (InteractionCommandNames.PromptTextKeyArgument, "qte.break_free")),
                    CreateNode("qte_success", "成功", NodeKind.Narration, ("textKey", "qte.success")),
                    CreateNode("qte_fail", "失败", NodeKind.Narration, ("textKey", "qte.fail")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("start", "completed", "完成", "video"),
                    CreateEdge("video", MediaCommandNames.CompletedOutcome, "完成", "qte"),
                    CreateEdge("qte", InteractionCommandNames.SuccessOutcome, "成功", "qte_success"),
                    CreateEdge("qte", InteractionCommandNames.FailOutcome, "失败", "qte_fail"),
                    CreateEdge("qte_success", "completed", "完成", "end"),
                    CreateEdge("qte_fail", "completed", "完成", "end"),
                }));

            return asset;
        }

        private AuthoringAsset CreateVideoUnlockTransitionAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";
            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "start",
                new[]
                {
                    CreateNode("start", "开始", NodeKind.Start),
                    CreateNode(
                        "video",
                        "互动视频",
                        NodeKind.PlayVideo,
                        (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                        (MediaCommandNames.ClipArgument, SampleGraphFixture.IntroVideoPath),
                        ("wait", "true"),
                        ("loop", "false")),
                    CreateNode(
                        "unlock",
                        "门锁",
                        NodeKind.Unlock,
                        (InteractionCommandNames.UnlockIdArgument, "chapter_01.door"),
                        (InteractionCommandNames.PuzzleTypeArgument, InteractionCommandNames.PuzzleTypeNodeUnlock),
                        (InteractionCommandNames.PromptTextKeyArgument, "unlock.door")),
                    CreateNode("unlock_success", "成功", NodeKind.Narration, ("textKey", "unlock.success")),
                    CreateNode("unlock_fail", "失败", NodeKind.Narration, ("textKey", "unlock.fail")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("start", "completed", "完成", "video"),
                    CreateEdge("video", MediaCommandNames.CompletedOutcome, "完成", "unlock"),
                    CreateEdge("unlock", InteractionCommandNames.SuccessOutcome, "成功", "unlock_success"),
                    CreateEdge("unlock", InteractionCommandNames.FailOutcome, "失败", "unlock_fail"),
                    CreateEdge("unlock_success", "completed", "完成", "end"),
                    CreateEdge("unlock_fail", "completed", "完成", "end"),
                }));

            return asset;
        }

        private AuthoringAsset CreateParallelCompilerAsset(
            bool missingChoiceMerge = false,
            bool nestedParallel = false,
            bool mixedMergeOwners = false,
            bool choiceInsideParallel = false)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            var nodes = new List<AuthoringNode>
            {
                CreateNode("start", "开始", NodeKind.Start),
                CreateNode("parallel", "并行", NodeKind.Parallel),
                CreateNode(
                    "video",
                    "播放视频",
                    NodeKind.PlayVideo,
                    (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                    ("clip", SampleGraphFixture.IntroVideoPath),
                    ("wait", "true")),
                CreateNode("narration", "旁白", NodeKind.Narration, ("textKey", "story.parallel.narration")),
                CreateNode("choice", "继续", NodeKind.Choice, ("textKey", "choice.continue")),
                CreateNode("merge", "等待全部完成", NodeKind.Merge),
                CreateNode("after_merge", "等待后", NodeKind.Narration, ("textKey", "story.after.merge")),
                CreateNode("end", "结束", NodeKind.End),
            };

            var edges = new List<AuthoringEdge>
            {
                CreateEdge("start", "completed", "完成", "parallel"),
                CreateEdge("parallel", "branch_video", "视频轨", "video"),
                CreateEdge("parallel", "branch_dialogue", "对白轨", nestedParallel ? "nested_parallel" : "narration"),
                CreateEdge("video", "completed", "完成", "merge"),
                CreateEdge("after_merge", "completed", "完成", "end"),
            };

            if (missingChoiceMerge is false)
            {
                edges.Add(CreateEdge("narration", "completed", "完成", choiceInsideParallel ? "choice" : "merge"));
                edges.Add(CreateEdge("choice", "selected", "选择后", "after_merge"));
                if (choiceInsideParallel is false)
                {
                    edges.Add(CreateEdge("merge", "completed", "进入选择", "choice"));
                }
            }
            else
            {
                edges.Add(CreateStoryEndEdge("choice", "selected", "选择后"));
            }

            if (nestedParallel)
            {
                nodes.Add(CreateNode("nested_parallel", "嵌套并行", NodeKind.Parallel));
                nodes.Add(CreateNode("nested_line", "嵌套旁白", NodeKind.Narration, ("textKey", "nested.line")));
                nodes.Add(CreateNode("nested_line_b", "嵌套旁白 B", NodeKind.Narration, ("textKey", "nested.line.b")));
                nodes.Add(CreateNode("nested_merge", "嵌套等待全部完成", NodeKind.Merge));
                edges.Add(CreateEdge("nested_parallel", "branch_a", "分支 A", "nested_line"));
                edges.Add(CreateEdge("nested_parallel", "branch_b", "分支 B", "nested_line_b"));
                edges.Add(CreateEdge("nested_line", "completed", "完成", "nested_merge"));
                edges.Add(CreateEdge("nested_line_b", "completed", "完成", "nested_merge"));
                edges.Add(CreateEdge("nested_merge", "completed", "完成", "end"));
            }

            if (mixedMergeOwners)
            {
                nodes.Add(CreateNode("parallel_extra", "第二个并行", NodeKind.Parallel));
                nodes.Add(CreateNode("extra_a", "额外 A", NodeKind.Narration, ("textKey", "extra.a")));
                nodes.Add(CreateNode("extra_b", "额外 B", NodeKind.Narration, ("textKey", "extra.b")));
                edges.Add(CreateEdge("parallel_extra", "branch_a", "分支 A", "extra_a"));
                edges.Add(CreateEdge("parallel_extra", "branch_b", "分支 B", "extra_b"));
                edges.Add(CreateEdge("extra_a", "completed", "完成", "merge"));
                edges.Add(CreateEdge("extra_b", "completed", "完成", "merge"));
            }

            asset.Chapters.Add(CreateChapter("chapter_01", "第一章", "start", nodes, edges));
            return asset;
        }

        private AuthoringAsset CreateParallelWaitChoiceAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_parallel_wait_choice";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "start",
                new[]
                {
                    CreateNode("start", "开始", NodeKind.Start),
                    CreateNode("parallel", "并行", NodeKind.Parallel),
                    CreateNode(
                        "video",
                        "播放视频",
                        NodeKind.PlayVideo,
                        (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                        (MediaCommandNames.ClipArgument, SampleGraphFixture.IntroVideoPath),
                        ("wait", "true")),
                    CreateNode("wait_choice", "等待选项", NodeKind.Wait, ("duration", "1.5")),
                    CreateNode("choice", "继续", NodeKind.Choice, ("textKey", "choice.continue")),
                    CreateNode("after_choice", "选择后", NodeKind.Narration, ("textKey", "story.after.choice")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("start", "completed", "完成", "parallel"),
                    CreateEdge("parallel", "branch_video", "视频轨", "video"),
                    CreateEdge("parallel", "branch_interaction", "交互轨", "wait_choice"),
                    CreateEdge("video", "completed", "完成", "end"),
                    CreateEdge("wait_choice", "completed", "完成", "choice"),
                    CreateEdge("choice", "selected", "选择后", "after_choice"),
                    CreateEdge("after_choice", "completed", "完成", "end"),
                }));

            return asset;
        }

        private AuthoringAsset CreateWaitChoiceCompilerAsset(bool mixDirectTarget = false)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_wait_choice";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            var edges = new List<AuthoringEdge>
            {
                CreateEdge("start", "completed", "完成", "wait_choice"),
                CreateEdge("wait_choice", "completed", "选项 A", "choice_a"),
                CreateEdge("wait_choice", "completed", "选项 B", "choice_b"),
                CreateEdge("choice_a", "selected", "选择后", "after_a"),
                CreateEdge("choice_b", "selected", "选择后", "after_b"),
                CreateEdge("after_a", "completed", "完成", "end"),
                CreateEdge("after_b", "completed", "完成", "end"),
            };

            if (mixDirectTarget)
            {
                edges.Add(CreateEdge("wait_choice", "completed", "完成", "end", "edge_wait_direct"));
            }

            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "start",
                new[]
                {
                    CreateNode("start", "开始", NodeKind.Start),
                    CreateNode("wait_choice", "等待选项", NodeKind.Wait, ("duration", "1.5")),
                    CreateNode("choice_a", "选项 A", NodeKind.Choice, ("textKey", "choice.a")),
                    CreateNode("choice_b", "选项 B", NodeKind.Choice, ("textKey", "choice.b")),
                    CreateNode("after_a", "选项 A 后", NodeKind.Narration, ("textKey", "after.a")),
                    CreateNode("after_b", "选项 B 后", NodeKind.Narration, ("textKey", "after.b")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                edges));

            return asset;
        }

        private AuthoringAsset CreateTransitionVideoAsset(bool videoTargetChoice = false, bool videoLoop = false)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            var nodes = new List<AuthoringNode>
            {
                CreateNode("start", "开始", NodeKind.Start),
                CreateNode(
                    "video",
                    "过渡视频",
                    NodeKind.PlayVideo,
                    (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                    (MediaCommandNames.ClipArgument, SampleGraphFixture.IntroVideoPath),
                    ("wait", "true"),
                    ("loop", videoLoop ? "true" : "false")),
                CreateNode("line", "过渡后旁白", NodeKind.Narration, ("textKey", "story.after.video")),
                CreateNode("choice", "视频后选择", NodeKind.Choice, ("textKey", "choice.after.video")),
                CreateNode("end", "结束", NodeKind.End),
            };

            var edges = new List<AuthoringEdge>
            {
                CreateEdge("start", "completed", "完成", "video"),
                CreateEdge("video", "completed", "完成", videoTargetChoice ? "choice" : "line"),
                CreateEdge("line", "completed", "完成", "end"),
                CreateStoryEndEdge("choice", "selected", "选择后"),
            };

            asset.Chapters.Add(CreateChapter("chapter_01", "第一章", "start", nodes, edges));
            return asset;
        }

        private AuthoringAsset CreatePreParallelVideoAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_pre_parallel_video";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            var nodes = new[]
            {
                CreateNode("start", "开始", NodeKind.Start),
                CreateNode(
                    "intro_video",
                    "过渡视频",
                    NodeKind.PlayVideo,
                    (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                    (MediaCommandNames.ClipArgument, SampleGraphFixture.IntroVideoPath),
                    ("wait", "true"),
                    ("loop", "false")),
                CreateNode("parallel", "并行", NodeKind.Parallel),
                CreateNode(
                    "branch_video",
                    "分支视频",
                    NodeKind.PlayVideo,
                    (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                    (MediaCommandNames.ClipArgument, SampleGraphFixture.IntroVideoPath),
                    ("wait", "true"),
                    ("loop", "false")),
                CreateNode("line", "对白", NodeKind.Dialogue, ("textKey", "story.choice.line"), ("speaker", "NPC")),
                CreateNode("choice", "选择", NodeKind.Choice, ("textKey", "choice.continue")),
                CreateNode("after_choice", "选择后", NodeKind.Narration, ("textKey", "story.after.choice")),
                CreateNode("end", "结束", NodeKind.End),
            };

            var edges = new[]
            {
                CreateEdge("start", "completed", "完成", "intro_video"),
                CreateEdge("intro_video", "completed", "完成", "parallel"),
                CreateEdge("parallel", "branch_video", "视频轨", "branch_video"),
                CreateEdge("parallel", "branch_dialogue", "对白轨", "line"),
                CreateEdge("branch_video", "completed", "完成", "end"),
                CreateEdge("line", "completed", "完成", "choice"),
                CreateEdge("choice", "selected", "选择后", "after_choice"),
                CreateEdge("after_choice", "completed", "完成", "end"),
            };

            asset.Chapters.Add(CreateChapter("chapter_01", "第一章", "start", nodes, edges));
            return asset;
        }

        private AuthoringAsset CreateParallelChoiceTargetAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "parallel",
                new[]
                {
                    CreateNode("parallel", "并行", NodeKind.Parallel),
                    CreateNode(
                        "video",
                        "播放视频",
                        NodeKind.PlayVideo,
                        (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                        ("clip", SampleGraphFixture.IntroVideoPath),
                        ("wait", "true")),
                    CreateNode("line", "对白", NodeKind.Dialogue, ("textKey", "story.choice.line"), ("speaker", "NPC")),
                    CreateNode("choice_a", "选择 A", NodeKind.Choice, ("textKey", "choice.a")),
                    CreateNode("choice_b", "选择 B", NodeKind.Choice, ("textKey", "choice.b")),
                    CreateNode("selected_audio", "选择后音频", NodeKind.PlayAudio, ("clip", SampleGraphFixture.StationAudioPath)),
                    CreateNode("unselected_image", "未选择图片", NodeKind.ShowImage, ("image", SampleGraphFixture.MapImagePath)),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("parallel", "branch_video", "视频轨", "video"),
                    CreateEdge("parallel", "branch_dialogue", "对白轨", "line"),
                    CreateEdge("line", "completed", "完成", "choice_a"),
                    CreateEdge("line", "completed", "完成", "choice_b"),
                    CreateEdge("choice_a", "selected", "选择后", "selected_audio"),
                    CreateEdge("choice_b", "selected", "选择后", "unselected_image"),
                }));
            return asset;
        }

        private AuthoringAsset CreateParallelChoiceTargetParallelAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "parallel",
                new[]
                {
                    CreateNode("parallel", "并行", NodeKind.Parallel),
                    CreateNode(
                        "intro_video",
                        "播放视频",
                        NodeKind.PlayVideo,
                        (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                        ("clip", SampleGraphFixture.IntroVideoPath),
                        ("wait", "true")),
                    CreateNode("line", "对白", NodeKind.Dialogue, ("textKey", "story.choice.line"), ("speaker", "NPC")),
                    CreateNode("choice_a", "选择 A", NodeKind.Choice, ("textKey", "choice.a")),
                    CreateNode("choice_b", "选择 B", NodeKind.Choice, ("textKey", "choice.b")),
                    CreateNode("after_choice_parallel", "选择后并行", NodeKind.Parallel),
                    CreateNode("after_audio", "选择后音频", NodeKind.PlayAudio, ("clip", SampleGraphFixture.StationAudioPath)),
                    CreateNode(
                        "after_video",
                        "选择后视频",
                        NodeKind.PlayVideo,
                        (MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets),
                        ("clip", SampleGraphFixture.IntroVideoPath),
                        ("wait", "true")),
                    CreateNode("after_merge", "选择后等待全部完成", NodeKind.Merge),
                    CreateNode("after_line", "选择后对白", NodeKind.Dialogue, ("textKey", "after.choice.line")),
                    CreateNode("unselected_image", "未选择图片", NodeKind.ShowImage, ("image", SampleGraphFixture.MapImagePath)),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("parallel", "branch_video", "视频轨", "intro_video"),
                    CreateEdge("parallel", "branch_dialogue", "对白轨", "line"),
                    CreateEdge("line", "completed", "完成", "choice_a"),
                    CreateEdge("line", "completed", "完成", "choice_b"),
                    CreateEdge("choice_a", "selected", "选择后", "after_choice_parallel"),
                    CreateEdge("choice_b", "selected", "选择后", "unselected_image"),
                    CreateEdge("after_choice_parallel", "branch_audio", "音频轨", "after_audio"),
                    CreateEdge("after_choice_parallel", "branch_video", "视频轨", "after_video"),
                    CreateEdge("after_audio", "completed", "完成", "after_merge"),
                    CreateEdge("after_video", "completed", "完成", "after_merge"),
                    CreateEdge("after_merge", "completed", "完成", "after_line"),
                    CreateEdge("after_line", "completed", "完成", "end"),
                }));
            return asset;
        }

        private AuthoringAsset CreateThreeBranchParallelAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            asset.Chapters.Add(CreateChapter(
                "chapter_01",
                "第一章",
                "parallel",
                new[]
                {
                    CreateNode("parallel", "并行", NodeKind.Parallel),
                    CreateNode("image", "显示图片", NodeKind.ShowImage, ("image", SampleGraphFixture.MapImagePath)),
                    CreateNode("audio", "播放音频", NodeKind.PlayAudio, ("clip", SampleGraphFixture.StationAudioPath)),
                    CreateNode("line", "旁白", NodeKind.Narration, ("textKey", "三轨旁白")),
                    CreateNode("choice", "继续", NodeKind.Choice, ("textKey", "继续")),
                    CreateNode("merge", "等待全部完成", NodeKind.Merge),
                    CreateNode("after_merge", "之后", NodeKind.Narration, ("textKey", "等待之后")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("parallel", "branch_image", "图片轨", "image"),
                    CreateEdge("parallel", "branch_audio", "音频轨", "audio"),
                    CreateEdge("parallel", "branch_text", "文本轨", "line"),
                    CreateEdge("image", "completed", "完成", "merge"),
                    CreateEdge("audio", "completed", "完成", "merge"),
                    CreateEdge("line", "completed", "完成", "merge"),
                    CreateEdge("merge", "completed", "进入选择", "choice"),
                    CreateEdge("choice", "selected", "选择后", "after_merge"),
                    CreateEdge("after_merge", "completed", "完成", "end"),
                }));
            return asset;
        }

        private AuthoringAsset CreateSemanticGraphAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            var chapter = CreateChapter(
                "chapter_01",
                "第一章",
                "start",
                new[]
                {
                    CreateNode("start", "开始", NodeKind.Start),
                    CreateNode("video", "播放开场视频", NodeKind.PlayVideo),
                    CreateNode("line_intro", "开场对白", NodeKind.Dialogue, ("textKey", "story.intro.line")),
                    CreateNode("choice", "救人", NodeKind.Choice, ("textKey", "choice.help")),
                    CreateNode("mini_game", "小游戏：撬锁", NodeKind.MiniGame, ("miniGameId", "lockpick")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("start", "completed", "完成", "video", "edge_start_completed"),
                    CreateEdge("video", "completed", "完成", "line_intro", "edge_video_completed"),
                    CreateEdge("line_intro", "completed", "完成", "choice", "edge_line_intro_completed"),
                    CreateEdge("choice", "selected", "选择后", "mini_game", "edge_choice_selected"),
                    CreateStoryEndEdge("mini_game", "success", "成功", "edge_mini_success"),
                });

            var target = CreateChapter(
                "chapter_02",
                "第二章",
                "target",
                new[]
                {
                    CreateNode("target", "结束", NodeKind.End),
                },
                Array.Empty<AuthoringEdge>());

            asset.Chapters.Add(chapter);
            asset.Chapters.Add(target);
            AddLayout(asset, "chapter_01", "video", 180f, 120f);
            return asset;
        }

        private static AuthoringChapter CreateChapter(
            string chapterId,
            string title,
            string entryNodeId,
            IReadOnlyList<AuthoringNode> nodes,
            IReadOnlyList<AuthoringEdge> edges)
        {
            var chapter = new AuthoringChapter
            {
                ChapterId = chapterId,
                Title = title,
                EntryNodeId = entryNodeId
            };
            chapter.Nodes.AddRange(nodes);
            chapter.Edges.AddRange(edges);
            return chapter;
        }

        private static AuthoringNode CreateNode(string nodeId, string title, NodeKind kind, params (string key, string value)[] parameters)
        {
            var node = new AuthoringNode
            {
                NodeId = nodeId,
                Title = title,
                NodeKind = kind
            };
            for (var i = 0; i < parameters.Length; i++)
            {
                node.Parameters.Add(new AuthoringParameter
                {
                    Key = parameters[i].key,
                    Value = parameters[i].value
                });
            }

            return node;
        }

        private static AuthoringEdge CreateEdge(
            string fromNodeId,
            string fromPortId,
            string fromPortLabel,
            string targetNodeId,
            string edgeId = null,
            params AuthoringCondition[] conditions)
        {
            var edge = new AuthoringEdge
            {
                EdgeId = edgeId ?? $"edge_{fromNodeId}_{fromPortId}_{targetNodeId}",
                FromNodeId = fromNodeId,
                FromPortId = fromPortId,
                FromPortLabel = fromPortLabel,
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = targetNodeId
            };
            edge.Conditions.AddRange(conditions);
            return edge;
        }

        private static AuthoringEdge CreateStoryEndEdge(
            string fromNodeId,
            string fromPortId,
            string fromPortLabel,
            string edgeId = null)
        {
            return new AuthoringEdge
            {
                EdgeId = edgeId ?? $"edge_{fromNodeId}_{fromPortId}_end",
                FromNodeId = fromNodeId,
                FromPortId = fromPortId,
                FromPortLabel = fromPortLabel,
                TargetKind = TransitionTargetKind.StoryEnd
            };
        }

        private static AuthoringCondition CreateCondition(string conditionId)
        {
            return new AuthoringCondition { ConditionId = conditionId };
        }

        private static AuthoringNode FindNode(AuthoringAsset asset, string nodeId)
        {
            return asset.Chapters
                .SelectMany(x => x.Nodes)
                .First(x => string.Equals(x.NodeId, nodeId, StringComparison.Ordinal));
        }

        private static void AddOrSetParameter(AuthoringNode node, string key, string value)
        {
            var parameter = node.Parameters.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
            if (parameter == null)
            {
                node.Parameters.Add(new AuthoringParameter { Key = key, Value = value });
            }
            else
            {
                parameter.Value = value;
            }
        }

        private static void AddLayout(AuthoringAsset asset, string graphId, string nodeId, float x, float y)
        {
            asset.Layout.Nodes.Add(new NodeLayout
            {
                GraphId = graphId,
                NodeId = nodeId,
                Position = new Vector2(x, y)
            });
        }

        private EditorWindow CreateStoryEditorWindow(AuthoringAsset asset)
        {
            var window = ScriptableObject.CreateInstance<MainWindow>();
            m_CreatedObjects.Add(window);
            asset.EnsureDefaults();
            SetPrivateField(window, "m_Asset", asset);
            InvokePrivate(window, "SelectDefaults");
            InvokePrivate(window, "BuildLayout");
            InvokePrivate(window, "RefreshAll", "Ready.");
            return window;
        }

        private static Step FindStep(Program program, string chapterId, string stepId)
        {
            var chapter = program.Chapters.First(x => string.Equals(x.ChapterId, chapterId, StringComparison.Ordinal));
            return chapter.Steps.First(x => string.Equals(x.StepId, stepId, StringComparison.Ordinal));
        }

        private static VisualElement FindStoryEditorNodeView(EditorWindow window, string nodeId)
        {
            var node = window.rootVisualElement
                .Query<VisualElement>(className: "editor-node-graph-node")
                .ToList()
                .FirstOrDefault(x => string.Equals(x.userData as string, nodeId, StringComparison.Ordinal));
            Assert.IsNotNull(node, nodeId);
            return node;
        }

        private static IReadOnlyList<VisualElement> GetIssueRows(EditorWindow window)
        {
            return window.rootVisualElement.Query<VisualElement>(className: "story-editor__issue").ToList();
        }

        private static string GetIssueSummaryText(EditorWindow window)
        {
            return string.Join("|", GetIssueRows(window).Select(GetVisualText));
        }

        private static string GetVisualText(VisualElement element)
        {
            return string.Join("|", FindVisualChildren<Label>(element).Select(x => x.text));
        }

        private static IReadOnlyList<DiagnosticSnapshot> GetGraphDiagnosticItems(EditorWindow window)
        {
            var diagnostics = GetPrivateField<object>(window, "m_GraphDiagnostics");
            var items = (System.Collections.IEnumerable)diagnostics.GetType().GetProperty("Items").GetValue(diagnostics);
            return items.Cast<object>().Select(DiagnosticSnapshot.From).ToList();
        }

        private sealed class DiagnosticSnapshot
        {
            public object RawItem { get; private set; }

            public EditorGraphDiagnostic GraphDiagnostic { get; private set; }

            public string Source { get; private set; }

            public string Tooltip { get; private set; }

            public static DiagnosticSnapshot From(object rawItem)
            {
                return new DiagnosticSnapshot
                {
                    RawItem = rawItem,
                    GraphDiagnostic = (EditorGraphDiagnostic)rawItem.GetType().GetProperty("GraphDiagnostic").GetValue(rawItem),
                    Source = (string)rawItem.GetType().GetProperty("Source").GetValue(rawItem),
                    Tooltip = (string)rawItem.GetType().GetProperty("Tooltip").GetValue(rawItem)
                };
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(VisualElement root) where T : VisualElement
        {
            if (root == null)
            {
                yield break;
            }

            if (root is T typed)
            {
                yield return typed;
            }

            foreach (var child in root.Children())
            {
                foreach (var nested in FindVisualChildren<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(instance, value);
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return (T)field.GetValue(instance);
        }

        private static void InvokePrivate(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            method.Invoke(instance, args);
        }

        private static T InvokePrivate<T>(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            return (T)method.Invoke(instance, args);
        }

        private static void AssertNoErrors(IEnumerable<ValidationIssue> issues)
        {
            Assert.IsFalse(issues.Any(x => x.Severity == ValidationSeverity.Error), FormatIssues(issues));
        }

        private static string FormatIssues(IEnumerable<ValidationIssue> issues)
        {
            return string.Join(Environment.NewLine, issues.Select(x => x.ToString()));
        }

        private static string FrameworkFilePath(string relativePath)
        {
            var normalizedRelativePath = NormalizePath(relativePath).Trim('/');
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AuthoringAsset).Assembly);
            if (string.IsNullOrWhiteSpace(packageInfo?.resolvedPath) is false)
            {
                var packageFilePath = Path.Combine(packageInfo.resolvedPath, normalizedRelativePath);
                if (System.IO.File.Exists(packageFilePath) || Directory.Exists(packageFilePath))
                {
                    return NormalizePath(packageFilePath);
                }
            }

            var assetsFilePath = Path.Combine("Assets/GameDeveloperKit", normalizedRelativePath);
            if (System.IO.File.Exists(assetsFilePath) || Directory.Exists(assetsFilePath))
            {
                return NormalizePath(assetsFilePath);
            }

            return NormalizePath(Path.Combine("Packages/com.gamedeveloperkit.framework", normalizedRelativePath));
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            EnsureFolder(parent);
            if (string.IsNullOrWhiteSpace(parent) is false &&
                string.IsNullOrWhiteSpace(name) is false &&
                AssetDatabase.IsValidFolder(folder) is false)
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}

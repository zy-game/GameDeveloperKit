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
using UnityEngine.UIElements;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryEditorTests
    {
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

            var program = StoryProgramCompiler.Compile(asset, out var report);
            var schema = program.CommandSchema.Definitions.First(x => x.Name == "play_video");
            var clipArgument = schema.ArgumentDefinitions.First(x => x.Key == "clip");
            var loopArgument = schema.ArgumentDefinitions.First(x => x.Key == "loop");

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);
            Assert.AreEqual("compiler_story", program.StoryId);
            Assert.AreEqual("chapter_01", program.EntryChapterId);
            Assert.AreEqual(1, program.Chapters.Count);
            Assert.AreEqual("start", program.Chapters[0].EntryStepId);
            Assert.AreEqual(ParameterValueType.AssetReference, clipArgument.ValueType);
            Assert.IsTrue(clipArgument.Required);
            Assert.AreEqual("video", clipArgument.ResourceType);
            Assert.AreEqual(ParameterValueType.Boolean, loopArgument.ValueType);
            Assert.IsFalse(loopArgument.Required);
            CollectionAssert.Contains(schema.ArgumentNames.ToList(), "clip");
            CollectionAssert.Contains(schema.ArgumentNames.ToList(), "loop");
            CollectionAssert.DoesNotContain(schema.ArgumentNames.ToList(), "wait");

            var line = FindStep(program, "chapter_01", "line_intro");
            Assert.AreEqual(StoryStepKind.Line, line.Kind);
            Assert.AreEqual("story.intro.line", line.Data.TextKey);
            Assert.AreEqual("npc", line.Data.Speaker);
            CollectionAssert.Contains(line.Tags, "intro");

            var choice = FindStep(program, "chapter_01", "line_intro_choices");
            Assert.AreEqual(StoryStepKind.Choice, choice.Kind);
            Assert.AreEqual(2, choice.Choices.Count);
            Assert.AreEqual("choice_help", choice.Choices[0].ChoiceId);
            Assert.AreEqual("choice.help", choice.Choices[0].TextKey);
            Assert.AreEqual(StoryExpressionKind.Function, choice.Choices[0].Condition.Kind);
            Assert.AreEqual("can_help", choice.Choices[0].Condition.FunctionName);
            Assert.AreEqual("video", choice.Choices[0].Target.StepId);

            var command = FindStep(program, "chapter_01", "video");
            Assert.AreEqual(StoryStepKind.Command, command.Kind);
            Assert.AreEqual("play_video", command.Data.Command.Name);
            Assert.AreEqual(StorySampleGraphFixture.IntroVideoPath, command.Data.Command.Arguments.GetString("clip"));
            Assert.IsTrue(command.Data.Command.Arguments.GetBoolean("loop"));
            Assert.IsTrue(command.Data.Command.WaitForCompletion);
            Assert.AreEqual("end", command.Data.Command.GetOutcomeTarget("completed").StepId);
        }

        [Test]
        public void NodeSchemaRegistry_WhenMediaNodesQueried_ExposeLoopParameter()
        {
            var video = NodeSchemaRegistry.Get(NodeKind.PlayVideo);
            var audio = NodeSchemaRegistry.Get(NodeKind.PlayAudio);

            Assert.AreEqual(ParameterValueType.Boolean, video.Parameters.First(x => x.Key == "loop").ValueType);
            Assert.AreEqual(ParameterValueType.Boolean, audio.Parameters.First(x => x.Key == "loop").ValueType);
        }

        [Test]
        public void ProgramCompiler_WhenCommandAssetReferenceIsManualString_CompilesWithWarning()
        {
            var asset = CreateCompilerAsset(videoClip: "intro.mp4");

            var program = StoryProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:video/field:clip", issues);
            StringAssert.Contains("Asset reference uses a manual string fallback.", issues);
            Assert.AreEqual("intro.mp4", FindStep(program, "chapter_01", "video").Data.Command.Arguments.GetString("clip"));
        }

        [Test]
        public void ProgramCompiler_WhenCommandRequiredFieldMissing_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoClip: null);

            var program = StoryProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:video/field:clip", issues);
            StringAssert.Contains("Required command field is missing.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenCommandBooleanFieldIsInvalid_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoWait: "maybe");

            var program = StoryProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:video/field:wait", issues);
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

            var program = StoryProgramCompiler.Compile(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/chapter:chapter_01/node:wait/field:duration", issues);
            StringAssert.Contains("Wait duration must be a number.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenChoiceItemMissingSelectedTarget_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(includeChoiceHelpSelected: false);

            var program = StoryProgramCompiler.Compile(asset, out var report);
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

            var program = StoryProgramCompiler.Compile(asset, out var report);
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

            var program = StoryProgramCompiler.Compile(asset, out var report);
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

            var program = StoryProgramCompiler.Compile(asset, out var report);
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

            var program = StoryProgramCompiler.Compile(asset, out var report);
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

            var program = StoryProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var parallel = FindStep(program, "chapter_01", "parallel");
            var video = FindStep(program, "chapter_01", "video");
            var narration = FindStep(program, "chapter_01", "narration");
            var choice = FindStep(program, "chapter_01", "merge_choices");
            var merge = FindStep(program, "chapter_01", "merge");

            Assert.AreEqual(StoryStepKind.Parallel, parallel.Kind);
            Assert.AreEqual(2, parallel.Data.Branches.Count);
            Assert.AreEqual("branch_video", parallel.Data.Branches[0].BranchId);
            Assert.AreEqual("video", parallel.Data.Branches[0].Entry.StepId);
            Assert.AreEqual("branch_dialogue", parallel.Data.Branches[1].BranchId);
            Assert.AreEqual("after_merge", choice.Choices[0].Target.StepId);
            Assert.AreEqual("merge", video.Data.Target.StepId);
            Assert.AreEqual("merge", narration.Data.Target.StepId);
            Assert.AreEqual(StoryStepKind.Merge, merge.Kind);
            Assert.AreEqual("parallel", merge.Data.ParallelStepId);
            Assert.AreEqual("merge_choices", merge.Data.Target.StepId);
        }

        [Test]
        public void ProgramCompiler_WhenParallelProgramRuns_ProducesCombinedFrameAndMerges()
        {
            var asset = CreateParallelCompilerAsset();
            var program = StoryProgramCompiler.Compile(asset, out var report);
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
            var program = StoryProgramCompiler.Compile(asset, out var report);
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

            var program = StoryProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var parallel = FindStep(program, "chapter_01", "parallel");
            var narration = FindStep(program, "chapter_01", "narration");

            Assert.AreEqual(StoryStepKind.Parallel, parallel.Kind);
            Assert.AreEqual(2, parallel.Data.Branches.Count);
            Assert.AreEqual(StoryTargetKind.StoryEnd, narration.Data.Target.TargetKind);
        }

        [Test]
        public void ProgramCompiler_WhenParallelTracksEndNaturally_CompletesAfterAllTracks()
        {
            var asset = CreateParallelCompilerAsset(missingChoiceMerge: true);
            var program = StoryProgramCompiler.Compile(asset, out var report);
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
        public void ProgramCompiler_WhenParallelTrackTargetsAnotherParallel_CompilesAndTransitions()
        {
            var asset = CreateParallelCompilerAsset(nestedParallel: true);

            var program = StoryProgramCompiler.Compile(asset, out var report);
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
            Assert.AreEqual("nested_parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.IsTrue(frame.Tracks.Any(x => x.Step.StepId == "nested_line"));
            Assert.IsTrue(frame.Tracks.Any(x => x.Step.StepId == "merge"));
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
                    CreateNode("video", "播放视频", NodeKind.PlayVideo, ("clip", StorySampleGraphFixture.IntroVideoPath), ("wait", "true")),
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

            var program = StoryProgramCompiler.Compile(asset, out var report);
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

            var program = StoryProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var narration = FindStep(program, "chapter_01", "narration");
            var syntheticChoice = FindStep(program, "chapter_01", "narration_choices");
            Assert.AreEqual("narration_choices", narration.Data.Target.StepId);
            Assert.AreEqual(StoryStepKind.Choice, syntheticChoice.Kind);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.AreEqual(1, frame.Choices.Count);
            Assert.IsTrue(frame.WaitsForChoice);

            frame = runner.Select("choice");
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.AreEqual("video", frame.Tracks[0].Step.StepId);

            frame = runner.CompleteCommand("video", "completed");
            Assert.IsTrue(frame.IsCompleted);
            Assert.IsTrue(runner.Completed);
        }

        [Test]
        public void ProgramCompiler_WhenParallelChoiceSelected_DoesNotPlayUnselectedTarget()
        {
            var asset = CreateParallelChoiceTargetAsset();
            var program = StoryProgramCompiler.Compile(asset, out var report);
            AssertNoErrors(report.Issues);

            var selectedAudio = FindStep(program, "chapter_01", "selected_audio");
            var unselectedImage = FindStep(program, "chapter_01", "unselected_image");
            Assert.AreEqual(StoryTargetKind.StoryEnd, selectedAudio.Data.Target.TargetKind);
            Assert.AreEqual(StoryTargetKind.StoryEnd, unselectedImage.Data.Target.TargetKind);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.AreEqual(2, frame.Choices.Count);

            frame = runner.Select("choice_a");
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.IsTrue(frame.Tracks.Any(x => x.Step.StepId == "selected_audio"));
            Assert.IsFalse(frame.Tracks.Any(x => x.Step.StepId == "unselected_image"));

            frame = runner.Continue();
            Assert.IsFalse(frame.Tracks.Any(x => x.Step.StepId == "unselected_image"));
        }

        [Test]
        public void ProgramCompiler_WhenParallelChoiceSelected_DoesNotWaitForOtherTracks()
        {
            var asset = CreateParallelChoiceTargetAsset();
            var program = StoryProgramCompiler.Compile(asset, out var report);
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
            var program = StoryProgramCompiler.Compile(asset, out var report);
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

            var program = StoryProgramCompiler.Compile(asset, out var report);
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

            var chapter = new StoryAuthoringChapter
            {
                ChapterId = "chapter_01",
                Title = "第一章",
                EntryNodeId = "wrong_entry"
            };
            chapter.Nodes.Add(new StoryAuthoringNode { NodeId = "line", Title = "对白", NodeKind = NodeKind.Dialogue });
            chapter.Nodes.Add(new StoryAuthoringNode { NodeId = "start_a", Title = "开始 A", NodeKind = NodeKind.Start });
            chapter.Nodes.Add(new StoryAuthoringNode { NodeId = "start_b", Title = "开始 B", NodeKind = NodeKind.Start });
            chapter.Nodes.Add(new StoryAuthoringNode { NodeId = "end_a", Title = "结束 A", NodeKind = NodeKind.End });
            chapter.Nodes.Add(new StoryAuthoringNode { NodeId = "end_b", Title = "结束 B", NodeKind = NodeKind.End });
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
            var treeRows = window.rootVisualElement.Query<Button>(className: "story-editor__tree-row").ToList();
            var treeLabels = treeRows.Select(x => x.text).ToList();
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
            const string authoringPath = "Assets/GameDeveloperKit/Story/__StoryEditorCompileExportTest.asset";
            const string programPath = "Assets/GameDeveloperKit/Story/compiler_story.runtime.asset";
            AssetDatabase.DeleteAsset(authoringPath);
            AssetDatabase.DeleteAsset(programPath);
            m_CreatedAssetPaths.Add(authoringPath);
            m_CreatedAssetPaths.Add(programPath);

            var asset = CreateCompilerAsset();
            AssetDatabase.CreateAsset(asset, authoringPath);
            m_CreatedObjects.Remove(asset);
            var runtimeAsset = ScriptableObject.CreateInstance<StoryProgramAsset>();
            AssetDatabase.CreateAsset(runtimeAsset, programPath);
            asset.RuntimeProgramAssetPath = programPath;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var window = CreateStoryEditorWindow(asset);
            InvokePrivate(window, "CompileProgram");

            var exported = AssetDatabase.LoadAssetAtPath<StoryProgramAsset>(programPath);
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
            AddOrSetParameter(FindNode(asset, "video"), "clip", StorySampleGraphFixture.IntroVideoPath);
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

            InvokePrivate(window, "OpenPlaybackWindow");
            var playbackWindow = Resources.FindObjectsOfTypeAll<StoryEditorPlaybackWindow>()
                .FirstOrDefault(x => ReferenceEquals(GetPrivateField<StoryAuthoringAsset>(x, "m_Asset"), asset));
            if (playbackWindow != null)
            {
                m_CreatedObjects.Add(playbackWindow);
            }

            Assert.IsNotNull(playbackWindow);
            Assert.AreSame(asset, GetPrivateField<StoryAuthoringAsset>(playbackWindow, "m_Asset"));
            Assert.AreEqual(asset.EntryChapterId, GetPrivateField<string>(playbackWindow, "m_ChapterId"));
        }

        [Test]
        public void StoryProcedure_WhenPlayerViewIsPrefabAsset_InstantiatesScenePlayer()
        {
            var procedureType = Type.GetType("GameDeveloperKit.Story.StoryProcedure, GameDeveloperKit.StoryPresentation.AVPro");
            var requestType = Type.GetType("GameDeveloperKit.Story.StoryProcedureRequest, GameDeveloperKit.StoryPresentation.AVPro");
            Assert.IsNotNull(procedureType);
            Assert.IsNotNull(requestType);

            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameDeveloperKit/Runtime/StoryPresentation.AVPro/StoryPlayerView.prefab");
            Assert.IsNotNull(prefabRoot);
            var prefab = prefabRoot.GetComponent("StoryPlayerView");
            Assert.IsNotNull(prefab);
            Assert.IsFalse(prefab.gameObject.scene.IsValid());

            var request = Activator.CreateInstance(requestType);
            var playerViewProperty = requestType.GetProperty("PlayerView");
            Assert.IsNotNull(playerViewProperty);
            playerViewProperty.SetValue(request, prefab);
            var procedure = Activator.CreateInstance(procedureType);
            var resolve = procedureType.GetMethod("ResolvePlayerView", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(resolve);

            var player = (Component)resolve.Invoke(procedure, new[] { request });
            if (player != null)
            {
                m_CreatedObjects.Add(player.gameObject);
            }

            Assert.IsNotNull(player);
            Assert.AreNotSame(prefab, player);
            Assert.IsTrue(player.gameObject.scene.IsValid());
            Assert.IsTrue(player.name.StartsWith(prefab.name, StringComparison.Ordinal));
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
            var toggles = videoNode.Query<Toggle>().ToList();
            var labels = textFields.Select(x => x.label).ToList();
            var tooltips = textFields.Select(x => x.tooltip)
                .Concat(objectFields.Select(x => x.tooltip))
                .Concat(toggles.Select(x => x.tooltip))
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .ToList();
            var nodeText = string.Join("|", FindVisualChildren<Label>(videoNode).Select(x => x.text)
                .Concat(textFields.Select(x => x.label))
                .Concat(objectFields.Select(x => x.label))
                .Concat(toggles.Select(x => x.label)));

            Assert.IsNull(inspector);
            CollectionAssert.DoesNotContain(labels, "标题");
            CollectionAssert.Contains(objectFields.Select(x => x.label).ToList(), "视频 *");
            CollectionAssert.Contains(labels, "资源路径");
            CollectionAssert.Contains(toggles.Select(x => x.label).ToList(), "等待完成");
            Assert.AreEqual(typeof(UnityEngine.Object), objectFields.First(x => x.label == "视频 *").objectType);
            Assert.IsTrue(tooltips.Any(x => x.Contains("参数键：clip")));
            Assert.IsFalse(videoNode.Query<EnumField>().ToList().Any(x => x.label == "节点类型"));
            Assert.IsFalse(nodeText.IndexOf("Payload", StringComparison.OrdinalIgnoreCase) >= 0, nodeText);
            Assert.IsFalse(nodeText.IndexOf("Owner", StringComparison.OrdinalIgnoreCase) >= 0, nodeText);
            Assert.IsTrue(tooltips.Count >= 2, string.Join(",", tooltips));
        }

        [Test]
        public void StoryEditorGraph_WhenAssetFieldWritesStableId_StoresStringParameterOnly()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var video = asset.Chapters[0].Nodes.First(x => x.NodeId == "video");

            InvokePrivate(window, "SetNodeFieldFromGraph", "video", "clip", StorySampleGraphFixture.IntroVideoPath);

            Assert.AreEqual(StorySampleGraphFixture.IntroVideoPath, video.Parameters.First(x => x.Key == "clip").Value);
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
                .FirstOrDefault(x => FindVisualChildren<ObjectField>(x).Any(field => field.label == "视频 *"));
            var summary = window.rootVisualElement.Query<Button>(className: "story-editor__issue").ToList()
                .FirstOrDefault(x => x.text.Contains("必填命令字段未填写"));

            Assert.IsNotNull(field);
            Assert.IsNotNull(summary);
            Assert.IsTrue(videoNode.ClassListContains("editor-node-graph-node--diagnostic-error"));
            Assert.IsTrue(field.ClassListContains("editor-node-graph-node__field--diagnostic-error"));
            StringAssert.Contains("必填命令字段未填写", summary.text);
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
            var summaryText = string.Join("|", window.rootVisualElement.Query<Button>(className: "story-editor__issue").ToList().Select(x => x.text));

            Assert.IsTrue(duration.ClassListContains("editor-node-graph-node__field--diagnostic-error"));
            StringAssert.Contains("字段必须填写数字", duration.tooltip);
            StringAssert.Contains("字段必须填写数字", summaryText);
        }

        [Test]
        public void StoryEditorGraph_WhenBooleanFieldInvalid_ShowsFieldDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            var video = asset.Chapters[0].Nodes.First(x => x.NodeId == "video");
            AddOrSetParameter(video, "clip", StorySampleGraphFixture.IntroVideoPath);
            AddOrSetParameter(video, "wait", "maybe");
            var window = CreateStoryEditorWindow(asset);

            var videoNode = FindStoryEditorNodeView(window, "video");
            var wait = videoNode.Query<Toggle>().ToList().First(x => x.label == "等待完成");
            var summaryText = string.Join("|", window.rootVisualElement.Query<Button>(className: "story-editor__issue").ToList().Select(x => x.text));

            Assert.IsTrue(wait.ClassListContains("editor-node-graph-node__field--diagnostic-error"));
            StringAssert.Contains("字段必须填写布尔值", wait.tooltip);
            StringAssert.Contains("字段必须填写布尔值", summaryText);
        }

        [Test]
        public void StoryEditorGraph_WhenAssetFieldIsManualString_ShowsWarningDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            AddOrSetParameter(asset.Chapters[0].Nodes.First(x => x.NodeId == "video"), "clip", "intro.mp4");
            var window = CreateStoryEditorWindow(asset);

            var videoNode = FindStoryEditorNodeView(window, "video");
            var clipField = videoNode.Query<VisualElement>(className: "editor-node-graph-node__field").ToList()
                .FirstOrDefault(x => FindVisualChildren<ObjectField>(x).Any(field => field.label == "视频 *"));
            var summaryText = string.Join("|", window.rootVisualElement.Query<Button>(className: "story-editor__issue").ToList().Select(x => x.text));

            Assert.IsNotNull(clipField);
            Assert.IsTrue(clipField.ClassListContains("editor-node-graph-node__field--diagnostic-warning"));
            StringAssert.Contains("资源引用不是项目资源路径", clipField.tooltip);
            StringAssert.Contains("资源引用不是项目资源路径", summaryText);
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
            var summaryText = string.Join("|", window.rootVisualElement.Query<Button>(className: "story-editor__issue").ToList().Select(x => x.text));

            Assert.IsTrue(choiceNode.ClassListContains("editor-node-graph-node--diagnostic-error"));
            Assert.IsTrue(selectedPort.ClassListContains("editor-node-graph-node__port-dot--diagnostic-error"));
            StringAssert.Contains("选项必须且只能连接一个", selectedPort.tooltip);
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
            var summaryText = string.Join("|", window.rootVisualElement.Query<Button>(className: "story-editor__issue").ToList().Select(x => x.text));

            Assert.IsTrue(completedPort.ClassListContains("editor-node-graph-node__port-dot--diagnostic-error"));
            StringAssert.Contains("完成端口不能同时连接选项和普通流程", completedPort.tooltip);
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
                x.Tooltip.Contains("必填命令字段未填写") && x.Tooltip.Contains("node:video"));

            InvokePrivate(window, "FocusDiagnostic", item.RawItem);

            var selectedNode = GetPrivateField<StoryAuthoringNode>(window, "m_SelectedNode");
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

            var selectedEdge = GetPrivateField<StoryAuthoringEdge>(window, "m_SelectedEdge");
            Assert.IsNotNull(selectedEdge);
            Assert.AreEqual("edge_video_unknown", selectedEdge.EdgeId);
        }

        [Test]
        public void StoryEditorGraph_WhenNodesAddedFromTemplate_CreatesAndConnectsReadableEdge()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var chapter = asset.Chapters[0];

            InvokePrivate(window, "SetNodeFieldFromGraph", "video", "clip", StorySampleGraphFixture.IntroVideoPath);
            var command = InvokePrivate<StoryAuthoringNode>(
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

            var deniedStart = InvokePrivate<StoryAuthoringNode>(window, "AddNodeAt", Vector2.zero, NodeKind.Start, null, null, null);
            var deniedEnd = InvokePrivate<StoryAuthoringNode>(window, "AddNodeAt", Vector2.zero, NodeKind.End, null, null, null);
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
        public void StoryEditorGraphAdapter_WhenPortsAreInvalid_ReturnsChineseReason()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Chapters[0].Nodes.Add(CreateNode("choice_extra", "备用选项", NodeKind.Choice));
            asset.Chapters[0].Nodes.Add(CreateNode("merge", "等待全部完成", NodeKind.Merge));
            asset.Chapters[0].Nodes.Add(CreateNode("narration", "旁白", NodeKind.Narration, ("textKey", "story.narration")));
            var window = CreateStoryEditorWindow(asset);
            var adapter = GetPrivateField<IEditorNodeGraphAdapter>(window, "m_GraphAdapter");

            var videoToChoice = adapter.CanConnect(new EditorGraphPortRef("video", "completed"), new EditorGraphPortRef("choice_extra", "in"));
            var lineToChoice = adapter.CanConnect(new EditorGraphPortRef("line_intro", "completed"), new EditorGraphPortRef("choice_extra", "in"));
            var lineToMerge = adapter.CanConnect(new EditorGraphPortRef("line_intro", "completed"), new EditorGraphPortRef("merge", "in"));
            var narrationToMerge = adapter.CanConnect(new EditorGraphPortRef("narration", "completed"), new EditorGraphPortRef("merge", "in"));
            var choiceToEnd = adapter.CanConnect(new EditorGraphPortRef("choice", "selected"), new EditorGraphPortRef("end", "in"));
            var choiceUnknown = adapter.CanConnect(new EditorGraphPortRef("choice", "help"), new EditorGraphPortRef("mini_game", "in"));
            var endOutput = adapter.CanConnect(new EditorGraphPortRef("end", "completed"), new EditorGraphPortRef("mini_game", "in"));

            Assert.IsFalse(videoToChoice.Allowed);
            StringAssert.Contains("选项节点只能接在对白、旁白或等待全部完成的完成端口后", videoToChoice.Message);
            Assert.IsTrue(lineToChoice.Allowed, lineToChoice.Message);
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
            var files = Directory.GetFiles("Assets/GameDeveloperKit/Runtime/Story", "*.cs", SearchOption.AllDirectories);
            var source = string.Join(Environment.NewLine, files.Select(System.IO.File.ReadAllText));

            Assert.IsFalse(source.Contains("EditorNodeGraph"), "Story runtime must not reference editor graph kit.");
            Assert.IsFalse(source.Contains("UnityEditor"), "Story runtime must not reference UnityEditor.");
            Assert.IsFalse(source.Contains("AssetDatabase"), "Story runtime must not reference AssetDatabase.");
            Assert.IsFalse(source.Contains("ObjectField"), "Story runtime must not reference UI Toolkit ObjectField.");
            Assert.IsFalse(source.Contains("UIElements"), "Story runtime must not reference UI Toolkit.");
            Assert.IsFalse(source.Contains("VideoClip"), "Story runtime must not reference concrete video clip types.");
            Assert.IsFalse(source.Contains("StoryEditorPlaybackWindow"), "Story runtime must not reference the editor playback window.");
        }

        private StoryAuthoringAsset CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<StoryAuthoringAsset>();
            m_CreatedObjects.Add(asset);
            return asset;
        }

        private StoryAuthoringAsset CreateCompilerAsset(
            string helpTargetNodeId = "video",
            string videoClip = StorySampleGraphFixture.IntroVideoPath,
            string videoWait = "true",
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
            if (videoClip != null)
            {
                videoParameters.Add(("clip", videoClip));
            }

            if (videoWait != null)
            {
                videoParameters.Add(("wait", videoWait));
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

        private StoryAuthoringAsset CreateParallelCompilerAsset(
            bool missingChoiceMerge = false,
            bool nestedParallel = false,
            bool mixedMergeOwners = false,
            bool choiceInsideParallel = false)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.EntryChapterId = "chapter_01";

            var nodes = new List<StoryAuthoringNode>
            {
                CreateNode("start", "开始", NodeKind.Start),
                CreateNode("parallel", "并行", NodeKind.Parallel),
                CreateNode("video", "播放视频", NodeKind.PlayVideo, ("clip", StorySampleGraphFixture.IntroVideoPath), ("wait", "true")),
                CreateNode("narration", "旁白", NodeKind.Narration, ("textKey", "story.parallel.narration")),
                CreateNode("choice", "继续", NodeKind.Choice, ("textKey", "choice.continue")),
                CreateNode("merge", "等待全部完成", NodeKind.Merge),
                CreateNode("after_merge", "等待后", NodeKind.Narration, ("textKey", "story.after.merge")),
                CreateNode("end", "结束", NodeKind.End),
            };

            var edges = new List<StoryAuthoringEdge>
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
                edges.Add(CreateEdge("choice", "selected", "选择后", choiceInsideParallel ? "merge" : "after_merge"));
                if (choiceInsideParallel is false)
                {
                    edges.Add(CreateEdge("merge", "completed", "进入选择", "choice"));
                }
            }

            if (nestedParallel)
            {
                nodes.Add(CreateNode("nested_parallel", "嵌套并行", NodeKind.Parallel));
                nodes.Add(CreateNode("nested_line", "嵌套旁白", NodeKind.Narration, ("textKey", "nested.line")));
                edges.Add(CreateEdge("nested_parallel", "branch_a", "分支 A", "nested_line"));
                edges.Add(CreateEdge("nested_parallel", "branch_b", "分支 B", "merge"));
                edges.Add(CreateEdge("nested_line", "completed", "完成", "merge"));
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

        private StoryAuthoringAsset CreateParallelChoiceTargetAsset()
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
                    CreateNode("video", "播放视频", NodeKind.PlayVideo, ("clip", StorySampleGraphFixture.IntroVideoPath), ("wait", "true")),
                    CreateNode("line", "对白", NodeKind.Dialogue, ("textKey", "story.choice.line"), ("speaker", "NPC")),
                    CreateNode("choice_a", "选择 A", NodeKind.Choice, ("textKey", "choice.a")),
                    CreateNode("choice_b", "选择 B", NodeKind.Choice, ("textKey", "choice.b")),
                    CreateNode("selected_audio", "选择后音频", NodeKind.PlayAudio, ("clip", StorySampleGraphFixture.StationAudioPath)),
                    CreateNode("unselected_image", "未选择图片", NodeKind.ShowImage, ("image", StorySampleGraphFixture.MapImagePath)),
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

        private StoryAuthoringAsset CreateParallelChoiceTargetParallelAsset()
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
                    CreateNode("intro_video", "播放视频", NodeKind.PlayVideo, ("clip", StorySampleGraphFixture.IntroVideoPath), ("wait", "true")),
                    CreateNode("line", "对白", NodeKind.Dialogue, ("textKey", "story.choice.line"), ("speaker", "NPC")),
                    CreateNode("choice_a", "选择 A", NodeKind.Choice, ("textKey", "choice.a")),
                    CreateNode("choice_b", "选择 B", NodeKind.Choice, ("textKey", "choice.b")),
                    CreateNode("after_choice_parallel", "选择后并行", NodeKind.Parallel),
                    CreateNode("after_audio", "选择后音频", NodeKind.PlayAudio, ("clip", StorySampleGraphFixture.StationAudioPath)),
                    CreateNode("after_video", "选择后视频", NodeKind.PlayVideo, ("clip", StorySampleGraphFixture.IntroVideoPath), ("wait", "true")),
                    CreateNode("after_merge", "选择后等待全部完成", NodeKind.Merge),
                    CreateNode("after_line", "选择后对白", NodeKind.Dialogue, ("textKey", "after.choice.line")),
                    CreateNode("unselected_image", "未选择图片", NodeKind.ShowImage, ("image", StorySampleGraphFixture.MapImagePath)),
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

        private StoryAuthoringAsset CreateThreeBranchParallelAsset()
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
                    CreateNode("image", "显示图片", NodeKind.ShowImage, ("image", StorySampleGraphFixture.MapImagePath)),
                    CreateNode("audio", "播放音频", NodeKind.PlayAudio, ("clip", StorySampleGraphFixture.StationAudioPath)),
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

        private StoryAuthoringAsset CreateSemanticGraphAsset()
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
                Array.Empty<StoryAuthoringEdge>());

            asset.Chapters.Add(chapter);
            asset.Chapters.Add(target);
            AddLayout(asset, "chapter_01", "video", 180f, 120f);
            return asset;
        }

        private static StoryAuthoringChapter CreateChapter(
            string chapterId,
            string title,
            string entryNodeId,
            IReadOnlyList<StoryAuthoringNode> nodes,
            IReadOnlyList<StoryAuthoringEdge> edges)
        {
            var chapter = new StoryAuthoringChapter
            {
                ChapterId = chapterId,
                Title = title,
                EntryNodeId = entryNodeId
            };
            chapter.Nodes.AddRange(nodes);
            chapter.Edges.AddRange(edges);
            return chapter;
        }

        private static StoryAuthoringNode CreateNode(string nodeId, string title, NodeKind kind, params (string key, string value)[] parameters)
        {
            var node = new StoryAuthoringNode
            {
                NodeId = nodeId,
                Title = title,
                NodeKind = kind
            };
            for (var i = 0; i < parameters.Length; i++)
            {
                node.Parameters.Add(new StoryAuthoringParameter
                {
                    Key = parameters[i].key,
                    Value = parameters[i].value
                });
            }

            return node;
        }

        private static StoryAuthoringEdge CreateEdge(
            string fromNodeId,
            string fromPortId,
            string fromPortLabel,
            string targetNodeId,
            string edgeId = null,
            params StoryAuthoringCondition[] conditions)
        {
            var edge = new StoryAuthoringEdge
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

        private static StoryAuthoringEdge CreateStoryEndEdge(
            string fromNodeId,
            string fromPortId,
            string fromPortLabel,
            string edgeId = null)
        {
            return new StoryAuthoringEdge
            {
                EdgeId = edgeId ?? $"edge_{fromNodeId}_{fromPortId}_end",
                FromNodeId = fromNodeId,
                FromPortId = fromPortId,
                FromPortLabel = fromPortLabel,
                TargetKind = TransitionTargetKind.StoryEnd
            };
        }

        private static StoryAuthoringCondition CreateCondition(string conditionId)
        {
            return new StoryAuthoringCondition { ConditionId = conditionId };
        }

        private static StoryAuthoringNode FindNode(StoryAuthoringAsset asset, string nodeId)
        {
            return asset.Chapters
                .SelectMany(x => x.Nodes)
                .First(x => string.Equals(x.NodeId, nodeId, StringComparison.Ordinal));
        }

        private static void AddOrSetParameter(StoryAuthoringNode node, string key, string value)
        {
            var parameter = node.Parameters.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
            if (parameter == null)
            {
                node.Parameters.Add(new StoryAuthoringParameter { Key = key, Value = value });
            }
            else
            {
                parameter.Value = value;
            }
        }

        private static void AddLayout(StoryAuthoringAsset asset, string graphId, string nodeId, float x, float y)
        {
            asset.Layout.Nodes.Add(new StoryNodeLayout
            {
                GraphId = graphId,
                NodeId = nodeId,
                Position = new Vector2(x, y)
            });
        }

        private EditorWindow CreateStoryEditorWindow(StoryAuthoringAsset asset)
        {
            var window = ScriptableObject.CreateInstance<StoryEditorWindow>();
            m_CreatedObjects.Add(window);
            SetPrivateField(window, "m_Asset", asset);
            InvokePrivate(window, "SelectDefaults");
            InvokePrivate(window, "BuildLayout");
            InvokePrivate(window, "RefreshAll", "Ready.");
            return window;
        }

        private static StoryStep FindStep(StoryProgram program, string chapterId, string stepId)
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

        private static void AssertNoErrors(IEnumerable<StoryValidationIssue> issues)
        {
            Assert.IsFalse(issues.Any(x => x.Severity == StoryValidationSeverity.Error), FormatIssues(issues));
        }

        private static string FormatIssues(IEnumerable<StoryValidationIssue> issues)
        {
            return string.Join(Environment.NewLine, issues.Select(x => x.ToString()));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryInteractiveVideoSampleTests
    {
        [Test]
        public void SampleFixture_WhenInteractiveVideoChapterCompiled_CoversAuthoringContracts()
        {
            var asset = SampleGraphFixture.Create();
            try
            {
                var validation = AuthoringValidator.Validate(asset);
                var program = ProgramCompiler.Compile(asset, out var compilation);
                var chapter = SampleGraphFixture.FindChapter(asset, SampleGraphFixture.InteractiveVideoChapterId);
                var seekVideo = SampleGraphFixture.FindNode(chapter, "interactive_seek_video");
                var playbackVideo = SampleGraphFixture.FindNode(chapter, "interactive_playback_video");
                var qteVideo = SampleGraphFixture.FindNode(chapter, "interactive_qte_video");
                var unlockVideo = SampleGraphFixture.FindNode(chapter, "interactive_unlock_video");

                AssertNoErrors(validation.Issues);
                AssertNoErrors(compilation.Issues);
                Assert.IsNotNull(program);
                Assert.AreEqual(5, asset.Chapters.Count);
                CollectionAssert.AreEqual(SampleGraphFixture.ChapterIds, asset.Chapters.Select(x => x.ChapterId).ToArray());
                Assert.AreEqual(asset.Chapters.Sum(x => x.Nodes.Count), asset.Layout.Nodes.Count);
                Assert.AreEqual(asset.Layout.Nodes.Count, asset.Layout.Nodes.Select(x => x.GraphId + ":" + x.NodeId).Distinct().Count());
                AssertParameter(seekVideo, "allowSeek", "true");
                AssertParameter(playbackVideo, "allowSeek", "false");
                AssertParameter(qteVideo, "allowSeek", "false");
                AssertParameter(unlockVideo, "allowSeek", "false");
                Assert.AreEqual(NodeKind.Qte, SampleGraphFixture.FindNode(chapter, "interactive_qte").NodeKind);
                Assert.AreEqual(NodeKind.Unlock, SampleGraphFixture.FindNode(chapter, "interactive_unlock").NodeKind);

                var compiledSeekVideo = FindStep(program, "interactive_seek_video").Data.Command;
                var compiledPlaybackVideo = FindStep(program, "interactive_playback_video").Data.Command;
                Assert.IsTrue(compiledSeekVideo.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
                Assert.IsFalse(compiledPlaybackVideo.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
                Assert.AreEqual(InteractionCommandNames.Qte, FindStep(program, "interactive_qte").Data.Command.Name);
                Assert.AreEqual(InteractionCommandNames.Unlock, FindStep(program, "interactive_unlock").Data.Command.Name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [TestCase("interactive_choice_qte", "interactive_qte_video", "interactive_qte", InteractionCommandNames.SuccessOutcome, "interactive_qte_success")]
        [TestCase("interactive_choice_qte", "interactive_qte_video", "interactive_qte", InteractionCommandNames.FailOutcome, "interactive_qte_fail")]
        [TestCase("interactive_choice_unlock", "interactive_unlock_video", "interactive_unlock", InteractionCommandNames.SuccessOutcome, "interactive_unlock_success")]
        [TestCase("interactive_choice_unlock", "interactive_unlock_video", "interactive_unlock", InteractionCommandNames.FailOutcome, "interactive_unlock_fail")]
        public void SampleFixture_WhenInteractiveOutcomeCompleted_KeepsVideoTrackAndAdvancesBranch(
            string choiceId,
            string videoCommandId,
            string commandId,
            string outcome,
            string expectedStepId)
        {
            var asset = SampleGraphFixture.Create();
            var module = new StoryModule();
            module.Startup();
            try
            {
                var program = ProgramCompiler.Compile(asset, out var report);
                AssertNoErrors(report.Issues);
                module.Register(program);

                var frame = module.StartProgram(program.StoryId, SampleGraphFixture.InteractiveVideoChapterId).CurrentFrame;
                AssertCommandTrack(frame, "interactive_seek_video");
                Assert.IsTrue(frame.Tracks[0].Command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));

                frame = module.CompleteCommand("interactive_seek_video", MediaCommandNames.CompletedOutcome);
                AssertParallelFrame(frame, FrameTrackKind.Command, FrameTrackKind.Wait);
                Assert.AreEqual("interactive_playback_video", frame.Tracks[0].Command.CommandId);
                Assert.IsFalse(frame.Tracks[0].Command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
                Assert.IsTrue(frame.WaitsForCommand);
                Assert.IsTrue(frame.WaitsForTime);

                frame = module.Evaluate(1d);
                AssertParallelFrame(frame, FrameTrackKind.Command);
                Assert.AreEqual(2, frame.Choices.Count);
                Assert.IsTrue(frame.Choices.Any(x => x.ChoiceId == "interactive_choice_qte"));
                Assert.IsTrue(frame.Choices.Any(x => x.ChoiceId == "interactive_choice_unlock"));
                Assert.IsTrue(frame.WaitsForChoice);
                Assert.IsTrue(frame.WaitsForCommand);

                frame = module.Select(choiceId);
                AssertInteractiveTracks(frame, FrameTrackKind.Command, FrameTrackKind.Wait);
                Assert.AreEqual(videoCommandId, frame.Tracks[0].Command.CommandId);
                Assert.IsTrue(frame.WaitsForCommand);
                Assert.IsTrue(frame.WaitsForTime);

                frame = module.Evaluate(1d);
                AssertInteractiveTracks(frame, FrameTrackKind.Command, FrameTrackKind.Command);
                Assert.AreEqual(videoCommandId, frame.Tracks[0].Command.CommandId);
                Assert.AreEqual(commandId, frame.Tracks[1].Command.CommandId);

                frame = module.CompleteCommand(commandId, outcome);
                AssertInteractiveTracks(frame, FrameTrackKind.Command, FrameTrackKind.Text);
                Assert.AreEqual(videoCommandId, frame.Tracks[0].Command.CommandId);
                Assert.AreEqual(expectedStepId, frame.Tracks[1].Step.StepId);
                Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            }
            finally
            {
                module.Shutdown();
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void SampleFixture_WhenCanonicalShapeChecked_RefreshIsIdempotentAndDetectsLegacyAsset()
        {
            var asset = SampleGraphFixture.Create();
            try
            {
                Assert.IsFalse(ShouldRefresh(asset));

                asset.SelectedVolume.Chapters.RemoveAll(x =>
                    string.Equals(x.ChapterId, SampleGraphFixture.InteractiveVideoChapterId, StringComparison.Ordinal));

                Assert.IsTrue(ShouldRefresh(asset));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static Step FindStep(Program program, string stepId)
        {
            var episode = program.Volumes.SelectMany(x => x.Episodes).First(x => x.EpisodeId == SampleGraphFixture.InteractiveVideoChapterId);
            return episode.Steps.First(x => x.StepId == stepId);
        }

        private static void AssertCommandTrack(Frame frame, string commandId)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.AreEqual(FrameTrackKind.Command, frame.Tracks[0].Kind);
            Assert.AreEqual(commandId, frame.Tracks[0].Command.CommandId);
        }

        private static void AssertParallelFrame(Frame frame, params FrameTrackKind[] kinds)
        {
            AssertInteractiveFrame(frame, "interactive_parallel", kinds);
        }

        private static void AssertInteractiveFrame(Frame frame, string anchorStepId, params FrameTrackKind[] kinds)
        {
            AssertInteractiveTracks(frame, kinds);
            Assert.AreEqual(anchorStepId, frame.AnchorStep.StepId);
        }

        private static void AssertInteractiveTracks(Frame frame, params FrameTrackKind[] kinds)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(SampleGraphFixture.InteractiveVideoChapterId, frame.Episode.EpisodeId);
            CollectionAssert.AreEqual(kinds, frame.Tracks.Select(x => x.Kind).ToArray());
        }

        private static void AssertParameter(AuthoringNode node, string key, string expected)
        {
            Assert.IsNotNull(node);
            Assert.AreEqual(expected, node.Parameters.First(x => x.Key == key).Value);
        }

        private static bool ShouldRefresh(AuthoringAsset asset)
        {
            var method = typeof(SampleGraphFixture).GetMethod(
                "ShouldRefreshSample",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { asset });
        }

        private static void AssertNoErrors(IReadOnlyList<ValidationIssue> issues)
        {
            var errors = issues.Where(x => x.Severity == ValidationSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors.Select(x => x.Message)));
        }
    }
}

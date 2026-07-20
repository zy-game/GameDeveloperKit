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
        public void SampleFixture_WhenInteractiveVideoEpisodeCompiled_CoversAuthoringContracts()
        {
            var asset = SampleGraphFixture.Create();
            try
            {
                var validation = AuthoringValidator.Validate(asset);
                var program = ProgramCompiler.Compile(asset, out var compilation);
                var episode = SampleGraphFixture.FindEpisode(asset, SampleGraphFixture.InteractiveVideoEpisodeId);
                var seekVideo = SampleGraphFixture.FindNode(episode, "interactive_seek_video");
                var playbackVideo = SampleGraphFixture.FindNode(episode, "interactive_playback_video");
                var qteVideo = SampleGraphFixture.FindNode(episode, "interactive_qte_video");
                var unlockVideo = SampleGraphFixture.FindNode(episode, "interactive_unlock_video");

                AssertNoErrors(validation.Issues);
                AssertNoErrors(compilation.Issues);
                Assert.IsNotNull(program);
                Assert.AreEqual(5, asset.Episodes.Count);
                CollectionAssert.AreEqual(SampleGraphFixture.EpisodeIds, asset.Episodes.Select(x => x.EpisodeId).ToArray());
                Assert.AreEqual(asset.Episodes.Sum(x => x.Nodes.Count), asset.Episodes.Sum(x => x.DetailLayout.Nodes.Count));
                Assert.IsTrue(asset.Episodes.All(x => x.DetailLayout.Nodes.Count == x.DetailLayout.Nodes.Select(y => y.NodeId).Distinct().Count()));
                AssertParameter(seekVideo, "allowSeek", "true");
                AssertParameter(playbackVideo, "allowSeek", "false");
                AssertParameter(qteVideo, "allowSeek", "false");
                AssertParameter(unlockVideo, "allowSeek", "false");
                Assert.AreEqual(NodeKind.Logic, SampleGraphFixture.FindNode(episode, "interactive_qte").NodeKind);
                Assert.AreEqual(NodeKind.Logic, SampleGraphFixture.FindNode(episode, "interactive_unlock").NodeKind);

                var compiledSeekVideo = FindStep(program, "interactive_seek_video").Data.Command;
                var compiledPlaybackVideo = FindStep(program, "interactive_playback_video").Data.Command;
                Assert.IsTrue(compiledSeekVideo.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
                Assert.IsFalse(compiledPlaybackVideo.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
                Assert.AreEqual("sample.qte", FindStep(program, "interactive_qte").Data.Command.Name);
                Assert.AreEqual("sample.unlock", FindStep(program, "interactive_unlock").Data.Command.Name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [TestCase("success", "interactive_qte_success", "success", "interactive_unlock_success")]
        [TestCase("success", "interactive_qte_success", "fail", "interactive_unlock_fail")]
        [TestCase("fail", "interactive_qte_fail", "success", "interactive_unlock_success")]
        [TestCase("fail", "interactive_qte_fail", "fail", "interactive_unlock_fail")]
        public void SampleFixture_WhenSequentialInteractiveOutcomesCompleted_AdvancesBothEventBranches(
            string qteOutcome,
            string expectedQteStepId,
            string unlockOutcome,
            string expectedUnlockStepId)
        {
            var asset = SampleGraphFixture.Create();
            var module = new StoryModule();
            module.Startup();
            try
            {
                var program = ProgramCompiler.Compile(asset, out var report);
                AssertNoErrors(report.Issues);
                module.Register(program);

                var frame = module.StartProgram(program.StoryId, SampleGraphFixture.InteractiveVideoEpisodeId).CurrentFrame;
                AssertCommandTrack(frame, "interactive_seek_video");
                Assert.IsTrue(frame.Tracks[0].Command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));

                frame = module.CompleteCommand("interactive_seek_video", MediaCommandNames.CompletedOutcome);
                AssertCommandTrack(frame, "interactive_playback_video");
                Assert.IsFalse(frame.Tracks[0].Command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));

                frame = module.CompleteCommand("interactive_playback_video", MediaCommandNames.CompletedOutcome);
                AssertInteractiveFrame(frame, "interactive_qte_parallel", FrameTrackKind.Command, FrameTrackKind.Wait);
                Assert.AreEqual("interactive_qte_video", frame.Tracks[0].Command.CommandId);

                frame = module.Evaluate(1d);
                AssertInteractiveTracks(frame, FrameTrackKind.Command, FrameTrackKind.Command);
                Assert.AreEqual("interactive_qte_video", frame.Tracks[0].Command.CommandId);
                Assert.AreEqual("interactive_qte", frame.Tracks[1].Command.CommandId);

                frame = module.CompleteCommand("interactive_qte_video", MediaCommandNames.CompletedOutcome);
                AssertInteractiveTracks(frame, FrameTrackKind.Command);
                Assert.AreEqual("interactive_qte", frame.Tracks[0].Command.CommandId);

                frame = module.CompleteCommand("interactive_qte", qteOutcome);
                AssertInteractiveTracks(frame, FrameTrackKind.Text);
                Assert.AreEqual(expectedQteStepId, frame.Tracks[0].Step.StepId);

                frame = module.Continue();
                AssertInteractiveFrame(frame, "interactive_unlock_parallel", FrameTrackKind.Command, FrameTrackKind.Wait);
                Assert.AreEqual("interactive_unlock_video", frame.Tracks[0].Command.CommandId);

                frame = module.Evaluate(1d);
                AssertInteractiveTracks(frame, FrameTrackKind.Command, FrameTrackKind.Command);
                Assert.AreEqual("interactive_unlock_video", frame.Tracks[0].Command.CommandId);
                Assert.AreEqual("interactive_unlock", frame.Tracks[1].Command.CommandId);

                frame = module.CompleteCommand("interactive_unlock_video", MediaCommandNames.CompletedOutcome);
                AssertInteractiveTracks(frame, FrameTrackKind.Command);
                Assert.AreEqual("interactive_unlock", frame.Tracks[0].Command.CommandId);

                frame = module.CompleteCommand("interactive_unlock", unlockOutcome);
                AssertInteractiveTracks(frame, FrameTrackKind.Text);
                Assert.AreEqual(expectedUnlockStepId, frame.Tracks[0].Step.StepId);

                frame = module.Continue();
                Assert.IsTrue(frame.IsCompleted);
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

                asset.SelectedVolume.Episodes.RemoveAll(x =>
                    string.Equals(x.EpisodeId, SampleGraphFixture.InteractiveVideoEpisodeId, StringComparison.Ordinal));

                Assert.IsTrue(ShouldRefresh(asset));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static Step FindStep(Program program, string stepId)
        {
            var episode = program.Volumes.SelectMany(x => x.Episodes).First(x => x.EpisodeId == SampleGraphFixture.InteractiveVideoEpisodeId);
            return episode.Steps.First(x => x.StepId == stepId);
        }

        private static void AssertCommandTrack(Frame frame, string commandId)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.AreEqual(FrameTrackKind.Command, frame.Tracks[0].Kind);
            Assert.AreEqual(commandId, frame.Tracks[0].Command.CommandId);
        }

        private static void AssertInteractiveFrame(Frame frame, string anchorStepId, params FrameTrackKind[] kinds)
        {
            AssertInteractiveTracks(frame, kinds);
            Assert.AreEqual(anchorStepId, frame.AnchorStep.StepId);
        }

        private static void AssertInteractiveTracks(Frame frame, params FrameTrackKind[] kinds)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(SampleGraphFixture.InteractiveVideoEpisodeId, frame.Episode.EpisodeId);
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

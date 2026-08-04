using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
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
                var transition = SampleGraphFixture.FindNode(episode, "interactive_transition");

                AssertNoErrors(validation.Issues);
                AssertNoErrors(compilation.Issues);
                Assert.IsNotNull(program);
                Assert.AreEqual(SampleGraphFixture.EpisodeIds.Length, asset.Episodes.Count);
                CollectionAssert.AreEqual(SampleGraphFixture.EpisodeIds, asset.Episodes.Select(x => x.EpisodeId).ToArray());
                Assert.AreEqual(asset.Episodes.Sum(x => x.Nodes.Count), asset.Episodes.Sum(x => x.DetailLayout.Nodes.Count));
                Assert.IsTrue(asset.Episodes.All(x => x.DetailLayout.Nodes.Count == x.DetailLayout.Nodes.Select(y => y.NodeId).Distinct().Count()));
                AssertParameter(seekVideo, "allowSeek", "true");
                AssertParameter(playbackVideo, "allowSeek", "false");
                Assert.AreEqual(NodeKind.Transition, transition.NodeKind);

                var compiledSeekVideo = FindStep(program, "interactive_seek_video");
                var compiledPlaybackVideo = FindStep(program, "interactive_playback_video");
                Assert.AreEqual(StepKind.PlayVideo, compiledSeekVideo.Kind);
                Assert.AreEqual(StepKind.PlayVideo, compiledPlaybackVideo.Kind);
                Assert.IsTrue(compiledSeekVideo.Data.Seekable);
                Assert.IsFalse(compiledPlaybackVideo.Data.Seekable);
                Assert.AreEqual(StepKind.Transition, FindStep(program, "interactive_transition").Kind);
                Assert.IsTrue(program.Volumes[0].Route.Edges.Any(x =>
                    x.FromEpisodeId == SampleGraphFixture.InteractiveVideoEpisodeId &&
                    x.FromExitId == "interactive_transition" &&
                    x.ToEpisodeId == "episode_final"));
            }
            finally
            {
                DestroyFixture(asset);
            }
        }

        [Test]
        public void SampleFixture_WhenTransitionVideoCompleted_AutomaticallyStartsConvergedEpisode()
        {
            var asset = SampleGraphFixture.Create();
            var module = new StoryModule();
            var completions = new List<EpisodeCompletion>();
            module.Startup();
            try
            {
                var program = ProgramCompiler.Compile(asset, out var report);
                AssertNoErrors(report.Issues);
                module.Register(program);
                module.EpisodeCompleted += completions.Add;

                var volume = program.Volumes.Single(candidate => candidate.Episodes.Any(episode =>
                    episode.EpisodeId == SampleGraphFixture.InteractiveVideoEpisodeId));
                var frame = module.StartEpisode(
                    program.StoryId,
                    volume.VolumeId,
                    SampleGraphFixture.InteractiveVideoEpisodeId).CurrentFrame;
                AssertInstructionTrack(frame, "interactive_seek_video", true);

                frame = module.CompleteInstruction("interactive_seek_video");
                AssertInstructionTrack(frame, "interactive_playback_video", false);

                frame = module.CompleteInstruction("interactive_playback_video");
                Assert.AreEqual("episode_final", frame.Episode.EpisodeId);
                Assert.AreEqual("final_intro", frame.AnchorStep.StepId);
                Assert.AreEqual(1, completions.Count);
                Assert.AreEqual(EpisodeCompletionKind.Transition, completions[0].Kind);
                Assert.AreEqual(SampleGraphFixture.InteractiveVideoEpisodeId, completions[0].EpisodeId);
                Assert.AreEqual("episode_final", completions[0].NextEpisodeId);
            }
            finally
            {
                module.Shutdown();
                DestroyFixture(asset);
            }
        }

        [Test]
        public void SampleFixture_WhenCanonicalShapeChecked_RefreshIsIdempotentAndDetectsIncompleteAsset()
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
                DestroyFixture(asset);
            }
        }

        private static Step FindStep(Program program, string stepId)
        {
            var episode = program.Volumes.SelectMany(x => x.Episodes).First(x => x.EpisodeId == SampleGraphFixture.InteractiveVideoEpisodeId);
            return episode.Steps.First(x => x.StepId == stepId);
        }

        private static void AssertInstructionTrack(Frame frame, string instructionId, bool seekable)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(1, frame.Tracks.Count);
            Assert.AreEqual(FrameTrackKind.Instruction, frame.Tracks[0].Kind);
            Assert.AreEqual(instructionId, frame.Tracks[0].Instruction.InstructionId);
            Assert.AreEqual(seekable, ((StoryInstruction.PlayVideo)frame.Tracks[0].Instruction).Seekable);
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

        private static void DestroyFixture(AuthoringAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            var volumes = asset.VolumeAssets.ToArray();
            UnityEngine.Object.DestroyImmediate(asset);
            for (var i = 0; i < volumes.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(volumes[i]);
            }
        }

        private static void AssertNoErrors(IReadOnlyList<ValidationIssue> issues)
        {
            var errors = issues.Where(x => x.Severity == ValidationSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors.Select(x => x.Message)));
        }
    }
}

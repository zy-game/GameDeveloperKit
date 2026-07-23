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
                var transition = SampleGraphFixture.FindNode(episode, "interactive_transition");

                AssertNoErrors(validation.Issues);
                AssertNoErrors(compilation.Issues);
                Assert.IsNotNull(program);
                Assert.AreEqual(5, asset.Episodes.Count);
                CollectionAssert.AreEqual(SampleGraphFixture.EpisodeIds, asset.Episodes.Select(x => x.EpisodeId).ToArray());
                Assert.AreEqual(asset.Episodes.Sum(x => x.Nodes.Count), asset.Episodes.Sum(x => x.DetailLayout.Nodes.Count));
                Assert.IsTrue(asset.Episodes.All(x => x.DetailLayout.Nodes.Count == x.DetailLayout.Nodes.Select(y => y.NodeId).Distinct().Count()));
                AssertParameter(seekVideo, "allowSeek", "true");
                AssertParameter(playbackVideo, "allowSeek", "false");
                Assert.AreEqual(NodeKind.Transition, transition.NodeKind);

                var compiledSeekVideo = FindStep(program, "interactive_seek_video").Data.Command;
                var compiledPlaybackVideo = FindStep(program, "interactive_playback_video").Data.Command;
                Assert.IsTrue(compiledSeekVideo.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
                Assert.IsFalse(compiledPlaybackVideo.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
                Assert.AreEqual(StepKind.Transition, FindStep(program, "interactive_transition").Kind);
                Assert.IsTrue(program.Volumes[0].Route.Edges.Any(x =>
                    x.FromEpisodeId == SampleGraphFixture.InteractiveVideoEpisodeId &&
                    x.FromExitId == "interactive_transition" &&
                    x.ToEpisodeId == "episode_final"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
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

                var frame = module.StartProgram(program.StoryId, SampleGraphFixture.InteractiveVideoEpisodeId).CurrentFrame;
                AssertCommandTrack(frame, "interactive_seek_video");
                Assert.IsTrue(frame.Tracks[0].Command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));

                frame = module.CompleteCommand("interactive_seek_video", MediaCommandNames.CompletedOutcome);
                AssertCommandTrack(frame, "interactive_playback_video");
                Assert.IsFalse(frame.Tracks[0].Command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));

                frame = module.CompleteCommand("interactive_playback_video", MediaCommandNames.CompletedOutcome);
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

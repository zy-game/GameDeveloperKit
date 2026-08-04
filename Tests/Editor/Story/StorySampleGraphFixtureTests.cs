using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StorySampleGraphFixtureTests
    {
        private readonly List<UnityEngine.Object> m_CreatedObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = m_CreatedObjects.Count - 1; i >= 0; i--)
            {
                if (m_CreatedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_CreatedObjects[i]);
                }
            }

            m_CreatedObjects.Clear();
        }

        [Test]
        public void SampleFixture_WhenBuilt_HasIndependentVolumesAndNoValidationErrors()
        {
            var asset = CreateFixtureAsset();

            var report = AuthoringValidator.Validate(asset);

            AssertNoErrors(report.Issues);
            Assert.AreEqual(SampleGraphFixture.StoryId, asset.StoryId);
            Assert.AreEqual(SampleGraphFixture.Version, asset.Version);
            Assert.AreEqual(2, asset.VolumeAssets.Count);
            Assert.AreEqual(2, asset.Volumes.Count);
            Assert.AreEqual(SampleGraphFixture.RootEpisodeId, asset.SelectedVolume.Route.Edges.Single(edge =>
                edge.SourceKind == RouteEdgeSourceKind.Root).ToEpisodeId);
            CollectionAssert.AreEqual(
                SampleGraphFixture.EpisodeIds,
                asset.Episodes.Select(episode => episode.EpisodeId).ToArray());
        }

        [Test]
        public void SampleFixture_WhenInspected_UsesCurrentMediaReferencesAndRelativePaths()
        {
            var asset = CreateFixtureAsset();
            var arrival = SampleGraphFixture.FindEpisode(asset, "episode_arrival");
            var station = SampleGraphFixture.FindEpisode(asset, "episode_station");
            var alley = SampleGraphFixture.FindEpisode(asset, "episode_alley");

            var video = SampleGraphFixture.FindNode(arrival, "arrival_video");
            var arrivalAudio = SampleGraphFixture.FindNode(arrival, "arrival_audio");
            var stationAudio = SampleGraphFixture.FindNode(station, "station_audio");
            var alleyVideo = SampleGraphFixture.FindNode(alley, "alley_video");

            AssertVideoReference(video, SampleGraphFixture.IntroVideoPath);
            AssertVideoReference(alleyVideo, SampleGraphFixture.AlleyVideoPath);
            AssertAudioReference(arrivalAudio, SampleGraphFixture.StationAudioPath);
            AssertAudioReference(stationAudio, SampleGraphFixture.StationAudioPath);
            Assert.IsTrue(asset.Episodes.SelectMany(episode => episode.Nodes)
                .All(node => NodeSchemaRegistry.IsDefaultAuthoringNode(node.NodeKind)));
            Assert.IsFalse(asset.Episodes.SelectMany(episode => episode.Nodes)
                .Any(node => node.Parameters.Any(parameter => parameter.Key == "mediaSource")));
        }

        [Test]
        public void SampleFixture_WhenCompiled_ProducesTypedProgramAndRunsPrimaryBranch()
        {
            var asset = CreateFixtureAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            Assert.AreEqual(StepKind.PlayVideo,
                FindStep(program, "episode_arrival", "arrival_video").Kind);
            Assert.AreEqual(SampleGraphFixture.IntroVideoPath,
                FindStep(program, "episode_arrival", "arrival_video").Data.VideoReference.Primary.Value);
            Assert.AreEqual(StepKind.PlayAudio,
                FindStep(program, "episode_arrival", "arrival_audio").Kind);

            var module = new StoryModule();
            module.Startup();
            try
            {
                module.Register(program);
                var volume = program.Volumes.First(candidate =>
                    candidate.Episodes.Any(episode => episode.EpisodeId == SampleGraphFixture.RootEpisodeId));
                var frame = module.StartEpisode(
                    program.StoryId,
                    volume.VolumeId,
                    SampleGraphFixture.RootEpisodeId).CurrentFrame;
                AssertTrack(frame, FrameTrackKind.Text, "arrival_intro");

                frame = module.Continue();
                AssertInstruction(frame, "arrival_video", typeof(StoryInstruction.PlayVideo));
                AssertInstruction(frame, "arrival_audio", typeof(StoryInstruction.PlayAudio));

                frame = module.CompleteInstruction("arrival_audio");
                Assert.IsTrue(frame.WaitsForInstruction);
                frame = module.CompleteInstruction("arrival_video");
                Assert.IsTrue(frame.WaitsForChoice);

                frame = module.Select("choice_enter_alley");
                Assert.AreEqual("episode_alley", frame.Episode.EpisodeId);
                AssertTrack(frame, FrameTrackKind.Text, "alley_line");

                frame = module.Continue();
                AssertInstruction(frame, "alley_door_audio", typeof(StoryInstruction.PlayAudio));
                frame = module.CompleteInstruction("alley_door_audio");
                AssertInstruction(frame, "alley_video", typeof(StoryInstruction.PlayVideo));
                frame = module.CompleteInstruction("alley_video");
                Assert.IsTrue(frame.IsCompleted);
            }
            finally
            {
                module.Shutdown();
            }
        }

        [Test]
        public void StoryRuntime_WhenScanned_DoesNotContainRemovedPlaybackFrameworkOrEditorDependencies()
        {
            var files = Directory.GetFiles(FrameworkFilePath("Runtime/Story"), "*.cs", SearchOption.AllDirectories);
            var source = string.Join(Environment.NewLine, files.Select(System.IO.File.ReadAllText));

            Assert.IsFalse(source.Contains("UnityEditor"));
            Assert.IsFalse(source.Contains("EditorNodeGraph"));
            Assert.IsFalse(source.Contains("ICommandHandler"));
            Assert.IsFalse(source.Contains("ICommandHandle"));
            Assert.IsFalse(source.Contains("LogicCommandHandler"));
            Assert.IsFalse(source.Contains("PlaybackView"));
        }

        private AuthoringAsset CreateFixtureAsset()
        {
            var asset = SampleGraphFixture.Create();
            m_CreatedObjects.Add(asset);
            for (var i = 0; i < asset.VolumeAssets.Count; i++)
            {
                m_CreatedObjects.Add(asset.VolumeAssets[i]);
            }

            return asset;
        }

        private static void AssertVideoReference(AuthoringNode node, string expectedPath)
        {
            var json = node.Parameters.Single(parameter =>
                parameter.Key == NodeSchemaRegistry.VideoReferenceParameter).Value;
            Assert.IsTrue(VideoReferenceCodec.TryDeserialize(json, out var reference, out var error), error);
            Assert.AreEqual(expectedPath, reference.Primary.Value);
            Assert.IsFalse(reference.Primary.Value.Contains("://"));
        }

        private static void AssertAudioReference(AuthoringNode node, string expectedPath)
        {
            var json = node.Parameters.Single(parameter =>
                parameter.Key == NodeSchemaRegistry.AudioReferenceParameter).Value;
            Assert.IsTrue(AudioReferenceCodec.TryDeserialize(json, out var reference, out var error), error);
            Assert.AreEqual(expectedPath, reference.Path.Value);
            Assert.IsFalse(reference.Path.Value.Contains("://"));
        }

        private static Step FindStep(Program program, string episodeId, string stepId)
        {
            return program.Volumes
                .SelectMany(volume => volume.Episodes)
                .Single(episode => episode.EpisodeId == episodeId)
                .Steps.Single(step => step.StepId == stepId);
        }

        private static void AssertTrack(Frame frame, FrameTrackKind kind, string stepId)
        {
            Assert.IsTrue(frame.Tracks.Any(track => track.Kind == kind && track.Step.StepId == stepId));
        }

        private static void AssertInstruction(Frame frame, string instructionId, Type instructionType)
        {
            Assert.IsTrue(frame.Instructions.Any(instruction =>
                instruction.InstructionId == instructionId && instruction.GetType() == instructionType));
        }

        private static void AssertNoErrors(IReadOnlyList<ValidationIssue> issues)
        {
            Assert.IsFalse(
                issues.Any(issue => issue.Severity == ValidationSeverity.Error),
                string.Join(Environment.NewLine, issues.Select(issue => issue.ToString())));
        }

        private static string FrameworkFilePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "GameDeveloperKit", relativePath));
        }
    }
}

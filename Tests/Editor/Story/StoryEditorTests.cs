using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Media;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Text;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryEditorTests
    {
        private readonly List<UnityEngine.Object> m_Objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = m_Objects.Count - 1; i >= 0; i--)
            {
                if (m_Objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_Objects[i]);
                }
            }

            m_Objects.Clear();
        }

        [Test]
        public void Compile_WhenCurrentMediaNodesAreUsed_ProducesTypedSteps()
        {
            var asset = CreateProject(
                Node("video", NodeKind.PlayVideo,
                    (NodeSchemaRegistry.VideoReferenceParameter, VideoJson("videos/chapter/master.m3u8")),
                    (NodeSchemaRegistry.LoopParameter, "true"),
                    (NodeSchemaRegistry.AllowSeekParameter, "true")),
                Node("image", NodeKind.ShowImage,
                    (NodeSchemaRegistry.ImageLocationParameter, "Images/ChapterCover")),
                Node("audio", NodeKind.PlayAudio,
                    (NodeSchemaRegistry.AudioReferenceParameter, AudioReferenceCodec.Serialize(
                        new AudioReference(new MediaPath("audio/theme.ogg")))),
                    (NodeSchemaRegistry.LoopParameter, "true"),
                    (NodeSchemaRegistry.VolumeParameter, "0.75"),
                    (NodeSchemaRegistry.PriorityParameter, "64")),
                Node("unlock", NodeKind.Unlock,
                    (NodeSchemaRegistry.UnlockIdParameter, "chapter-2")));

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report);
            var episode = program.Volumes[0].Episodes[0];
            var start = FindStep(episode, "start");
            Assert.AreEqual(TargetKind.Step, start.Data.Target.TargetKind);
            Assert.AreEqual("video", start.Data.Target.StepId);
            var video = FindStep(episode, "video");
            Assert.AreEqual(StepKind.PlayVideo, video.Kind);
            Assert.AreEqual("videos/chapter/master.m3u8", video.Data.VideoReference.Primary.Value);
            Assert.IsTrue(video.Data.Loop);
            Assert.IsTrue(video.Data.Seekable);
            var image = FindStep(episode, "image");
            Assert.AreEqual(StepKind.ShowImage, image.Kind);
            Assert.AreEqual("Images/ChapterCover", image.Data.ImageLocation);
            var audio = FindStep(episode, "audio");
            Assert.AreEqual(StepKind.PlayAudio, audio.Kind);
            Assert.AreEqual("audio/theme.ogg", audio.Data.AudioReference.Path.Value);
            Assert.IsTrue(audio.Data.Loop);
            Assert.AreEqual(0.75f, audio.Data.Volume);
            Assert.AreEqual(64, audio.Data.Priority);
            var unlock = FindStep(episode, "unlock");
            Assert.AreEqual(StepKind.Unlock, unlock.Kind);
            Assert.AreEqual("chapter-2", unlock.Data.UnlockId);
        }

        [Test]
        public void Compile_WhenVideoHasRenditions_PreservesRelativePaths()
        {
            var reference = new VideoReference(
                new MediaPath("videos/chapter/master.m3u8"),
                VideoFormat.Hls,
                new[]
                {
                    new VideoRendition(
                        "720P",
                        new MediaPath("videos/chapter/720/index.m3u8"),
                        1280,
                        720,
                        3000000,
                        90000)
                });
            var asset = CreateProject(Node(
                "video",
                NodeKind.PlayVideo,
                (NodeSchemaRegistry.VideoReferenceParameter, VideoReferenceCodec.Serialize(reference))));

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report);
            var restored = FindStep(program.Volumes[0].Episodes[0], "video").Data.VideoReference;
            Assert.AreEqual("videos/chapter/master.m3u8", restored.Primary.Value);
            Assert.AreEqual("videos/chapter/720/index.m3u8", restored.Renditions[0].Path.Value);
        }

        [Test]
        public void Compile_WhenVolumeHasHlsHomeVideo_PreservesReference()
        {
            var asset = CreateProject();
            asset.Volumes[0].HomeVideoReference = VideoJson("videos/home/volume/master.m3u8");

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report);
            Assert.AreEqual(
                "videos/home/volume/master.m3u8",
                program.Volumes[0].HomeVideoReference.Primary.Value);
        }

        [TestCase("not-json", "invalid")]
        [TestCase("{\"version\":2,\"primaryPath\":\"videos/home/volume.mp4\",\"format\":\"mp4\",\"renditions\":[]}", "hls")]
        public void Compile_WhenVolumeHomeVideoIsInvalid_ReportsBlockingError(
            string homeVideoReference,
            string expectedError)
        {
            var asset = CreateProject();
            asset.Volumes[0].HomeVideoReference = homeVideoReference;

            var program = ProgramCompiler.Compile(asset, out var report);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains(expectedError, Format(report).ToLowerInvariant());
        }

        [Test]
        public void Compile_WhenVideoReferenceIsAbsolute_ReportsBlockingError()
        {
            var asset = CreateProject(Node(
                "video",
                NodeKind.PlayVideo,
                (NodeSchemaRegistry.VideoReferenceParameter,
                    "{\"version\":2,\"primaryPath\":\"https://cdn.example.com/video.m3u8\",\"format\":\"hls\",\"renditions\":[]}")));

            var program = ProgramCompiler.Compile(asset, out var report);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("relative", Format(report));
        }

        [TestCase("-0.1")]
        [TestCase("1.1")]
        public void Compile_WhenAudioVolumeIsOutsideRange_ReportsError(string volume)
        {
            var asset = CreateProject(Node(
                "audio",
                NodeKind.PlayAudio,
                (NodeSchemaRegistry.AudioReferenceParameter, AudioReferenceCodec.Serialize(
                    new AudioReference(new MediaPath("audio/theme.ogg")))),
                (NodeSchemaRegistry.VolumeParameter, volume)));

            Assert.IsNull(ProgramCompiler.Compile(asset, out var report));
            StringAssert.Contains("volume", Format(report).ToLowerInvariant());
        }

        [TestCase("-1")]
        [TestCase("257")]
        public void Compile_WhenAudioPriorityIsOutsideRange_ReportsError(string priority)
        {
            var asset = CreateProject(Node(
                "audio",
                NodeKind.PlayAudio,
                (NodeSchemaRegistry.AudioReferenceParameter, AudioReferenceCodec.Serialize(
                    new AudioReference(new MediaPath("audio/theme.ogg")))),
                (NodeSchemaRegistry.PriorityParameter, priority)));

            Assert.IsNull(ProgramCompiler.Compile(asset, out var report));
            StringAssert.Contains("priority", Format(report).ToLowerInvariant());
        }

        [Test]
        public void Compile_WhenProjectHasNoVolumeAssets_FailsExplicitly()
        {
            var asset = Track(ScriptableObject.CreateInstance<AuthoringAsset>());
            asset.StoryId = "empty_story";
            asset.Version = "1";

            var program = ProgramCompiler.Compile(asset, out var report);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("volume asset", Format(report).ToLowerInvariant());
        }

        [Test]
        public void AuthoringAsset_ExposesVolumeAndEpisodeReadOnlyProjections()
        {
            var asset = CreateProject(Node("line", NodeKind.Dialogue,
                ("textKey", Literal("hello"))));

            Assert.AreEqual(1, asset.VolumeAssets.Count);
            Assert.AreEqual(1, asset.Volumes.Count);
            Assert.AreEqual(1, asset.Episodes.Count);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<AuthoringVolume>)asset.Volumes).Add(new AuthoringVolume()));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<AuthoringEpisode>)asset.Episodes).Clear());
        }

        [Test]
        public void NodeSchemaRegistry_UsesCurrentMediaFieldsOnly()
        {
            var video = NodeSchemaRegistry.Get(NodeKind.PlayVideo);
            var audio = NodeSchemaRegistry.Get(NodeKind.PlayAudio);
            var unlock = NodeSchemaRegistry.Get(NodeKind.Unlock);

            CollectionAssert.AreEqual(
                new[]
                {
                    NodeSchemaRegistry.VideoReferenceParameter,
                    NodeSchemaRegistry.LoopParameter,
                    NodeSchemaRegistry.AllowSeekParameter
                },
                video.Parameters.Select(parameter => parameter.Key));
            CollectionAssert.AreEqual(
                new[]
                {
                    NodeSchemaRegistry.AudioReferenceParameter,
                    NodeSchemaRegistry.LoopParameter,
                    NodeSchemaRegistry.VolumeParameter,
                    NodeSchemaRegistry.PriorityParameter
                },
                audio.Parameters.Select(parameter => parameter.Key));
            CollectionAssert.AreEqual(
                new[] { NodeSchemaRegistry.UnlockIdParameter },
                unlock.Parameters.Select(parameter => parameter.Key));
        }

        private AuthoringAsset CreateProject(params AuthoringNode[] actionNodes)
        {
            var asset = Track(ScriptableObject.CreateInstance<AuthoringAsset>());
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            var volumeAsset = Track(AuthoringVolumeAsset.CreateDefault("volume", "Volume"));
            var volume = volumeAsset.Volume;
            var episode = volume.Episodes[0];
            episode.Title = "Episode";
            episode.Nodes.Clear();
            episode.Edges.Clear();

            var start = Node("start", NodeKind.Start);
            var end = Node("end", NodeKind.End);
            episode.EntryNodeId = start.NodeId;
            episode.Nodes.Add(start);
            for (var i = 0; i < actionNodes.Length; i++)
            {
                episode.Nodes.Add(actionNodes[i]);
            }

            episode.Nodes.Add(end);
            var previous = start.NodeId;
            for (var i = 0; i < actionNodes.Length; i++)
            {
                episode.Edges.Add(Edge(previous, actionNodes[i].NodeId));
                previous = actionNodes[i].NodeId;
            }

            episode.Edges.Add(Edge(previous, end.NodeId));
            asset.ReplaceVolumeAssets(new[] { volumeAsset });
            return asset;
        }

        private static AuthoringNode Node(
            string nodeId,
            NodeKind kind,
            params (string key, string value)[] parameters)
        {
            var node = new AuthoringNode
            {
                NodeId = nodeId,
                Title = nodeId,
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

        private static AuthoringEdge Edge(string from, string to)
        {
            return new AuthoringEdge
            {
                EdgeId = $"{from}_{to}",
                FromNodeId = from,
                FromPortId = NodeSchemaRegistry.CompletedPort,
                FromPortLabel = "完成",
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = to
            };
        }

        private static Step FindStep(Episode episode, string stepId)
        {
            return episode.Steps.Single(step => step.StepId == stepId);
        }

        private static string VideoJson(string path)
        {
            return VideoReferenceCodec.Serialize(new VideoReference(new MediaPath(path), VideoFormat.Hls));
        }

        private static string Literal(string value)
        {
            return TextReferenceCodec.Serialize(new TextReference(TextMode.Literal, value));
        }

        private T Track<T>(T target) where T : UnityEngine.Object
        {
            m_Objects.Add(target);
            return target;
        }

        private static void AssertNoErrors(ValidationReport report)
        {
            Assert.IsNotNull(report);
            Assert.IsFalse(report.HasErrors, Format(report));
        }

        private static string Format(ValidationReport report)
        {
            return string.Join(Environment.NewLine, report.Issues.Select(issue => issue.ToString()));
        }
    }
}

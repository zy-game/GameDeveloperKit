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
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Playback;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Text;
using GameDeveloperKit.Story.Logic;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Excel;
using GameDeveloperKit.StoryEditor.Validation;
using GameDeveloperKit.StoryEditor.UI;
using GameDeveloperKit.StoryEditor.Publishing;

namespace GameDeveloperKit.Tests
{
    internal static class StoryEditorRouteTestExtensions
    {
        public static Runner StartProgram(this StoryModule module, string storyId, string episodeId = null)
        {
            if (!module.TryGetProgram(storyId, out var program))
            {
                throw new GameException($"Story program is not registered. story:{storyId}");
            }

            for (var volumeIndex = 0; volumeIndex < program.Volumes.Count; volumeIndex++)
            {
                var volume = program.Volumes[volumeIndex];
                for (var episodeIndex = 0; episodeIndex < volume.Episodes.Count; episodeIndex++)
                {
                    var episode = volume.Episodes[episodeIndex];
                    if (episode != null &&
                        (string.IsNullOrWhiteSpace(episodeId) || string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal)))
                    {
                        return module.StartEpisode(storyId, volume.VolumeId, episode.EpisodeId);
                    }
                }
            }

            throw new GameException($"Story episode does not exist. story:{storyId} episode:{episodeId}");
        }
    }

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

            var program = CompileCurrent(asset, out var report);
            var schema = program.CommandSchema.Definitions.First(x => x.Name == "play_video");
            var sourceArgument = schema.ArgumentDefinitions.First(x => x.Key == MediaCommandNames.MediaSourceArgument);
            var clipArgument = schema.ArgumentDefinitions.First(x => x.Key == "clip");
            var loopArgument = schema.ArgumentDefinitions.First(x => x.Key == "loop");

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);
            Assert.AreEqual("compiler_story", program.StoryId);
            Assert.AreEqual("episode_01", program.Volumes[0].Route.Edges[0].ToEpisodeId);
            Assert.AreEqual(1, program.Volumes[0].Episodes.Count);
            Assert.AreEqual("start", program.Volumes[0].Episodes[0].EntryStepId);
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

            var line = FindStep(program, "episode_01", "line_intro");
            Assert.AreEqual(StepKind.Line, line.Kind);
            Assert.AreEqual("story.intro.line", line.Data.TextKey);
            Assert.AreEqual("npc", line.Data.Speaker);
            CollectionAssert.Contains(line.Tags, "intro");

            var choice = FindStep(program, "episode_01", "line_intro_choices");
            Assert.AreEqual(StepKind.Choice, choice.Kind);
            Assert.AreEqual(2, choice.Choices.Count);
            Assert.AreEqual("choice_help", choice.Choices[0].ChoiceId);
            Assert.AreEqual("choice.help", choice.Choices[0].TextKey);
            Assert.AreEqual(ExpressionKind.Function, choice.Choices[0].Condition.Kind);
            Assert.AreEqual("can_help", choice.Choices[0].Condition.FunctionName);
            Assert.AreEqual("choice_help", choice.Choices[0].ExitId);

            var command = FindStep(program, "episode_01", "video");
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
        public void Excel_WhenExportedAndImported_RoundTripsEpisodeNodesParametersAndEdges()
        {
            var source = CreateAsset();
            source.StoryId = "excel_round_trip";
            source.Version = "1";
            source.LegacyEntryEpisodeId = "episode_01";
            source.Episodes.Add(CreateEpisode(
                "episode_01",
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
            target.LegacyEntryEpisodeId = source.LegacyEntryEpisodeId;
            var path = Path.Combine(Path.GetTempPath(), $"story-excel-{Guid.NewGuid():N}.xlsx");

            try
            {
                Exporter.Export(source, path);
                var report = Importer.Import(path, target);

                AssertNoErrors(report.Issues);
                Assert.AreEqual(1, target.Episodes.Count);
                var episode = target.Episodes[0];
                Assert.AreEqual("episode_01", episode.EpisodeId);
                Assert.AreEqual("第一章", episode.Title);
                Assert.AreEqual("start", episode.EntryNodeId);
                Assert.AreEqual(3, episode.Nodes.Count);
                Assert.AreEqual(NodeKind.Start, episode.Nodes.Single(x => x.NodeId == "start").NodeKind);
                var line = episode.Nodes.Single(x => x.NodeId == "line");
                Assert.AreEqual(NodeKind.Dialogue, line.NodeKind);
                Assert.AreEqual("story.excel.line", line.Parameters.Single(x => x.Key == "textKey").Value);
                Assert.AreEqual("npc", line.Parameters.Single(x => x.Key == "speaker").Value);
                Assert.AreEqual(NodeKind.End, episode.Nodes.Single(x => x.NodeId == "end").NodeKind);
                Assert.AreEqual(2, episode.Edges.Count);
                Assert.IsTrue(episode.Edges.Any(x => x.FromNodeId == "start" && x.TargetNodeId == "line"));
                Assert.IsTrue(episode.Edges.Any(x => x.FromNodeId == "line" && x.TargetNodeId == "end"));
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
        public void Excel_WhenTextReferenceContainsDelimiters_RoundTripsModeAndValue()
        {
            var encoded = TextReferenceCodec.Serialize(new TextReference(TextMode.Literal, "值=A;值=B"));
            var source = CreateAsset();
            source.StoryId = "excel_text_reference";
            source.Version = "1";
            source.LegacyEntryEpisodeId = "episode_01";
            source.Episodes.Add(CreateEpisode(
                "episode_01", "第一章", "line",
                new[] { CreateNode("line", "对白", NodeKind.Dialogue, ("textKey", encoded)) },
                Array.Empty<AuthoringEdge>()));
            var target = CreateAsset();
            target.StoryId = source.StoryId;
            target.Version = source.Version;
            target.LegacyEntryEpisodeId = source.LegacyEntryEpisodeId;
            var path = Path.Combine(Path.GetTempPath(), $"story-text-{Guid.NewGuid():N}.xlsx");
            try
            {
                Exporter.Export(source, path);
                var report = Importer.Import(path, target);

                AssertNoErrors(report.Issues);
                Assert.AreEqual(encoded, target.Episodes[0].Nodes[0].Parameters.Single(x => x.Key == "textKey").Value);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void ProgramCompiler_WhenExplicitLocalizationKeyMissingZhCn_ReturnsLocatedError()
        {
            var asset = CreateAsset();
            asset.StoryId = "localized_text";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";
            asset.Episodes.Add(CreateEpisode(
                "episode_01", "第一章", "line",
                new[]
                {
                    CreateNode("line", "对白", NodeKind.Dialogue,
                        ("textKey", TextReferenceCodec.Serialize(new TextReference(TextMode.LocalizationKey, "story.missing.key"))))
                },
                Array.Empty<AuthoringEdge>()));

            var program = CompileCurrent(asset, out var report);

            Assert.IsNull(program);
            StringAssert.Contains("node:line/field:textKey", FormatIssues(report.Issues));
            StringAssert.Contains("zh-CN", FormatIssues(report.Issues));
        }

        [Test]
        public void ProgramCompiler_WhenContentAuthoringFeaturesCombined_PreservesAllProtocols()
        {
            var video = new VideoReference(
                new MediaReference(MediaKind.Video, MediaSource.Cdn, "intro", "https://cdn.example.com/intro/master.m3u8"),
                VideoFormat.Hls,
                new[]
                {
                    new VideoRendition("720p", "intro-720", "https://cdn.example.com/intro/720.m3u8", 1280, 720, 2500000, 60000),
                    new VideoRendition("1080p", "intro-1080", "https://cdn.example.com/intro/1080.m3u8", 1920, 1080, 5000000, 60000)
                });
            var audio = AudioReferenceCodec.Serialize(new MediaReference(MediaKind.Audio, MediaSource.Resource, null, "story/audio/theme"));
            var line = TextReferenceCodec.Serialize(new TextReference(TextMode.Literal, "综合验收对白"));
            var asset = CreateAsset();
            asset.StoryId = "content_acceptance";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";
            asset.Episodes.Add(CreateEpisode(
                "episode_01", "综合验收", "video",
                new[]
                {
                    CreateNode("video", "CDN HLS", NodeKind.PlayVideo, ("clip", VideoReferenceCodec.Serialize(video)), ("allowSeek", "true")),
                    CreateNode("audio", "Resource Audio", NodeKind.PlayAudio, ("clip", audio)),
                    CreateNode("line", "Literal", NodeKind.Dialogue, ("textKey", line)),
                    CreateNode("logic", "Final Logic", NodeKind.Logic,
                        (LogicCommandCodec.LogicIdParameter, "sample.final-settlement"),
                        ("settlementId", "combined")),
                    CreateNode("retry", "Retry", NodeKind.Narration, ("textKey", "logic.retry")),
                    CreateNode("end", "End", NodeKind.End)
                },
                new[]
                {
                    CreateEdge("video", "completed", "完成", "audio"),
                    CreateEdge("audio", "completed", "完成", "line"),
                    CreateEdge("line", "completed", "完成", "logic"),
                    CreateEdge("logic", "completed", "完成", "end"),
                    CreateEdge("logic", "failed", "失败", "retry")
                }));

            var program = CompileCurrent(asset, out var report);

            AssertNoErrors(report.Issues);
            var videoCommand = FindStep(program, "episode_01", "video").Data.Command;
            var audioCommand = FindStep(program, "episode_01", "audio").Data.Command;
            var logic = FindStep(program, "episode_01", "logic").Data.Command;
            Assert.IsTrue(videoCommand.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
            Assert.AreEqual("hls", videoCommand.Arguments.GetString(MediaCommandNames.VideoFormatArgument));
            Assert.IsTrue(VideoReferenceCodec.TryDeserializeRenditions(videoCommand.Arguments.GetString(MediaCommandNames.VideoRenditionsArgument), out var renditions, out var renditionError), renditionError);
            Assert.AreEqual(2, renditions.Count);
            Assert.AreEqual(MediaCommandNames.MediaSourceResource, audioCommand.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
            Assert.AreEqual("story/audio/theme", audioCommand.Arguments.GetString(MediaCommandNames.ClipArgument));
            Assert.AreEqual(TextMode.Literal, FindStep(program, "episode_01", "line").Data.Text.Value.Mode);
            Assert.IsTrue(LogicCommandCodec.IsLogicCommand(logic));
            Assert.AreEqual("sample.final-settlement", logic.Name);
            Assert.AreEqual("combined", logic.Arguments.GetString("settlementId"));
            CollectionAssert.AreEquivalent(new[] { "completed", "failed" }, logic.OutcomePorts);
        }

        [Test]
        public void NodeSchemaRegistry_WhenMediaNodesQueried_ExposeLoopParameter()
        {
            var video = NodeSchemaRegistry.Get(NodeKind.PlayVideo);
            var audio = NodeSchemaRegistry.Get(NodeKind.PlayAudio);
            CollectionAssert.DoesNotContain(video.Parameters.Select(x => x.Key).ToList(), MediaCommandNames.VideoSourceArgument);
            Assert.AreEqual(ParameterValueType.AssetReference, video.Parameters.First(x => x.Key == MediaCommandNames.ClipArgument).ValueType);
            Assert.AreEqual(ParameterValueType.Boolean, video.Parameters.First(x => x.Key == "loop").ValueType);
            Assert.AreEqual(ParameterValueType.Boolean, video.Parameters.First(x => x.Key == "allowSeek").ValueType);
            Assert.AreEqual(ParameterValueType.Boolean, audio.Parameters.First(x => x.Key == "loop").ValueType);
            CollectionAssert.DoesNotContain(video.Parameters.Select(x => x.Key).ToList(), "playbackRole");
            CollectionAssert.DoesNotContain(video.Parameters.Select(x => x.Key).ToList(), "playbackRole");
        }

        [Test]
        public void ProgramCompiler_WhenAllowSeekEnabled_WritesPublicSeekableArgument()
        {
            var asset = CreateTransitionVideoAsset(allowSeek: true);

            var program = CompileCurrent(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "episode_01", "video").Data.Command;
            Assert.IsTrue(command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
            var schema = program.CommandSchema.Definitions.First(x => x.Name == MediaCommandNames.PlayVideo);
            CollectionAssert.Contains(schema.ArgumentNames.ToList(), MediaCommandNames.VideoSeekableArgument);
        }

        [Test]
        public void ProgramCompiler_WhenVideoTargetsParallel_WritesHiddenSeekPolicyOnlyForPreParallelVideo()
        {
            var asset = CreatePreParallelVideoAsset();

            var program = CompileCurrent(asset, out var report);

            AssertNoErrors(report.Issues);
            var introCommand = FindStep(program, "episode_01", "intro_video").Data.Command;
            var branchCommand = FindStep(program, "episode_01", "branch_video").Data.Command;
            Assert.IsTrue(introCommand.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
            Assert.IsFalse(branchCommand.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
        }

        [Test]
        public void ProgramCompiler_WhenVideoTargetsChoice_DoesNotWriteHiddenSeekPolicy()
        {
            var asset = CreateTransitionVideoAsset(videoTargetChoice: true);

            var program = CompileCurrent(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "episode_01", "video").Data.Command;
            Assert.IsFalse(command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
        }

        [Test]
        public void ProgramCompiler_WhenVideoIsInsideParallel_DoesNotWriteHiddenSeekPolicy()
        {
            var asset = CreateParallelCompilerAsset();

            var program = CompileCurrent(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "episode_01", "video").Data.Command;
            Assert.IsFalse(command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
        }

        [Test]
        public void ProgramCompiler_WhenVideoAndWaitChoiceAreParallel_DoesNotWriteHiddenSeekPolicy()
        {
            var asset = CreateParallelWaitChoiceAsset();

            var program = CompileCurrent(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "episode_01", "video").Data.Command;
            Assert.IsFalse(command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));

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
            Assert.AreEqual("choice", frame.Choices[0].ExitId);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForChoice);
            Assert.IsFalse(frame.WaitsForTime);
        }

        [Test]
        public void ProgramCompiler_WhenVideoLoops_PreservesExplicitSeekChoice()
        {
            var asset = CreateTransitionVideoAsset(videoLoop: true, allowSeek: true);

            var program = CompileCurrent(asset, out var report);

            AssertNoErrors(report.Issues);
            var command = FindStep(program, "episode_01", "video").Data.Command;
            Assert.IsTrue(command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument));
        }

        [Test]
        public void ProgramCompiler_WhenAllowSeekIsInvalid_ReturnsLocatedError()
        {
            var asset = CreateTransitionVideoAsset();
            asset.Episodes[0].Nodes.First(x => x.NodeId == "video").Parameters.Add(
                new AuthoringParameter { Key = "allowSeek", Value = "sometimes" });

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNotNull(program);
            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("node:video/field:allowSeek", issues);
            StringAssert.Contains("boolean", issues);
        }

        [Test]
        public void ProgramCompiler_WhenVideoClipPathDoesNotMatchSource_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoClip: InvalidStreamingAssetsVideoPath);

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:video/field:clip", issues);
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

            var program = CompileCurrent(asset, out var report);
            var command = FindStep(program, "episode_01", "video").Data.Command;

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
        public void ProgramCompiler_WhenCdnAudioReferenceCompiles_WritesSourceIdentityAndLocation()
        {
            var asset = CreateCompilerAsset();
            var episode = asset.Episodes[0];
            var audio = CreateNode(
                "audio",
                "主题音乐",
                NodeKind.PlayAudio,
                (MediaCommandNames.ClipArgument, AudioReferenceCodec.Serialize(new MediaReference(
                    MediaKind.Audio,
                    MediaSource.Cdn,
                    "theme",
                    "https://cdn.example.com/audio/theme.ogg"))));
            episode.Nodes.Add(audio);

            var program = CompileCurrent(asset, out var report);
            var command = FindStep(program, "episode_01", "audio").Data.Command;

            AssertNoErrors(report.Issues);
            Assert.AreEqual(MediaCommandNames.MediaSourceCdn, command.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
            Assert.AreEqual("theme", command.Arguments.GetString(MediaCommandNames.MediaIdArgument));
            Assert.AreEqual("https://cdn.example.com/audio/theme.ogg", command.Arguments.GetString(MediaCommandNames.ClipArgument));
        }

        [Test]
        public void ProgramCompiler_WhenLegacyStreamingVideoCompiles_ReportsMigrationWarning()
        {
            var asset = CreateCompilerAsset(videoClip: "Assets/StreamingAssets/story/intro.mp4");

            var program = CompileCurrent(asset, out var report);
            var command = FindStep(program, "episode_01", "video").Data.Command;
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

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:video/field:clip", issues);
            StringAssert.Contains("unsupported", issues);
        }

        [Test]
        public void ProgramCompiler_WhenVideoSourceMissing_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoSource: null);

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:video/field:clip", issues);
            StringAssert.Contains("missing or unsupported", issues);
        }

        [Test]
        public void ProgramCompiler_WhenCommandRequiredFieldMissing_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoClip: null);

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:video/field:clip", issues);
            StringAssert.Contains("Required video reference is missing.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenCommandBooleanFieldIsInvalid_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(videoLoop: "maybe");

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:video/field:loop", issues);
            StringAssert.Contains("Command field must be a boolean.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenWaitDurationIsInvalid_ReturnsLocatedError()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";
            asset.Episodes.Add(CreateEpisode(
                "episode_01",
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

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:wait/field:duration", issues);
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
            asset.LegacyEntryEpisodeId = "episode_01";
            asset.Episodes.Add(CreateEpisode(
                "episode_01",
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

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            StringAssert.Contains("Wait duration must be finite and non-negative.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenWaitOwnsChoiceItems_BuildsSyntheticChoiceStep()
        {
            var asset = CreateWaitChoiceCompilerAsset();

            var program = CompileCurrent(asset, out var report);

            AssertNoErrors(report.Issues);
            var wait = FindStep(program, "episode_01", "wait_choice");
            var choice = FindStep(program, "episode_01", "wait_choice_choices");

            Assert.AreEqual(StepKind.Wait, wait.Kind);
            Assert.AreEqual("wait_choice_choices", wait.Data.Target.StepId);
            Assert.AreEqual(StepKind.Choice, choice.Kind);
            Assert.AreEqual(2, choice.Choices.Count);
            Assert.AreEqual("choice_a", choice.Choices[0].ChoiceId);
            Assert.AreEqual("choice.a", choice.Choices[0].TextKey);
            Assert.AreEqual("choice_a", choice.Choices[0].ExitId);
            Assert.AreEqual("choice_b", choice.Choices[1].ChoiceId);
            Assert.AreEqual("choice.b", choice.Choices[1].TextKey);
            Assert.AreEqual("choice_b", choice.Choices[1].ExitId);

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

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_wait_choice/episode:episode_01/node:wait_choice/port:completed", issues);
            StringAssert.Contains("Wait completed output cannot mix choice items and direct flow targets.", issues);
        }

        [Test]
        public void ProgramCompiler_WhenChoiceItemHasNoDetailTarget_CompilesTerminalExit()
        {
            var asset = CreateCompilerAsset(includeChoiceHelpSelected: false);

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);
            var choice = FindStep(program, "episode_01", "line_intro_choices");
            Assert.AreEqual("choice_help", choice.Choices[0].ExitId);
        }

        [Test]
        public void ProgramCompiler_WhenChoiceItemHasLegacySelectedTarget_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(addExtraChoiceHelpSelected: true);

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:choice_help", issues);
            StringAssert.Contains("terminal Episode exits", issues);
        }

        [Test]
        public void ProgramCompiler_WhenChoiceItemMissingTextKey_UsesFallbackWithWarning()
        {
            var asset = CreateCompilerAsset(choiceHelpHasTextKey: false);

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:choice_help/field:textKey", issues);
            StringAssert.Contains("Choice item textKey is missing", issues);

            var choice = FindStep(program, "episode_01", "line_intro_choices");
            Assert.AreEqual("救人", choice.Choices[0].TextKey);
        }

        [Test]
        public void ProgramCompiler_WhenLegacySelectedTargetIsMissing_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset(helpTargetNodeId: "missing_step", includeChoiceHelpSelected: true);

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:choice_help", issues);
            StringAssert.Contains("terminal Episode exits", issues);
        }

        [Test]
        public void ProgramCompiler_WhenOldAuthoringNodeExists_ReturnsLocatedError()
        {
            var asset = CreateCompilerAsset();
            asset.Episodes[0].Nodes.Add(CreateNode("old_node", "旧节点", (NodeKind)999));

            var program = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, issues);
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:old_node", issues);
            StringAssert.Contains("Node kind is no longer supported in Story default authoring path.", issues);
        }

        [Test]
        public void StoryEditorGraph_WhenLegacyQteNodeExists_RendersPlaceholderWithoutThrowing()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Episodes[0].Nodes.Add(CreateNode("legacy_qte", "旧 QTE", (NodeKind)205));

            var window = CreateStoryEditorWindow(asset);
            var nodeView = FindStoryEditorNodeView(window, "legacy_qte");

            Assert.IsNotNull(nodeView);
            var text = string.Join("|", FindVisualChildren<Label>(nodeView).Select(x => x.text));
            StringAssert.Contains("已停用节点 (205)", text);
            Assert.IsTrue(nodeView.ClassListContains("editor-node-graph-node--diagnostic-error"));
        }

        [Test]
        public void ProgramCompiler_WhenParallelNaturalCompletionIsValid_BuildsParallelSteps()
        {
            var asset = CreateParallelCompilerAsset();

            var program = CompileCurrent(asset, out var report);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var parallel = FindStep(program, "episode_01", "parallel");
            var video = FindStep(program, "episode_01", "video");
            var narration = FindStep(program, "episode_01", "narration");

            Assert.AreEqual(StepKind.Parallel, parallel.Kind);
            Assert.AreEqual(2, parallel.Data.Branches.Count);
            Assert.AreEqual("branch_video", parallel.Data.Branches[0].BranchId);
            Assert.AreEqual("video", parallel.Data.Branches[0].Entry.StepId);
            Assert.AreEqual("branch_dialogue", parallel.Data.Branches[1].BranchId);
            Assert.AreEqual(TargetKind.EpisodeEnd, video.Data.Target.TargetKind);
            Assert.AreEqual(TargetKind.EpisodeEnd, narration.Data.Target.TargetKind);
            Assert.IsNull(parallel.Data.Target);
        }

        [Test]
        public void ProgramCompiler_WhenParallelProgramRuns_ProducesCombinedFrameAndCompletesNaturally()
        {
            var asset = CreateParallelCompilerAsset();
            var program = CompileCurrent(asset, out var report);
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
            Assert.IsTrue(frame.IsCompleted);
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.IsNull(frame.CompletedExitId);
        }

        [Test]
        public void ProgramCompiler_WhenThreeBranchParallelRuns_CombinesImageAudioTextThenChoice()
        {
            var asset = CreateThreeBranchParallelAsset();
            var program = CompileCurrent(asset, out var report);
            AssertNoErrors(report.Issues);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");
            var frame = runner.CurrentFrame;

            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(3, frame.Tracks.Count);
            Assert.AreEqual(1, frame.Choices.Count);
            Assert.AreEqual("image", frame.Tracks[0].Step.StepId);
            Assert.AreEqual("branch_image", frame.Tracks[0].BranchId);
            Assert.AreEqual("audio", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_audio", frame.Tracks[1].BranchId);
            Assert.AreEqual("line", frame.Tracks[2].Step.StepId);
            Assert.AreEqual("branch_text", frame.Tracks[2].BranchId);

            frame = runner.CompleteCommand("image", "completed");
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.AreEqual(1, frame.Choices.Count);

            frame = runner.CompleteCommand("audio", "completed");
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(1, frame.Choices.Count);

            frame = runner.Select("choice");
            Assert.IsTrue(frame.IsCompleted);
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual("choice", frame.CompletedExitId);
        }

        [Test]
        public void ProgramCompiler_WhenParallelTrackEndsNaturally_BuildsParallelWithoutWaitNode()
        {
            var asset = CreateParallelCompilerAsset();

            var program = CompileCurrent(asset, out var report);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var parallel = FindStep(program, "episode_01", "parallel");
            var narration = FindStep(program, "episode_01", "narration");

            Assert.AreEqual(StepKind.Parallel, parallel.Kind);
            Assert.AreEqual(2, parallel.Data.Branches.Count);
            Assert.IsNull(parallel.Data.Target);
            Assert.AreEqual(TargetKind.EpisodeEnd, narration.Data.Target.TargetKind);
        }

        [Test]
        public void ProgramCompiler_WhenParallelTracksEndNaturally_CompletesAfterAllTracks()
        {
            var asset = CreateParallelCompilerAsset();
            var program = CompileCurrent(asset, out var report);
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

            var program = CompileCurrent(asset, out var report);
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
        public void ProgramCompiler_WhenLegacyJumpNodeExists_ReturnsLocatedError()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";
            asset.Episodes.Add(CreateEpisode(
                "episode_01",
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
                    CreateNode("jump_next", "跳转章节", (NodeKind)2, ("episodeId", "episode_02")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("parallel", "branch_video", "视频轨", "video"),
                    CreateEdge("parallel", "branch_text", "文本轨", "line"),
                    CreateEdge("video", "completed", "完成", "jump_next"),
                }));
            asset.Episodes.Add(CreateEpisode(
                "episode_02",
                "第二章",
                "target_line",
                new[]
                {
                    CreateNode("target_line", "目标对白", NodeKind.Narration, ("textKey", "episode.02.line")),
                    CreateNode("target_end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("target_line", "completed", "完成", "target_end"),
                }));

            var program = CompileCurrent(asset, out var report);
            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors, FormatIssues(report.Issues));
            StringAssert.Contains("story:compiler_story/episode:episode_01/node:jump_next", FormatIssues(report.Issues));
            StringAssert.Contains("Node kind is no longer supported", FormatIssues(report.Issues));
        }

        [Test]
        public void ProgramCompiler_WhenParallelLineHasChoice_ShowsChoiceInsideParallelFrame()
        {
            var asset = CreateParallelCompilerAsset(choiceInsideParallel: true);

            var program = CompileCurrent(asset, out var report);
            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);

            var narration = FindStep(program, "episode_01", "narration");
            var syntheticChoice = FindStep(program, "episode_01", "narration_choices");
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
            Assert.IsTrue(frame.IsCompleted);
            Assert.IsTrue(runner.Completed);
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual("choice", frame.CompletedExitId);
        }

        [Test]
        public void ProgramCompiler_WhenParallelChoiceSelected_CompletesWithEpisodeExit()
        {
            var asset = CreateParallelChoiceTargetAsset();
            var program = CompileCurrent(asset, out var report);
            AssertNoErrors(report.Issues);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.AreEqual(2, frame.Choices.Count);

            frame = runner.Select("choice_a");
            Assert.IsTrue(frame.IsCompleted);
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual("choice_a", frame.CompletedExitId);
        }

        [Test]
        public void ProgramCompiler_WhenParallelChoiceSelected_CompletesWithoutWaitingForOtherTracks()
        {
            var asset = CreateParallelChoiceTargetAsset();
            var program = CompileCurrent(asset, out var report);
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

            Assert.IsTrue(frame.IsCompleted);
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual("choice_a", frame.CompletedExitId);
            Assert.AreEqual(0, frame.Tracks.Count);
            Assert.AreEqual(0, frame.Choices.Count);
        }

        [Test]
        public void ProgramCompiler_WhenParallelChoiceSelected_DoesNotEnterUnroutedDetailParallel()
        {
            var asset = CreateParallelChoiceTargetParallelAsset();
            var program = CompileCurrent(asset, out var report);
            AssertNoErrors(report.Issues);

            var module = new StoryModule();
            module.Register(program);
            var runner = module.StartProgram("compiler_story");

            var frame = runner.CurrentFrame;
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual(2, frame.Tracks.Count);
            Assert.AreEqual(2, frame.Choices.Count);

            frame = runner.Select("choice_a");
            Assert.IsTrue(frame.IsCompleted);
            Assert.AreEqual("parallel", frame.AnchorStep.StepId);
            Assert.AreEqual("choice_a", frame.CompletedExitId);
        }

        [Test]
        public void AuthoringAsset_WhenDefaultsEnsured_KeepsSingleStartAndMultipleEnds()
        {
            var asset = CreateAsset();
            asset.StoryId = "story";
            asset.LegacyEntryEpisodeId = "episode_01";

            var episode = new AuthoringEpisode
            {
                EpisodeId = "episode_01",
                Title = "第一章",
                EntryNodeId = "wrong_entry"
            };
            episode.Nodes.Add(new AuthoringNode { NodeId = "line", Title = "对白", NodeKind = NodeKind.Dialogue });
            episode.Nodes.Add(new AuthoringNode { NodeId = "start_a", Title = "开始 A", NodeKind = NodeKind.Start });
            episode.Nodes.Add(new AuthoringNode { NodeId = "start_b", Title = "开始 B", NodeKind = NodeKind.Start });
            episode.Nodes.Add(new AuthoringNode { NodeId = "end_a", Title = "结束 A", NodeKind = NodeKind.End });
            episode.Nodes.Add(new AuthoringNode { NodeId = "end_b", Title = "结束 B", NodeKind = NodeKind.End });
            episode.Edges.Add(CreateEdge("start_b", "completed", "完成", "line", "edge_duplicate_start"));
            episode.Edges.Add(CreateEdge("line", "completed", "完成", "end_b", "edge_duplicate_end"));
            asset.Episodes.Add(episode);

            asset.EnsureDefaults();

            var starts = episode.Nodes.Where(x => x.NodeKind == NodeKind.Start).ToList();
            var ends = episode.Nodes.Where(x => x.NodeKind == NodeKind.End).ToList();
            Assert.AreEqual(1, starts.Count);
            Assert.AreEqual(2, ends.Count);
            Assert.AreEqual("start_a", starts[0].NodeId);
            CollectionAssert.AreEquivalent(new[] { "end_a", "end_b" }, ends.Select(x => x.NodeId));
            Assert.AreEqual(starts[0].NodeId, episode.EntryNodeId);
            Assert.IsFalse(episode.Edges.Any(x =>
                string.Equals(x.FromNodeId, "start_b", StringComparison.Ordinal) ||
                string.Equals(x.TargetNodeId, "start_b", StringComparison.Ordinal)));
            Assert.IsTrue(episode.Edges.Any(x => string.Equals(x.TargetNodeId, "end_b", StringComparison.Ordinal)));
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
            const string manifestPath = "Assets/Bundles/Story/compiler_story.runtime.identity-manifest.json";
            const string reportPath = "Assets/Bundles/Story/compiler_story.runtime.identity-change.json";
            AssetDatabase.DeleteAsset(authoringPath);
            AssetDatabase.DeleteAsset(programPath);
            AssetDatabase.DeleteAsset(manifestPath);
            AssetDatabase.DeleteAsset(reportPath);
            m_CreatedAssetPaths.Add(authoringPath);
            m_CreatedAssetPaths.Add(programPath);
            m_CreatedAssetPaths.Add(manifestPath);
            m_CreatedAssetPaths.Add(reportPath);
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
            Assert.IsTrue(System.IO.File.Exists(manifestPath));
            Assert.IsTrue(System.IO.File.Exists(reportPath));
            Assert.IsTrue(asset.TryGetPublishedIdentity(out var published, out var error), error);
            Assert.AreEqual("compiler_story", published.StoryId);
        }

        [Test]
        public void PublishedIdentity_WhenBaselineCommitted_RoundTripsManifest()
        {
            var asset = CreateAsset();
            var source = new IdentityManifest(
                "story",
                "1",
                new[] { "episode_b", "episode_a" },
                new[] { "edge_b", "edge_a" },
                new[]
                {
                    new ExitIdentity("episode_b", "exit_b"),
                    new ExitIdentity("episode_a", "exit_a")
                });

            asset.CommitPublishedIdentity(source);

            Assert.IsTrue(asset.TryGetPublishedIdentity(out var restored, out var error), error);
            CollectionAssert.AreEqual(new[] { "episode_a", "episode_b" }, restored.EpisodeIds);
            CollectionAssert.AreEqual(new[] { "edge_a", "edge_b" }, restored.EdgeIds);
            Assert.AreEqual(new ExitIdentity("episode_a", "exit_a"), restored.Exits[0]);
        }

        [Test]
        public void ProgramCompiler_WhenPublishedIdentityRemoved_ReportsLocatedWarnings()
        {
            var asset = CreateCompilerAsset();
            var first = CompileCurrent(asset, out var firstReport);
            AssertNoErrors(firstReport.Issues);
            var current = IdentityManifest.Create(first);
            var episodes = current.EpisodeIds.Concat(new[] { "removed_episode" }).ToArray();
            var edges = current.EdgeIds.Concat(new[] { "removed_edge" }).ToArray();
            var exits = current.Exits.Concat(new[] { new ExitIdentity("removed_episode", "removed_exit") }).ToArray();
            asset.CommitPublishedIdentity(new IdentityManifest(
                current.StoryId,
                current.Version,
                episodes,
                edges,
                exits));

            var compiled = CompileCurrent(asset, out var report);
            var issues = FormatIssues(report.Issues);

            Assert.IsNotNull(compiled);
            StringAssert.Contains("identity/episode:removed_episode", issues);
            StringAssert.Contains("identity/edge:removed_edge", issues);
            StringAssert.Contains("identity/episode:removed_episode/exit:removed_exit", issues);
        }

        [Test]
        public void PublishedIdentityExport_WhenBreakingChangesRejectedAndConfirmed_PreservesThenCommitsOutputs()
        {
            const string authoringPath = "Assets/Bundles/Story/__PublishedIdentityExportTest.asset";
            const string programPath = "Assets/Bundles/Story/__PublishedIdentityExportTest.runtime.asset";
            const string manifestPath = "Assets/Bundles/Story/__PublishedIdentityExportTest.runtime.identity-manifest.json";
            const string reportPath = "Assets/Bundles/Story/__PublishedIdentityExportTest.runtime.identity-change.json";
            var paths = new[] { authoringPath, programPath, manifestPath, reportPath };
            for (var i = 0; i < paths.Length; i++)
            {
                AssetDatabase.DeleteAsset(paths[i]);
                m_CreatedAssetPaths.Add(paths[i]);
            }

            EnsureFolder(Path.GetDirectoryName(authoringPath)?.Replace('\\', '/'));
            var asset = CreateAsset();
            asset.StoryId = "identity_story";
            asset.RuntimeProgramAssetPath = programPath;
            AssetDatabase.CreateAsset(asset, authoringPath);
            m_CreatedObjects.Remove(asset);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ProgramAsset>(), programPath);
            AssetDatabase.SaveAssets();

            var publishedProgram = CreatePublishedIdentityProgram(true);
            var first = ProgramAssetExporter.ExportCompiled(
                asset,
                publishedProgram,
                _ => throw new AssertionException("First publish must not request removal confirmation."));
            Assert.IsTrue(first.Exported);
            var originalManifestJson = System.IO.File.ReadAllText(manifestPath);
            var originalReportJson = System.IO.File.ReadAllText(reportPath);

            var currentProgram = CreatePublishedIdentityProgram(false);
            var confirmationCalled = false;
            var rejected = ProgramAssetExporter.ExportCompiled(
                asset,
                currentProgram,
                changes =>
                {
                    confirmationCalled = true;
                    Assert.IsTrue(changes.HasBreakingChanges);
                    return false;
                });

            Assert.IsTrue(confirmationCalled);
            Assert.IsTrue(rejected.Canceled);
            Assert.AreEqual(originalManifestJson, System.IO.File.ReadAllText(manifestPath));
            Assert.AreEqual(originalReportJson, System.IO.File.ReadAllText(reportPath));
            Assert.AreEqual(2, AssetDatabase.LoadAssetAtPath<ProgramAsset>(programPath).ToProgram().Volumes[0].Episodes.Count);
            Assert.IsTrue(asset.TryGetPublishedIdentity(out var rejectedBaseline, out var rejectedError), rejectedError);
            Assert.AreEqual(2, rejectedBaseline.EpisodeIds.Count);

            var accepted = ProgramAssetExporter.ExportCompiled(asset, currentProgram, _ => true);

            Assert.IsTrue(accepted.Exported);
            Assert.AreEqual(1, AssetDatabase.LoadAssetAtPath<ProgramAsset>(programPath).ToProgram().Volumes[0].Episodes.Count);
            Assert.IsTrue(asset.TryGetPublishedIdentity(out var acceptedBaseline, out var acceptedError), acceptedError);
            Assert.AreEqual(1, acceptedBaseline.EpisodeIds.Count);
            StringAssert.Contains("episode_b", System.IO.File.ReadAllText(reportPath));
        }

        [Test]
        public void StoryEditorBlackboard_WhenBuilt_ShowsCurrentEpisodeWithoutPlaybackControls()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);

            var blackboard = window.rootVisualElement.Q(className: "editor-node-graph__blackboard");
            var buttons = blackboard.Query<Button>().ToList().Select(x => x.text).ToList();
            var labels = FindVisualChildren<Label>(blackboard).Select(x => x.text).ToList();

            Assert.AreNotEqual(DisplayStyle.None, blackboard.resolvedStyle.display);
            Assert.AreEqual(0, buttons.Count);
            Assert.IsTrue(labels.Any(x => x.Contains("当前章节")), string.Join("|", labels));
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
            var video = asset.Episodes[0].Nodes.First(x => x.NodeId == "video");

            InvokePrivate(window, "SetNodeFieldFromGraph", "video", "clip", SampleGraphFixture.IntroVideoPath);

            Assert.AreEqual(SampleGraphFixture.IntroVideoPath, video.Parameters.First(x => x.Key == "clip").Value);
        }

        [Test]
        public void StoryEditorGraph_WhenTemplatesBuilt_DoesNotOfferLegacyJumpNode()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var adapter = GetPrivateField<IEditorNodeGraphAdapter>(window, "m_GraphAdapter");

            Assert.IsFalse(adapter.Templates.Any(x =>
                string.Equals(x.TemplateId, "2", StringComparison.Ordinal) ||
                x.DisplayName.Contains("跳转")));
            Assert.IsTrue(adapter.Templates.Any(x => x.TemplateId == NodeKind.End.ToString()));
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
        public void StoryEditorDiagnostics_WhenCompilerIssuesProjected_UsesChineseTooltips()
        {
            var asset = CreateSemanticGraphAsset();
            var episode = asset.Episodes[0];
            var report = new ValidationReport();
            report.AddError(
                $"story:{asset.StoryId}/volume:volume/route",
                "Volume route requires at least one Episode.");
            report.AddError(
                $"story:{asset.StoryId}/volume:volume/layout:layout/episode[0]",
                "Route layout references an unknown Episode. episode:missing");

            var diagnostics = GameDeveloperKit.StoryEditor.Graph.Diagnostics.FromReport(
                report,
                asset,
                episode,
                false);

            Assert.IsTrue(diagnostics.Items.Any(x => x.GraphDiagnostic.Message == "卷路线至少需要包含一个剧情段。"));
            Assert.IsTrue(diagnostics.Items.Any(x => x.GraphDiagnostic.Message == "路线布局引用了不存在的剧情段。"));
        }

        [Test]
        public void StoryEditorGraph_WhenNumberFieldInvalid_ShowsFieldDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Episodes[0].Nodes.Add(CreateNode("wait_invalid", "等待", NodeKind.Wait, ("duration", "fast")));
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
            var video = asset.Episodes[0].Nodes.First(x => x.NodeId == "video");
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
            var video = asset.Episodes[0].Nodes.First(x => x.NodeId == "video");
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
            var video = asset.Episodes[0].Nodes.First(x => x.NodeId == "video");
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
        public void StoryEditorGraph_WhenChoiceIsTerminal_ShowsNoSelectedPortDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);

            var choiceNode = FindStoryEditorNodeView(window, "choice");
            var summaryText = GetIssueSummaryText(window);

            Assert.IsFalse(choiceNode.Query<VisualElement>(className: "editor-node-graph-node__port-dot").ToList()
                .Any(x => x.userData is EditorGraphPortRef port && port.PortId == "selected"));
            Assert.IsFalse(choiceNode.ClassListContains("editor-node-graph-node--diagnostic-error"));
            Assert.IsFalse(summaryText.Contains("选项必须且只能连接一个"));
        }

        [Test]
        public void StoryEditorGraph_WhenLineMixesChoiceAndDirectTargets_ShowsCompletedPortDiagnostic()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Episodes[0].Edges.Add(CreateEdge("line_intro", "completed", "完成", "mini_game", "edge_line_intro_direct"));
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
            asset.Episodes[0].Edges.Add(CreateEdge("video", "unknown", "未知", "line_intro", "edge_video_unknown"));
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
            asset.Episodes[0].Edges.Add(CreateEdge("video", "completed", "完成", "missing", "edge_video_missing"));
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
            var asset = CreateParallelCompilerAsset();
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
        public void StoryEditorGraph_WhenParallelCompletedEdgeExists_ShowsStalePortAndCompilerRejectsIt()
        {
            var asset = CreateParallelCompilerAsset();
            asset.Episodes[0].Edges.Add(CreateEdge("parallel", "completed", "完成", "end", "edge_stale_parallel_completed"));
            var window = CreateStoryEditorWindow(asset);

            var diagnostic = GetGraphDiagnosticItems(window).First(x =>
                x.GraphDiagnostic.NodeId == "parallel" &&
                x.GraphDiagnostic.PortId == "completed");
            StringAssert.Contains("端口“completed”不是该节点的输出端口", diagnostic.GraphDiagnostic.Message);

            var program = CompileCurrent(asset, out var report);
            Assert.IsNull(program);
            StringAssert.Contains("Parallel output must use a branch port.", FormatIssues(report.Issues));
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
            asset.Episodes[0].Edges.Add(CreateEdge("video", "unknown", "未知", "line_intro", "edge_video_unknown"));
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
            var episode = asset.Episodes[0];

            InvokePrivate(window, "SetNodeFieldFromGraph", "video", MediaCommandNames.VideoSourceArgument, MediaCommandNames.VideoSourceStreamingAssets);
            InvokePrivate(window, "SetNodeFieldFromGraph", "video", "clip", SampleGraphFixture.IntroVideoPath);
            var command = InvokePrivate<AuthoringNode>(
                window,
                "AddNodeAt",
                new Vector2(360f, 140f),
                NodeKind.PlayAudio,
                FindNode(asset, "line_intro"),
                "completed",
                "完成");

            Assert.IsNotNull(command);
            Assert.AreEqual(NodeKind.PlayAudio, command.NodeKind);
            Assert.IsTrue(episode.Edges.Any(x =>
                string.Equals(x.FromNodeId, "line_intro", StringComparison.Ordinal) &&
                string.Equals(x.TargetNodeId, command.NodeId, StringComparison.Ordinal) &&
                string.Equals(x.FromPortLabel, "完成", StringComparison.Ordinal)));
            Assert.IsFalse(episode.Edges.Any(x => x.FromPortLabel.StartsWith("next_", StringComparison.Ordinal)), string.Join(",", episode.Edges.Select(x => x.FromPortLabel)));
        }

        [Test]
        public void StoryEditorGraph_WhenEpisodeOpened_KeepsStartFixedAndAllowsEndAuthoring()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var episode = asset.Episodes[0];
            var originalCount = episode.Nodes.Count;
            var end = episode.Nodes.First(x => x.NodeKind == NodeKind.End);

            var deniedStart = InvokePrivate<AuthoringNode>(window, "AddNodeAt", Vector2.zero, NodeKind.Start, null, null, null);
            var addedEnd = InvokePrivate<AuthoringNode>(window, "AddNodeAt", Vector2.zero, NodeKind.End, null, null, null);
            InvokePrivate(window, "SelectNode", end);
            InvokePrivate(window, "RemoveSelection");

            Assert.AreEqual(1, episode.Nodes.Count(x => x.NodeKind == NodeKind.Start));
            Assert.AreEqual(1, episode.Nodes.Count(x => x.NodeKind == NodeKind.End));
            Assert.AreEqual(episode.Nodes.First(x => x.NodeKind == NodeKind.Start).NodeId, episode.EntryNodeId);
            Assert.AreEqual(originalCount, episode.Nodes.Count);
            Assert.IsNull(deniedStart);
            Assert.IsNotNull(addedEnd);
            Assert.IsFalse(episode.Nodes.Contains(end));
            Assert.IsTrue(episode.Nodes.Contains(addedEnd));
        }

        [Test]
        public void StoryEditorGraph_WhenMultipleNodesSelectedFromGraph_DeletesSelectedNodes()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var episode = asset.Episodes[0];

            InvokePrivate(window, "SelectNodesFromGraph", (object)new[] { "video", "choice" });
            InvokePrivate(window, "DeleteSelectionFromGraph");

            Assert.IsFalse(episode.Nodes.Any(x => x.NodeId == "video"));
            Assert.IsFalse(episode.Nodes.Any(x => x.NodeId == "choice"));
            Assert.IsFalse(episode.Edges.Any(x =>
                string.Equals(x.FromNodeId, "video", StringComparison.Ordinal) ||
                string.Equals(x.TargetNodeId, "video", StringComparison.Ordinal) ||
                string.Equals(x.FromNodeId, "choice", StringComparison.Ordinal) ||
                string.Equals(x.TargetNodeId, "choice", StringComparison.Ordinal)));
        }


        [Test]
        public void StoryEditorGraphAdapter_WhenPortsAreInvalid_ReturnsChineseReason()
        {
            var asset = CreateSemanticGraphAsset();
            asset.Episodes[0].Nodes.Add(CreateNode("choice_extra", "备用选项", NodeKind.Choice));
            asset.Episodes[0].Nodes.Add(CreateNode("wait", "等待", NodeKind.Wait, ("duration", "1")));
            var window = CreateStoryEditorWindow(asset);
            var adapter = GetPrivateField<IEditorNodeGraphAdapter>(window, "m_GraphAdapter");

            var videoToChoice = adapter.CanConnect(new EditorGraphPortRef("video", "completed"), new EditorGraphPortRef("choice_extra", "in"));
            var lineToChoice = adapter.CanConnect(new EditorGraphPortRef("line_intro", "completed"), new EditorGraphPortRef("choice_extra", "in"));
            var waitToChoice = adapter.CanConnect(new EditorGraphPortRef("wait", "completed"), new EditorGraphPortRef("choice_extra", "in"));
            var choiceToEnd = adapter.CanConnect(new EditorGraphPortRef("choice", "selected"), new EditorGraphPortRef("end", "in"));
            var choiceUnknown = adapter.CanConnect(new EditorGraphPortRef("choice", "help"), new EditorGraphPortRef("mini_game", "in"));
            var endOutput = adapter.CanConnect(new EditorGraphPortRef("end", "completed"), new EditorGraphPortRef("mini_game", "in"));

            Assert.IsFalse(videoToChoice.Allowed);
            StringAssert.Contains("选项节点只能接在对白、旁白或等待的完成端口后", videoToChoice.Message);
            Assert.IsTrue(lineToChoice.Allowed, lineToChoice.Message);
            Assert.IsTrue(waitToChoice.Allowed, waitToChoice.Message);
            Assert.IsFalse(choiceToEnd.Allowed);
            StringAssert.Contains("Episode 出口", choiceToEnd.Message);
            Assert.IsFalse(choiceUnknown.Allowed);
            StringAssert.Contains("Episode 出口", choiceUnknown.Message);
            Assert.IsFalse(endOutput.Allowed);
            StringAssert.Contains("结束节点没有输出端口", endOutput.Message);
        }

        [Test]
        public void StoryEditorGraph_WhenLineSwitchesBetweenChoiceAndDirectMode_ReplacesCompletedEdges()
        {
            var asset = CreateSemanticGraphAsset();
            var window = CreateStoryEditorWindow(asset);
            var episode = asset.Episodes[0];
            episode.Edges.RemoveAll(x => x.FromNodeId == "line_intro" && x.FromPortId == "completed");
            episode.Edges.Add(CreateEdge("line_intro", "completed", "完成", "mini_game", "edge_line_intro_direct"));

            InvokePrivate(
                window,
                "ConnectFromGraph",
                new EditorGraphPortRef("line_intro", "completed"),
                new EditorGraphPortRef("choice", "in"));
            var choiceModeEdges = episode.Edges
                .Where(x => x.FromNodeId == "line_intro" && x.FromPortId == "completed")
                .ToList();

            Assert.AreEqual(1, choiceModeEdges.Count);
            Assert.AreEqual("choice", choiceModeEdges[0].TargetNodeId);

            InvokePrivate(
                window,
                "ConnectFromGraph",
                new EditorGraphPortRef("line_intro", "completed"),
                new EditorGraphPortRef("mini_game", "in"));
            var afterDirect = episode.Edges
                .Where(x => x.FromNodeId == "line_intro" && x.FromPortId == "completed")
                .ToList();

            Assert.AreEqual(1, afterDirect.Count);
            Assert.AreEqual("mini_game", afterDirect[0].TargetNodeId);
        }

        [Test]
        public void StoryRuntime_WhenScanned_DoesNotReferenceEditorOrConcreteMediaTypes()
        {
            var files = Directory.GetFiles(FrameworkFilePath("Runtime/Story"), "*.cs", SearchOption.AllDirectories);
            var source = string.Join(Environment.NewLine, files.Select(System.IO.File.ReadAllText));

            Assert.IsFalse(source.Contains("EditorNodeGraph"), "Story runtime must not reference editor graph kit.");
            Assert.IsFalse(source.Contains("UnityEditor"), "Story runtime must not reference UnityEditor.");
            Assert.IsFalse(source.Contains("AssetDatabase"), "Story runtime must not reference AssetDatabase.");
            Assert.IsFalse(source.Contains("ObjectField"), "Story runtime must not reference UI Toolkit ObjectField.");
            Assert.IsFalse(source.Contains("UIElements"), "Story runtime must not reference UI Toolkit.");
            Assert.IsFalse(source.Contains("VideoClip"), "Story runtime must not reference concrete video clip types.");
        }

        private AuthoringAsset CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            m_CreatedObjects.Add(asset);
            return asset;
        }

        private static Program CompileCurrent(AuthoringAsset asset, out ValidationReport report)
        {
            PrepareCurrentRoutes(asset);
            return ProgramCompiler.Compile(asset, out report);
        }

        private static void PrepareCurrentRoutes(AuthoringAsset asset)
        {
            asset.EnsureDefaults();
            for (var volumeIndex = 0; volumeIndex < asset.Volumes.Count; volumeIndex++)
            {
                var volume = asset.Volumes[volumeIndex];
                if (volume == null || volume.Route != null)
                {
                    continue;
                }

                volume.Route = new AuthoringRoute();
                for (var episodeIndex = 0; episodeIndex < volume.Episodes.Count; episodeIndex++)
                {
                    var episode = volume.Episodes[episodeIndex];
                    if (episode == null || string.IsNullOrWhiteSpace(episode.EpisodeId))
                    {
                        continue;
                    }

                    volume.Route.Edges.Add(new AuthoringRouteEdge
                    {
                        EdgeId = $"test_root_{volumeIndex}_{episodeIndex}",
                        SourceKind = RouteEdgeSourceKind.Root,
                        ToEpisodeId = episode.EpisodeId
                    });
                }
            }
        }

        private static Program CreatePublishedIdentityProgram(bool includeSecondEpisode)
        {
            var firstExitId = includeSecondEpisode ? "to_b" : "done";
            var first = CreatePublishedIdentityEpisode("episode_a", firstExitId);
            var episodes = includeSecondEpisode
                ? new[] { first, CreatePublishedIdentityEpisode("episode_b", "done") }
                : new[] { first };
            var edges = includeSecondEpisode
                ? new[]
                {
                    RouteEdge.FromRoot(IdentityId.RootEdge("episode_a"), "episode_a"),
                    RouteEdge.FromExit(
                        IdentityId.ExitEdge("episode_a", "to_b"),
                        "episode_a",
                        "to_b",
                        "episode_b")
                }
                : new[] { RouteEdge.FromRoot(IdentityId.RootEdge("episode_a"), "episode_a") };
            return new Program(
                "identity_story",
                includeSecondEpisode ? "1" : "2",
                new[] { new Volume("volume", "Volume", episodes, new Route(edges)) });
        }

        private static Episode CreatePublishedIdentityEpisode(string episodeId, string exitId)
        {
            return new Episode(
                episodeId,
                episodeId,
                "start",
                new[] { new EpisodeExit(exitId) },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("end"))),
                    new Step("end", StepKind.End, new StepData(exitId: exitId))
                });
        }

        private AuthoringAsset CreateCompilerAsset(
            string helpTargetNodeId = "video",
            string videoSource = MediaCommandNames.VideoSourceStreamingAssets,
            string videoClip = SampleGraphFixture.IntroVideoPath,
            string videoLoop = "true",
            bool includeChoiceHelpSelected = false,
            bool addExtraChoiceHelpSelected = false,
            bool choiceHelpHasTextKey = true)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";

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

            var episode = CreateEpisode(
                "episode_01",
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
                });

            if (includeChoiceHelpSelected)
            {
                episode.Edges.Add(CreateEdge("choice_help", "selected", "选择后", helpTargetNodeId, "edge_help_selected"));
            }

            if (addExtraChoiceHelpSelected)
            {
                episode.Edges.Add(CreateStoryEndEdge("choice_help", "selected", "选择后", "edge_help_selected_extra"));
            }

            asset.Episodes.Add(episode);
            return asset;
        }

        private AuthoringAsset CreateParallelCompilerAsset(
            bool nestedParallel = false,
            bool choiceInsideParallel = false)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";

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
                CreateNode("end", "结束", NodeKind.End),
            };

            var edges = new List<AuthoringEdge>
            {
                CreateEdge("start", "completed", "完成", "parallel"),
                CreateEdge("parallel", "branch_video", "视频轨", "video"),
                CreateEdge("parallel", "branch_dialogue", "对白轨", nestedParallel ? "nested_parallel" : "narration"),
            };

            if (choiceInsideParallel)
            {
                edges.Add(CreateEdge("narration", "completed", "选择", "choice"));
            }

            if (nestedParallel)
            {
                nodes.Add(CreateNode("nested_parallel", "嵌套并行", NodeKind.Parallel));
                nodes.Add(CreateNode("nested_line", "嵌套旁白", NodeKind.Narration, ("textKey", "nested.line")));
                nodes.Add(CreateNode("nested_line_b", "嵌套旁白 B", NodeKind.Narration, ("textKey", "nested.line.b")));
                edges.Add(CreateEdge("nested_parallel", "branch_a", "分支 A", "nested_line"));
                edges.Add(CreateEdge("nested_parallel", "branch_b", "分支 B", "nested_line_b"));
            }

            asset.Episodes.Add(CreateEpisode("episode_01", "第一章", "start", nodes, edges));
            return asset;
        }

        private AuthoringAsset CreateParallelWaitChoiceAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_parallel_wait_choice";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";

            asset.Episodes.Add(CreateEpisode(
                "episode_01",
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
                    CreateEdge("after_choice", "completed", "完成", "end"),
                }));

            return asset;
        }

        private AuthoringAsset CreateWaitChoiceCompilerAsset(bool mixDirectTarget = false)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_wait_choice";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";

            var edges = new List<AuthoringEdge>
            {
                CreateEdge("start", "completed", "完成", "wait_choice"),
                CreateEdge("wait_choice", "completed", "选项 A", "choice_a"),
                CreateEdge("wait_choice", "completed", "选项 B", "choice_b"),
                CreateEdge("after_a", "completed", "完成", "end"),
                CreateEdge("after_b", "completed", "完成", "end"),
            };

            if (mixDirectTarget)
            {
                edges.Add(CreateEdge("wait_choice", "completed", "完成", "end", "edge_wait_direct"));
            }

            asset.Episodes.Add(CreateEpisode(
                "episode_01",
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

        private AuthoringAsset CreateTransitionVideoAsset(
            bool videoTargetChoice = false,
            bool videoLoop = false,
            bool allowSeek = false)
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";

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
                    ("loop", videoLoop ? "true" : "false"),
                    ("allowSeek", allowSeek ? "true" : "false")),
                CreateNode("line", "过渡后旁白", NodeKind.Narration, ("textKey", "story.after.video")),
                CreateNode("choice", "视频后选择", NodeKind.Choice, ("textKey", "choice.after.video")),
                CreateNode("end", "结束", NodeKind.End),
            };

            var edges = new List<AuthoringEdge>
            {
                CreateEdge("start", "completed", "完成", "video"),
                CreateEdge("video", "completed", "完成", videoTargetChoice ? "choice" : "line"),
                CreateEdge("line", "completed", "完成", "end"),
            };

            asset.Episodes.Add(CreateEpisode("episode_01", "第一章", "start", nodes, edges));
            return asset;
        }

        private AuthoringAsset CreatePreParallelVideoAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_pre_parallel_video";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";

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
                    ("loop", "false"),
                    ("allowSeek", "true")),
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
                CreateEdge("after_choice", "completed", "完成", "end"),
            };

            asset.Episodes.Add(CreateEpisode("episode_01", "第一章", "start", nodes, edges));
            return asset;
        }

        private AuthoringAsset CreateParallelChoiceTargetAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";

            asset.Episodes.Add(CreateEpisode(
                "episode_01",
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
                }));
            return asset;
        }

        private AuthoringAsset CreateParallelChoiceTargetParallelAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";

            asset.Episodes.Add(CreateEpisode(
                "episode_01",
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
                    CreateEdge("after_choice_parallel", "branch_audio", "音频轨", "after_audio"),
                    CreateEdge("after_choice_parallel", "branch_video", "视频轨", "after_video"),
                    CreateEdge("after_line", "completed", "完成", "end"),
                }));
            return asset;
        }

        private AuthoringAsset CreateThreeBranchParallelAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "compiler_story";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";

            asset.Episodes.Add(CreateEpisode(
                "episode_01",
                "第一章",
                "parallel",
                new[]
                {
                    CreateNode("parallel", "并行", NodeKind.Parallel),
                    CreateNode("image", "显示图片", NodeKind.ShowImage, ("image", SampleGraphFixture.MapImagePath)),
                    CreateNode("audio", "播放音频", NodeKind.PlayAudio, ("clip", SampleGraphFixture.StationAudioPath)),
                    CreateNode("line", "旁白", NodeKind.Narration, ("textKey", "三轨旁白")),
                    CreateNode("choice", "继续", NodeKind.Choice, ("textKey", "继续")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("parallel", "branch_image", "图片轨", "image"),
                    CreateEdge("parallel", "branch_audio", "音频轨", "audio"),
                    CreateEdge("parallel", "branch_text", "文本轨", "line"),
                    CreateEdge("line", "completed", "进入选择", "choice"),
                }));
            return asset;
        }

        private AuthoringAsset CreateSemanticGraphAsset()
        {
            var asset = CreateAsset();
            asset.StoryId = "story";
            asset.Version = "1";
            asset.LegacyEntryEpisodeId = "episode_01";

            var episode = CreateEpisode(
                "episode_01",
                "第一章",
                "start",
                new[]
                {
                    CreateNode("start", "开始", NodeKind.Start),
                    CreateNode("video", "播放开场视频", NodeKind.PlayVideo),
                    CreateNode("line_intro", "开场对白", NodeKind.Dialogue, ("textKey", "story.intro.line")),
                    CreateNode("choice", "救人", NodeKind.Choice, ("textKey", "choice.help")),
                    CreateNode(
                        "mini_game",
                        "小游戏：撬锁",
                        NodeKind.Logic,
                        (LogicCommandCodec.LogicIdParameter, "sample.minigame.lockpick")),
                    CreateNode("end", "结束", NodeKind.End),
                },
                new[]
                {
                    CreateEdge("start", "completed", "完成", "video", "edge_start_completed"),
                    CreateEdge("video", "completed", "完成", "line_intro", "edge_video_completed"),
                    CreateEdge("line_intro", "completed", "完成", "choice", "edge_line_intro_completed"),
                    CreateStoryEndEdge("mini_game", "success", "成功", "edge_mini_success"),
                });

            var target = CreateEpisode(
                "episode_02",
                "第二章",
                "target",
                new[]
                {
                    CreateNode("target", "结束", NodeKind.End),
                },
                Array.Empty<AuthoringEdge>());

            asset.Episodes.Add(episode);
            asset.Episodes.Add(target);
            AddLayout(asset, "episode_01", "video", 180f, 120f);
            return asset;
        }

        private static AuthoringEpisode CreateEpisode(
            string episodeId,
            string title,
            string entryNodeId,
            IReadOnlyList<AuthoringNode> nodes,
            IReadOnlyList<AuthoringEdge> edges)
        {
            var episode = new AuthoringEpisode
            {
                EpisodeId = episodeId,
                Title = title,
                EntryNodeId = entryNodeId
            };
            episode.Nodes.AddRange(nodes);
            episode.Edges.AddRange(edges);
            return episode;
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
            return asset.Episodes
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

        private static void AddLayout(AuthoringAsset asset, string episodeId, string nodeId, float x, float y)
        {
            asset.FindEpisode(episodeId).DetailLayout.Nodes.Add(new EpisodeNodePlacement
            {
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
            InvokePrivate(window, "EnterEpisodeDetail", GetPrivateField<AuthoringEpisode>(window, "m_SelectedEpisode"));
            return window;
        }

        private static Step FindStep(Program program, string episodeId, string stepId)
        {
            var episode = program.Volumes.SelectMany(x => x.Episodes).First(x => string.Equals(x.EpisodeId, episodeId, StringComparison.Ordinal));
            return episode.Steps.First(x => string.Equals(x.StepId, stepId, StringComparison.Ordinal));
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

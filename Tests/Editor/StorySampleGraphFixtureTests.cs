using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story;
using GameDeveloperKit.StoryEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Event;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Playback;
using GameDeveloperKit.StoryEditor.Validation;
using GameDeveloperKit.StoryEditor.UI;

namespace GameDeveloperKit.Tests
{
    public sealed class StorySampleGraphFixtureTests
    {
        private readonly List<UnityEngine.Object> m_CreatedObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < m_CreatedObjects.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(m_CreatedObjects[i]);
            }

            m_CreatedObjects.Clear();
        }

        [Test]
        public void SampleFixture_WhenBuilt_HasCanonicalStoryTreeAndNoValidationErrors()
        {
            var asset = CreateFixtureAsset();

            var report = AuthoringValidator.Validate(asset);

            AssertNoErrors(report.Issues);
            Assert.AreEqual(SampleGraphFixture.StoryId, asset.StoryId);
            Assert.AreEqual(SampleGraphFixture.Version, asset.Version);
            Assert.AreEqual(SampleGraphFixture.RootEpisodeId, asset.SelectedVolume.Route.Edges.Single(x =>
                x.SourceKind == RouteEdgeSourceKind.Root).ToEpisodeId);
            CollectionAssert.AreEqual(SampleGraphFixture.EpisodeIds, asset.Episodes.Select(x => x.EpisodeId).ToArray());

            for (var i = 0; i < asset.Episodes.Count; i++)
            {
                var episode = asset.Episodes[i];
                Assert.AreEqual(1, episode.Nodes.Count(x => x.NodeKind == NodeKind.Start), episode.EpisodeId);
                Assert.IsTrue(
                    episode.Nodes.Any(x => x.NodeKind == NodeKind.End) || episode.Nodes.Any(x => x.NodeKind == NodeKind.Choice),
                    episode.EpisodeId);
                Assert.GreaterOrEqual(episode.Nodes.Count, 6, episode.EpisodeId);
                Assert.IsTrue(episode.Nodes.Any(x => string.Equals(x.NodeId, episode.EntryNodeId, StringComparison.Ordinal)), episode.EpisodeId);
            }
        }

        [Test]
        public void SampleFixture_WhenInspected_CoversChoiceCommandAndLayoutContracts()
        {
            var asset = CreateFixtureAsset();
            var arrival = SampleGraphFixture.FindEpisode(asset, "episode_arrival");
            var station = SampleGraphFixture.FindEpisode(asset, "episode_station");
            var alley = SampleGraphFixture.FindEpisode(asset, "episode_alley");

            var parallelEdges = arrival.Edges
                .Where(x => string.Equals(x.FromNodeId, "arrival_parallel", StringComparison.Ordinal))
                .ToList();
            var mergeChoiceEdge = SampleGraphFixture.FindEdge(arrival, "edge_arrival_merge_alley_choice");
            var video = SampleGraphFixture.FindNode(arrival, "arrival_video");
            var arrivalAudio = SampleGraphFixture.FindNode(arrival, "arrival_audio");
            var audio = SampleGraphFixture.FindNode(station, "station_audio");
            var miniGame = SampleGraphFixture.FindNode(alley, "alley_minigame");
            var alleyVideo = SampleGraphFixture.FindNode(alley, "alley_video");
            var route = asset.SelectedVolume.Route;

            Assert.IsTrue(asset.Episodes.SelectMany(x => x.Nodes).All(x => NodeSchemaRegistry.IsDefaultAuthoringNode(x.NodeKind)), string.Join(",", asset.Episodes.SelectMany(x => x.Nodes).Select(x => x.NodeKind).Distinct()));
            Assert.AreEqual(3, parallelEdges.Count);
            Assert.IsTrue(parallelEdges.Any(x => x.FromPortId == "branch_video" && x.TargetNodeId == "arrival_video"));
            Assert.IsTrue(parallelEdges.Any(x => x.FromPortId == "branch_audio" && x.TargetNodeId == "arrival_audio"));
            Assert.IsTrue(parallelEdges.Any(x => x.FromPortId == "branch_dialogue" && x.TargetNodeId == "arrival_guard_line"));
            Assert.AreEqual("completed", mergeChoiceEdge.FromPortId);
            Assert.AreEqual("choice_enter_alley", mergeChoiceEdge.TargetNodeId);
            Assert.IsFalse(arrival.Edges.Any(x => x.FromNodeId == "choice_enter_alley" || x.FromNodeId == "choice_help_guard"));
            Assert.IsTrue(route.Edges.Any(x => x.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                x.FromEpisodeId == "episode_arrival" && x.FromExitId == "choice_enter_alley" && x.ToEpisodeId == "episode_alley"));
            Assert.IsTrue(route.Edges.Any(x => x.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                x.FromEpisodeId == "episode_arrival" && x.FromExitId == "choice_help_guard" && x.ToEpisodeId == "episode_station"));
            AssertParameter(video, MediaCommandNames.VideoSourceArgument, SampleGraphFixture.VideoSource);
            AssertParameter(video, "clip", SampleGraphFixture.IntroVideoPath);
            AssertParameter(video, "wait", "true");
            AssertParameter(arrivalAudio, "clip", SampleGraphFixture.StationAudioPath);
            AssertParameter(audio, "clip", SampleGraphFixture.StationAudioPath);
            AssertParameter(miniGame, EventCommandCodec.EventIdParameter, "sample.minigame.lockpick");
            AssertParameter(alleyVideo, MediaCommandNames.VideoSourceArgument, SampleGraphFixture.VideoSource);
            AssertParameter(alleyVideo, "clip", SampleGraphFixture.AlleyVideoPath);
            Assert.IsTrue(asset.Episodes.All(episode => episode.DetailLayout.Nodes.All(x =>
                episode.Nodes.Any(node => string.Equals(node.NodeId, x.NodeId, StringComparison.Ordinal)))));
            Assert.AreEqual(asset.Episodes.Sum(x => x.Nodes.Count), asset.Episodes.Sum(x => x.DetailLayout.Nodes.Count));
        }

        [Test]
        public void SampleFixture_WhenCompiled_BuildsProgramAndRuntimeSmokePath()
        {
            var asset = CreateFixtureAsset();

            var program = ProgramCompiler.Compile(asset, out var report);

            AssertNoErrors(report.Issues);
            Assert.IsNotNull(program);
            Assert.AreEqual(SampleGraphFixture.StoryId, program.StoryId);
            Assert.AreEqual(SampleGraphFixture.RootEpisodeId, program.Volumes[0].Route.Edges[0].ToEpisodeId);
            Assert.IsTrue(program.Volumes.SelectMany(x => x.Episodes).Any(x => x.EpisodeId == "episode_arrival"));
            Assert.IsTrue(program.Volumes.SelectMany(x => x.Episodes).Any(x => x.EpisodeId == "episode_alley"));
            Assert.AreEqual(StepKind.Choice, FindStep(program, "episode_arrival", "arrival_merge_choices").Kind);
            Assert.AreEqual(StepKind.Parallel, FindStep(program, "episode_arrival", "arrival_parallel").Kind);
            Assert.AreEqual(StepKind.Merge, FindStep(program, "episode_arrival", "arrival_merge").Kind);
            Assert.AreEqual(StepKind.Command, FindStep(program, "episode_arrival", "arrival_video").Kind);
            Assert.AreEqual(StepKind.Command, FindStep(program, "episode_arrival", "arrival_audio").Kind);
            Assert.AreEqual(5, program.Volumes[0].Route.Edges.Count);
            var compiledIntroVideo = FindStep(program, "episode_arrival", "arrival_video").Data.Command;
            Assert.AreEqual(SampleGraphFixture.VideoSource, compiledIntroVideo.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
            Assert.AreEqual("videos/0.mp4", compiledIntroVideo.Arguments.GetString("clip"));

            var module = new StoryModule();
            module.Startup();
            try
            {
                module.Register(program);
                var runner = module.StartProgram(SampleGraphFixture.StoryId);
                var frame = runner.CurrentFrame;

                AssertTrackFrame(frame, FrameTrackKind.Text, "episode_arrival", "arrival_intro");

                frame = module.Continue();
                var introVideo = AssertParallelArrivalFrame(frame);
                Assert.AreEqual(SampleGraphFixture.VideoSource, introVideo.Command.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
                Assert.AreEqual("videos/0.mp4", introVideo.Command.Arguments.GetString("clip"));

                frame = module.Continue();
                AssertParallelArrivalMediaFrame(frame, 2);

                frame = module.CompleteCommand("arrival_audio", "completed");
                AssertParallelArrivalMediaFrame(frame, 1);

                frame = module.CompleteCommand("arrival_video", "completed");
                AssertChoiceFrame(frame, "episode_arrival", "arrival_merge_choices");

                frame = module.Select("choice_enter_alley");
                AssertTrackFrame(frame, FrameTrackKind.Text, "episode_alley", "alley_line");

                frame = module.Continue();
                AssertTrackFrame(frame, FrameTrackKind.Command, "episode_alley", "alley_minigame");

                frame = module.CompleteCommand("alley_minigame", "success");
                AssertTrackFrame(frame, FrameTrackKind.Command, "episode_alley", "alley_door_audio");

                frame = module.CompleteCommand("alley_door_audio", "completed");
                var alleyVideoCommand = AssertTrackFrame(frame, FrameTrackKind.Command, "episode_alley", "alley_video");
                Assert.AreEqual(SampleGraphFixture.VideoSource, alleyVideoCommand.Command.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
                Assert.AreEqual("videos/4.mp4", alleyVideoCommand.Command.Arguments.GetString("clip"));

                frame = module.CompleteCommand("alley_video", "completed");
                Assert.IsTrue(frame.IsCompleted);
                Assert.AreEqual("episode_alley", frame.Episode.EpisodeId);
            }
            finally
            {
                module.Shutdown();
            }
        }

        [Test]
        public void SampleFixture_WhenPlayedThroughPlaybackSession_UsesRuntimeModuleAndRecordsHistory()
        {
            var asset = CreateFixtureAsset();
            using (var session = new Session(asset, SampleGraphFixture.RootEpisodeId))
            {
                Assert.IsTrue(session.Start(), session.ErrorMessage);
                AssertNoErrors(session.Report.Issues);
                Assert.IsNotNull(session.Program);
                Assert.AreEqual(SampleGraphFixture.StoryId, session.Program.StoryId);
                AssertTrackFrame(session.CurrentFrame, FrameTrackKind.Text, "episode_arrival", "arrival_intro");

                session.Continue();
                var introVideo = AssertParallelArrivalFrame(session.CurrentFrame);
                Assert.AreEqual(SampleGraphFixture.VideoSource, introVideo.Command.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
                Assert.AreEqual("videos/0.mp4", introVideo.Command.Arguments.GetString("clip"));

                session.Continue();
                AssertParallelArrivalMediaFrame(session.CurrentFrame, 2);

                session.CompleteCommand("arrival_audio", "completed");
                AssertParallelArrivalMediaFrame(session.CurrentFrame, 1);

                session.CompleteCommand("arrival_video", "completed");
                AssertChoiceFrame(session.CurrentFrame, "episode_arrival", "arrival_merge_choices");

                session.Select("choice_enter_alley");
                AssertTrackFrame(session.CurrentFrame, FrameTrackKind.Text, "episode_alley", "alley_line");

                session.Continue();
                AssertTrackFrame(session.CurrentFrame, FrameTrackKind.Command, "episode_alley", "alley_minigame");

                session.CompleteCommand("alley_minigame", "success");
                AssertTrackFrame(session.CurrentFrame, FrameTrackKind.Command, "episode_alley", "alley_door_audio");

                session.CompleteCommand("alley_door_audio", "completed");
                var alleyVideoCommand = AssertTrackFrame(session.CurrentFrame, FrameTrackKind.Command, "episode_alley", "alley_video");
                Assert.AreEqual(SampleGraphFixture.VideoSource, alleyVideoCommand.Command.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
                Assert.AreEqual("videos/4.mp4", alleyVideoCommand.Command.Arguments.GetString("clip"));

                session.CompleteCommand("alley_video", "completed");
                Assert.IsTrue(session.CurrentFrame.IsCompleted);
                Assert.AreEqual("episode_alley", session.CurrentFrame.Episode.EpisodeId);

                Assert.IsTrue(session.History.Any(x => x.Action.Contains("选择 choice_enter_alley")));
                Assert.IsTrue(session.History.Any(x => x.Action.Contains("完成命令 alley_minigame:success")));
                Assert.IsTrue(session.History.Any(x => x.Action.Contains("继续")));
            }
        }

        [Test]
        public void SampleFixture_WhenOpenedInPlaybackWindow_ShowsRuntimeOutputAndControls()
        {
            var asset = CreateFixtureAsset();
            var window = ScriptableObject.CreateInstance<PlaybackWindow>();
            m_CreatedObjects.Add(window);

            window.SetContext(asset, SampleGraphFixture.RootEpisodeId);

            var labels = FindVisualChildren<Label>(window.rootVisualElement).Select(x => x.text).ToList();
            var buttons = window.rootVisualElement.Query<Button>().ToList().Select(x => x.text).ToList();
            var allText = string.Join("|", labels.Concat(buttons));

            Assert.IsTrue(labels.Any(x => x.Contains(SampleGraphFixture.StoryId)), allText);
            Assert.IsTrue(labels.Any(x => x.Contains(SampleGraphFixture.RootEpisodeId)), allText);
            Assert.IsTrue(labels.Any(x => x.Contains("正在播放：文本")), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "文本", StringComparison.Ordinal)), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "说话人", StringComparison.Ordinal)), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "黑雨压低了旧车站的灯光，站台尽头只剩一盏红色信号灯。", StringComparison.Ordinal)), allText);
            Assert.IsTrue(buttons.Any(x => string.Equals(x, "继续", StringComparison.Ordinal)), allText);
            Assert.IsTrue(buttons.Any(x => string.Equals(x, "重启章节", StringComparison.Ordinal)), allText);
        }

        [Test]
        public void SampleFixture_WhenPlaybackWindowReachesParallelFrame_ShowsVideoAudioAndTextTogether()
        {
            var asset = CreateFixtureAsset();
            var window = ScriptableObject.CreateInstance<PlaybackWindow>();
            m_CreatedObjects.Add(window);

            window.SetContext(asset, SampleGraphFixture.RootEpisodeId);
            InvokePrivate(window, "Continue");

            var labels = FindVisualChildren<Label>(window.rootVisualElement).Select(x => x.text).ToList();
            var buttons = window.rootVisualElement.Query<Button>().ToList().Select(x => x.text).ToList();
            var allText = string.Join("|", labels.Concat(buttons));

            Assert.IsTrue(labels.Any(x => x.Contains("正在播放：命令")), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "轨道", StringComparison.Ordinal)), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "3", StringComparison.Ordinal)), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "命令 ID", StringComparison.Ordinal)), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "arrival_video", StringComparison.Ordinal)), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "arrival_audio", StringComparison.Ordinal)), allText);
            Assert.IsTrue(labels.Any(x => x.Contains("视频轨") && x.Contains("branch_video")), allText);
            Assert.IsTrue(labels.Any(x => x.Contains("音频轨") && x.Contains("branch_audio")), allText);
            Assert.IsTrue(labels.Any(x => x.Contains("对白轨") && x.Contains("branch_dialogue")), allText);
            Assert.IsTrue(labels.Any(x => string.Equals(x, "文本", StringComparison.Ordinal)), allText);
            Assert.IsTrue(labels.Any(x => x.Contains("站住。这里今晚不该有人来。")), allText);
            Assert.IsFalse(buttons.Any(x => x.Contains("绕开守卫进入暗巷")), allText);
        }

        [Test]
        public void SampleFixture_WhenPlaybackWindowAdvances_RefreshesRuntimeOutput()
        {
            var asset = CreateFixtureAsset();
            var window = ScriptableObject.CreateInstance<PlaybackWindow>();
            m_CreatedObjects.Add(window);

            window.SetContext(asset, SampleGraphFixture.RootEpisodeId);
            var session = GetPrivateField<Session>(window, "m_Session");
            AssertTrackFrame(session.CurrentFrame, FrameTrackKind.Text, "episode_arrival", "arrival_intro");

            InvokePrivate(window, "Continue");
            var video = AssertParallelArrivalFrame(session.CurrentFrame);
            Assert.AreEqual(SampleGraphFixture.VideoSource, video.Command.Arguments.GetString(MediaCommandNames.MediaSourceArgument));
            Assert.AreEqual("videos/0.mp4", video.Command.Arguments.GetString("clip"));
            Assert.IsTrue(session.History.Any(x => x.Summary.Contains("branch_video") && x.Summary.Contains("branch_dialogue")));

            InvokePrivate(window, "Continue");
            AssertParallelArrivalMediaFrame(session.CurrentFrame, 2);

            InvokePrivate(window, "CompleteCommand", "arrival_audio", "completed");
            AssertParallelArrivalMediaFrame(session.CurrentFrame, 1);

            InvokePrivate(window, "CompleteCommand", "arrival_video", "completed");
            AssertChoiceFrame(session.CurrentFrame, "episode_arrival", "arrival_merge_choices");

            InvokePrivate(window, "Select", "choice_enter_alley");
            AssertTrackFrame(session.CurrentFrame, FrameTrackKind.Text, "episode_alley", "alley_line");

            var labels = FindVisualChildren<Label>(window.rootVisualElement).Select(x => x.text).ToList();
            var buttons = window.rootVisualElement.Query<Button>().ToList().Select(x => x.text).ToList();
            Assert.IsTrue(labels.Any(x => x.Contains("正在播放：文本")), string.Join("|", labels));
            Assert.IsTrue(buttons.Any(x => string.Equals(x, "继续", StringComparison.Ordinal)), string.Join("|", buttons));
            Assert.IsTrue(labels.Any(x => string.Equals(x, "历史", StringComparison.Ordinal)), string.Join("|", labels));
            Assert.IsTrue(session.History.Any(x => x.Action.Contains("选择 choice_enter_alley")));
        }

        [Test]
        public void PlaybackWindow_WhenCompileFails_ShowsErrorAndDoesNotStartRuntimeSession()
        {
            var asset = CreateFixtureAsset();
            var arrival = SampleGraphFixture.FindEpisode(asset, "episode_arrival");
            var video = SampleGraphFixture.FindNode(arrival, "arrival_video");
            video.Parameters.RemoveAll(x => string.Equals(x.Key, "clip", StringComparison.Ordinal));
            var window = ScriptableObject.CreateInstance<PlaybackWindow>();
            m_CreatedObjects.Add(window);

            window.SetContext(asset, SampleGraphFixture.RootEpisodeId);

            var session = GetPrivateField<Session>(window, "m_Session");
            var labels = FindVisualChildren<Label>(window.rootVisualElement).Select(x => x.text).ToList();
            var allText = string.Join("|", labels);

            Assert.IsFalse(session.Started);
            Assert.IsTrue(session.Report.HasErrors);
            Assert.IsTrue(labels.Any(x => x.Contains("编译失败")), allText);
        }

        [Test]
        public void PlaybackWindow_WhenDisabled_ShutsDownSession()
        {
            var asset = CreateFixtureAsset();
            var window = ScriptableObject.CreateInstance<PlaybackWindow>();
            m_CreatedObjects.Add(window);

            window.SetContext(asset, SampleGraphFixture.RootEpisodeId);
            Assert.IsNotNull(GetPrivateField<Session>(window, "m_Session"));

            InvokePrivate(window, "OnDisable");

            Assert.IsNull(GetPrivateField<Session>(window, "m_Session"));
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

        [Test]
        public void SampleFixture_WhenOpenedInStoryEditorWindow_ShowsReadableTreeAndGraphFields()
        {
            var asset = CreateFixtureAsset();
            var window = CreateStoryEditorWindow(asset);

            var treeLabels = window.rootVisualElement.Query<VisualElement>(className: "story-editor__tree-row").ToList()
                .SelectMany(FindVisualChildren<Label>)
                .Select(x => x.text)
                .ToList();
            var graphNodes = window.rootVisualElement.Query<VisualElement>(className: "editor-node-graph-node").ToList();
            var nodeText = string.Join("|", FindVisualChildren<Label>(window.rootVisualElement).Select(x => x.text)
                .Concat(window.rootVisualElement.Query<TextField>().ToList().Select(x => x.label))
                .Concat(window.rootVisualElement.Query<Toggle>().ToList().Select(x => x.label)));
            var diagnostics = GetGraphDiagnosticItems(window);

            Assert.IsFalse(treeLabels.Any(x => string.Equals(x, "剧情  sample_story_graph", StringComparison.Ordinal)), string.Join(",", treeLabels));
            Assert.IsTrue(treeLabels.Any(x => x.Contains("雨夜抵达")), string.Join(",", treeLabels));
            Assert.IsTrue(treeLabels.Any(x => x.Contains("旧车站")), string.Join(",", treeLabels));
            Assert.IsTrue(treeLabels.Any(x => x.Contains("暗巷")), string.Join(",", treeLabels));
            Assert.IsTrue(treeLabels.Any(x => x.Contains("余波")), string.Join(",", treeLabels));
            Assert.IsTrue(treeLabels.Any(x => x.Contains("交互视频演示")), string.Join(",", treeLabels));
            Assert.IsTrue(graphNodes.Any(x => string.Equals(x.userData as string, "arrival_parallel", StringComparison.Ordinal)));
            Assert.IsTrue(graphNodes.Any(x => string.Equals(x.userData as string, "arrival_merge", StringComparison.Ordinal)));
            Assert.IsTrue(graphNodes.Any(x => string.Equals(x.userData as string, "arrival_video", StringComparison.Ordinal)));
            Assert.IsTrue(graphNodes.Any(x => string.Equals(x.userData as string, "choice_enter_alley", StringComparison.Ordinal)));
            Assert.IsTrue(nodeText.Contains("并行"), nodeText);
            Assert.IsTrue(nodeText.Contains("等待全部完成"), nodeText);
            Assert.IsTrue(nodeText.Contains("播放视频"), nodeText);
            Assert.IsTrue(nodeText.Contains("多语言 Key"), nodeText);
            Assert.IsTrue(nodeText.Contains("视频"), nodeText);
            Assert.IsTrue(nodeText.Contains("等待完成"), nodeText);
            Assert.IsFalse(diagnostics.Any(x => x.GraphDiagnostic.Severity == EditorGraphDiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics.Select(x => x.GraphDiagnostic.Message)));
        }

        private AuthoringAsset CreateFixtureAsset()
        {
            var asset = SampleGraphFixture.Create();
            m_CreatedObjects.Add(asset);
            return asset;
        }

        private EditorWindow CreateStoryEditorWindow(AuthoringAsset asset)
        {
            var window = ScriptableObject.CreateInstance<MainWindow>();
            m_CreatedObjects.Add(window);
            SetPrivateField(window, "m_Asset", asset);
            InvokePrivate(window, "SelectDefaults");
            InvokePrivate(window, "BuildLayout");
            InvokePrivate(window, "RefreshAll", "Ready.");
            return window;
        }

        private static void AssertParameter(AuthoringNode node, string key, string value)
        {
            Assert.IsNotNull(node, key);
            Assert.AreEqual(value, node.Parameters.First(x => string.Equals(x.Key, key, StringComparison.Ordinal)).Value);
        }

        private static Step FindStep(Program program, string episodeId, string stepId)
        {
            var episode = program.Volumes.SelectMany(x => x.Episodes).First(x => string.Equals(x.EpisodeId, episodeId, StringComparison.Ordinal));
            return episode.Steps.First(x => string.Equals(x.StepId, stepId, StringComparison.Ordinal));
        }

        private static void AssertChoiceFrame(Frame frame, string episodeId, string stepId)
        {
            AssertFrame(frame, episodeId, stepId);
            Assert.Greater(frame.Choices.Count, 0);
            Assert.IsTrue(frame.WaitsForChoice);
            Assert.IsFalse(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);
            Assert.IsFalse(frame.IsCompleted);
        }

        private static FrameTrack AssertTrackFrame(Frame frame, FrameTrackKind kind, string episodeId, string stepId)
        {
            AssertFrame(frame, episodeId, stepId);
            AssertFrameTracks(frame, kind);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsFalse(frame.IsCompleted);
            return frame.Tracks[0];
        }

        private static void AssertTextChoiceFrame(Frame frame, string episodeId, string stepId)
        {
            AssertFrame(frame, episodeId, stepId);
            AssertFrameTracks(frame, FrameTrackKind.Text);
            Assert.Greater(frame.Choices.Count, 0);
            Assert.IsTrue(frame.WaitsForChoice);
            Assert.IsFalse(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);
            Assert.IsFalse(frame.IsCompleted);
        }

        private static FrameTrack AssertParallelArrivalFrame(Frame frame)
        {
            AssertFrame(frame, "episode_arrival", "arrival_parallel");
            Assert.AreEqual(3, frame.Tracks.Count);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsFalse(frame.WaitsForChoice);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.AreEqual(FrameTrackKind.Command, frame.Tracks[0].Kind);
            Assert.AreEqual("arrival_video", frame.Tracks[0].Step.StepId);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual(FrameTrackKind.Command, frame.Tracks[1].Kind);
            Assert.AreEqual("arrival_audio", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_audio", frame.Tracks[1].BranchId);
            Assert.AreEqual(FrameTrackKind.Text, frame.Tracks[2].Kind);
            Assert.AreEqual("arrival_guard_line", frame.Tracks[2].Step.StepId);
            Assert.AreEqual("branch_dialogue", frame.Tracks[2].BranchId);
            return frame.Tracks[0];
        }

        private static void AssertParallelArrivalMediaFrame(Frame frame, int commandCount)
        {
            AssertFrame(frame, "episode_arrival", "arrival_parallel");
            Assert.AreEqual(commandCount, frame.Tracks.Count);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsFalse(frame.WaitsForChoice);
            Assert.AreEqual(commandCount > 0, frame.WaitsForCommand);
            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                Assert.AreEqual(FrameTrackKind.Command, frame.Tracks[i].Kind);
            }
        }

        private static void AssertFrameTracks(Frame frame, params FrameTrackKind[] kinds)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(kinds.Length, frame.Tracks.Count);
            for (var i = 0; i < kinds.Length; i++)
            {
                Assert.AreEqual(kinds[i], frame.Tracks[i].Kind);
            }
        }

        private static void AssertFrame(Frame frame, string episodeId, string stepId)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(episodeId, frame.Episode.EpisodeId);
            Assert.AreEqual(stepId, frame.AnchorStep.StepId);
        }

        private static IReadOnlyList<DiagnosticSnapshot> GetGraphDiagnosticItems(EditorWindow window)
        {
            var diagnostics = GetPrivateField<object>(window, "m_GraphDiagnostics");
            var items = (System.Collections.IEnumerable)diagnostics.GetType().GetProperty("Items").GetValue(diagnostics);
            return items.Cast<object>().Select(DiagnosticSnapshot.From).ToList();
        }

        private sealed class DiagnosticSnapshot
        {
            public EditorGraphDiagnostic GraphDiagnostic { get; private set; }

            public static DiagnosticSnapshot From(object rawItem)
            {
                return new DiagnosticSnapshot
                {
                    GraphDiagnostic = (EditorGraphDiagnostic)rawItem.GetType().GetProperty("GraphDiagnostic").GetValue(rawItem)
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
            var field = instance.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(instance, value);
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            var field = instance.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return (T)field.GetValue(instance);
        }

        private static void InvokePrivate(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            method.Invoke(instance, args);
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

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}

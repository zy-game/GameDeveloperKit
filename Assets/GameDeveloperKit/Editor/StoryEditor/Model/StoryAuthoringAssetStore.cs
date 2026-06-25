using System;
using System.IO;
using GameDeveloperKit.Story;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor
{
    /// <summary>
    /// Story authoring asset 存取。
    /// </summary>
    internal static class StoryAuthoringAssetStore
    {
        private const string DefaultFolder = "Assets/GameDeveloperKit/Story";
        private const string DefaultAssetPath = DefaultFolder + "/NewStoryAuthoring.asset";

        public static StoryAuthoringAsset LoadOrCreate()
        {
            EnsureFolder(DefaultFolder);

            var asset = AssetDatabase.LoadAssetAtPath<StoryAuthoringAsset>(DefaultAssetPath);
            if (asset != null)
            {
                asset.EnsureDefaults();
                return asset;
            }

            asset = ScriptableObject.CreateInstance<StoryAuthoringAsset>();
            asset.EnsureDefaults();
            AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            Save(asset);
            return asset;
        }

        public static StoryAuthoringAsset CreateAtPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<StoryAuthoringAsset>();
            asset.EnsureDefaults();
            AssetDatabase.CreateAsset(asset, assetPath);
            Save(asset);
            return asset;
        }

        public static void Save(StoryAuthoringAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            asset.EnsureDefaults();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
            if (string.IsNullOrWhiteSpace(parent) is false && string.IsNullOrWhiteSpace(name) is false && AssetDatabase.IsValidFolder(folder) is false)
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }

    /// <summary>
    /// Story Editor 标准示例剧情图。
    /// </summary>
    public static class StorySampleGraphFixture
    {
        public const string StoryId = "sample_story_graph";
        public const string Version = "1.0.0";
        public const string EntryChapterId = "chapter_arrival";
        public const string AssetPath = "Assets/GameDeveloperKit/Story/SampleStoryGraph.asset";
        public const string VideoSource = StoryMediaCommandNames.VideoSourceStreamingAssets;
        public const string IntroVideoPath = "Assets/StreamingAssets/videos/0.mp4";
        public const string AlleyVideoPath = "Assets/StreamingAssets/videos/4.mp4";
        public const string MapImagePath = "Assets/GameDeveloperKit/Simples/UI/test.jpg";
        public const string StationAudioPath = "Assets/GameDeveloperKit/Simples/Sounds/bgm.mp3";
        public const string DoorAudioPath = "Assets/GameDeveloperKit/Simples/Sounds/opendoor.mp3";

        public static readonly string[] ChapterIds =
        {
            "chapter_arrival",
            "chapter_station",
            "chapter_alley",
            "chapter_final"
        };

        public static StoryAuthoringAsset Create()
        {
            var asset = ScriptableObject.CreateInstance<StoryAuthoringAsset>();
            asset.StoryId = StoryId;
            asset.Version = Version;
            asset.EntryChapterId = EntryChapterId;
            asset.EnsureDefaults();
            asset.SelectedVolume.Chapters.Clear();
            asset.Layout.Nodes.Clear();

            var chapters = new[]
            {
                CreateArrivalChapter(asset),
                CreateStationChapter(asset),
                CreateAlleyChapter(asset),
                CreateFinalChapter(asset)
            };

            for (var i = 0; i < chapters.Length; i++)
            {
                asset.SelectedVolume.Chapters.Add(chapters[i]);
            }

            asset.EnsureDefaults();
            return asset;
        }

        public static StoryAuthoringAsset LoadOrCreateAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<StoryAuthoringAsset>(AssetPath);
            if (asset != null)
            {
                asset.EnsureDefaults();
                if (ShouldRefreshSample(asset))
                {
                    var refreshed = Create();
                    EditorUtility.CopySerialized(refreshed, asset);
                    UnityEngine.Object.DestroyImmediate(refreshed);
                    StoryAuthoringAssetStore.Save(asset);
                }

                return asset;
            }

            EnsureFolder(Path.GetDirectoryName(AssetPath)?.Replace('\\', '/'));
            asset = Create();
            AssetDatabase.CreateAsset(asset, AssetPath);
            StoryAuthoringAssetStore.Save(asset);
            return asset;
        }

        private static bool ShouldRefreshSample(StoryAuthoringAsset asset)
        {
            if (asset == null || string.Equals(asset.StoryId, StoryId, StringComparison.Ordinal) is false)
            {
                return false;
            }

            var arrival = FindChapter(asset, "chapter_arrival");
            var parallel = FindNode(arrival, "arrival_parallel");
            var merge = FindNode(arrival, "arrival_merge");
            var video = FindNode(arrival, "arrival_video");
            var audio = FindNode(arrival, "arrival_audio");
            var intro = FindNode(arrival, "arrival_intro");
            if (parallel == null || merge == null || video == null || audio == null || intro == null)
            {
                return true;
            }

            var source = GetParameter(video, StoryMediaCommandNames.VideoSourceArgument);
            var clip = GetParameter(video, "clip");
            var audioClip = GetParameter(audio, "clip");
            var text = GetParameter(intro, "textKey");
            return string.Equals(source, VideoSource, StringComparison.Ordinal) is false ||
                   string.Equals(clip, IntroVideoPath, StringComparison.Ordinal) is false ||
                   string.Equals(audioClip, StationAudioPath, StringComparison.Ordinal) is false ||
                   string.IsNullOrWhiteSpace(text) ||
                   text.StartsWith("story.", StringComparison.Ordinal);
        }

        private static string GetParameter(StoryAuthoringNode node, string key)
        {
            if (node == null)
            {
                return null;
            }

            for (var i = 0; i < node.Parameters.Count; i++)
            {
                var parameter = node.Parameters[i];
                if (parameter != null && string.Equals(parameter.Key, key, StringComparison.Ordinal))
                {
                    return parameter.Value;
                }
            }

            return null;
        }

        public static StoryAuthoringChapter FindChapter(StoryAuthoringAsset asset, string chapterId)
        {
            if (asset == null)
            {
                return null;
            }

            for (var i = 0; i < asset.Chapters.Count; i++)
            {
                var chapter = asset.Chapters[i];
                if (chapter != null && string.Equals(chapter.ChapterId, chapterId, StringComparison.Ordinal))
                {
                    return chapter;
                }
            }

            return null;
        }

        public static StoryAuthoringNode FindNode(StoryAuthoringChapter chapter, string nodeId)
        {
            if (chapter == null)
            {
                return null;
            }

            for (var i = 0; i < chapter.Nodes.Count; i++)
            {
                var node = chapter.Nodes[i];
                if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        public static StoryAuthoringEdge FindEdge(StoryAuthoringChapter chapter, string edgeId)
        {
            if (chapter == null)
            {
                return null;
            }

            for (var i = 0; i < chapter.Edges.Count; i++)
            {
                var edge = chapter.Edges[i];
                if (edge != null && string.Equals(edge.EdgeId, edgeId, StringComparison.Ordinal))
                {
                    return edge;
                }
            }

            return null;
        }

        private static StoryAuthoringChapter CreateArrivalChapter(StoryAuthoringAsset asset)
        {
            var chapter = Chapter("chapter_arrival", "雨夜抵达", "arrival_start");
            AddNodes(
                chapter,
                Node("arrival_start", "开始", NodeKind.Start),
                Node("arrival_intro", "旁白：雨夜抵达", NodeKind.Narration, ("textKey", "黑雨压低了旧车站的灯光，站台尽头只剩一盏红色信号灯。")),
                Node("arrival_parallel", "并行：开场表现", NodeKind.Parallel),
                Node("arrival_video", "播放开场视频", NodeKind.PlayVideo, (StoryMediaCommandNames.VideoSourceArgument, VideoSource), ("clip", IntroVideoPath), ("wait", "true")),
                Node("arrival_audio", "播放车站环境音", NodeKind.PlayAudio, ("clip", StationAudioPath)),
                Node("arrival_guard_line", "守卫对白", NodeKind.Dialogue, ("textKey", "站住。这里今晚不该有人来。"), ("speaker", "守卫")),
                Node("choice_enter_alley", "选择：进入暗巷", NodeKind.Choice, ("textKey", "绕开守卫进入暗巷")),
                Node("choice_help_guard", "选择：帮助守卫", NodeKind.Choice, ("textKey", "询问守卫发生了什么")),
                Node("arrival_merge", "等待全部完成：开场表现", NodeKind.Merge),
                Node("arrival_show_map", "显示雨巷地图", NodeKind.ShowImage, ("image", MapImagePath)),
                Node("arrival_help_line", "守卫补充说明", NodeKind.Dialogue, ("textKey", "如果你真想帮忙，就去候车大厅找那枚旧徽章。"), ("speaker", "守卫")),
                Node("arrival_wait_rain", "等待雨声", NodeKind.Wait, ("duration", "1.5")),
                Node("jump_alley", "跳转暗巷", NodeKind.JumpChapter, ("chapterId", "chapter_alley")),
                Node("jump_station", "跳转旧车站", NodeKind.JumpChapter, ("chapterId", "chapter_station")),
                Node("arrival_end", "结束", NodeKind.End));
            AddEdges(
                chapter,
                Edge("edge_arrival_start_intro", "arrival_start", "completed", "完成", TargetNode("arrival_intro")),
                Edge("edge_arrival_intro_parallel", "arrival_intro", "completed", "完成", TargetNode("arrival_parallel")),
                Edge("edge_arrival_parallel_video", "arrival_parallel", "branch_video", "视频轨", TargetNode("arrival_video")),
                Edge("edge_arrival_parallel_audio", "arrival_parallel", "branch_audio", "音频轨", TargetNode("arrival_audio")),
                Edge("edge_arrival_parallel_dialogue", "arrival_parallel", "branch_dialogue", "对白轨", TargetNode("arrival_guard_line")),
                Edge("edge_arrival_video_merge", "arrival_video", "completed", "完成", TargetNode("arrival_merge")),
                Edge("edge_arrival_audio_merge", "arrival_audio", "completed", "完成", TargetNode("arrival_merge")),
                Edge("edge_arrival_guard_merge", "arrival_guard_line", "completed", "完成", TargetNode("arrival_merge")),
                Edge("edge_arrival_merge_alley_choice", "arrival_merge", "completed", "进入选择", TargetNode("choice_enter_alley")),
                Edge("edge_arrival_merge_help_choice", "arrival_merge", "completed", "进入选择", TargetNode("choice_help_guard")),
                Edge("edge_choice_alley_map", "choice_enter_alley", "selected", "选择后", TargetNode("arrival_show_map")),
                Edge("edge_choice_help_line", "choice_help_guard", "selected", "选择后", TargetNode("arrival_help_line")),
                Edge("edge_help_line_station", "arrival_help_line", "completed", "完成", TargetNode("jump_station")),
                Edge("edge_arrival_map_wait", "arrival_show_map", "completed", "完成", TargetNode("arrival_wait_rain")),
                Edge("edge_arrival_wait_alley", "arrival_wait_rain", "completed", "完成", TargetNode("jump_alley")),
                Edge("edge_jump_alley_chapter", "jump_alley", "completed", "完成", TargetChapter("chapter_alley")),
                Edge("edge_jump_station_chapter", "jump_station", "completed", "完成", TargetChapter("chapter_station")));
            AddLayout(
                asset,
                "chapter_arrival",
                ("arrival_start", 0f, 120f),
                ("arrival_intro", 220f, 120f),
                ("arrival_parallel", 440f, 120f),
                ("arrival_video", 700f, 0f),
                ("arrival_audio", 700f, 140f),
                ("arrival_guard_line", 700f, 280f),
                ("arrival_merge", 980f, 140f),
                ("choice_enter_alley", 1240f, 60f),
                ("choice_help_guard", 1240f, 220f),
                ("arrival_show_map", 1500f, 60f),
                ("arrival_help_line", 1500f, 220f),
                ("arrival_wait_rain", 1720f, 60f),
                ("jump_alley", 1940f, 60f),
                ("jump_station", 1720f, 220f),
                ("arrival_end", 2160f, 140f));
            return chapter;
        }

        private static StoryAuthoringChapter CreateStationChapter(StoryAuthoringAsset asset)
        {
            var chapter = Chapter("chapter_station", "旧车站", "station_start");
            AddNodes(
                chapter,
                Node("station_start", "开始", NodeKind.Start),
                Node("station_intro", "旁白：旧车站", NodeKind.Narration, ("textKey", "候车大厅空无一人，广播却还在重复播放一段旧通知。")),
                Node("station_audio", "播放车站环境音", NodeKind.PlayAudio, ("clip", StationAudioPath)),
                Node("station_line", "列车员对白", NodeKind.Dialogue, ("textKey", "拿着这枚徽章，别让检票口认出你。"), ("speaker", "列车员")),
                Node("choice_take_badge", "选择：收下徽章", NodeKind.Choice, ("textKey", "收下站台徽章")),
                Node("choice_refuse_badge", "选择：拒绝徽章", NodeKind.Choice, ("textKey", "拒绝并立刻离开")),
                Node("station_gate_audio", "播放闸机声", NodeKind.PlayAudio, ("clip", DoorAudioPath)),
                Node("jump_station_final", "跳转余波", NodeKind.JumpChapter, ("chapterId", "chapter_final")),
                Node("jump_station_alley", "跳转暗巷", NodeKind.JumpChapter, ("chapterId", "chapter_alley")),
                Node("station_end", "结束", NodeKind.End));
            AddEdges(
                chapter,
                Edge("edge_station_start_intro", "station_start", "completed", "完成", TargetNode("station_intro")),
                Edge("edge_station_intro_audio", "station_intro", "completed", "完成", TargetNode("station_audio")),
                Edge("edge_station_audio_line", "station_audio", "completed", "完成", TargetNode("station_line")),
                Edge("edge_station_line_take", "station_line", "completed", "完成", TargetNode("choice_take_badge")),
                Edge("edge_station_line_refuse", "station_line", "completed", "完成", TargetNode("choice_refuse_badge")),
                Edge("edge_choice_take_badge", "choice_take_badge", "selected", "选择后", TargetNode("station_gate_audio")),
                Edge("edge_gate_audio_final", "station_gate_audio", "completed", "完成", TargetNode("jump_station_final")),
                Edge("edge_choice_refuse_alley", "choice_refuse_badge", "selected", "选择后", TargetNode("jump_station_alley")),
                Edge("edge_jump_station_final_chapter", "jump_station_final", "completed", "完成", TargetChapter("chapter_final")),
                Edge("edge_jump_station_alley_chapter", "jump_station_alley", "completed", "完成", TargetChapter("chapter_alley")));
            AddLayout(
                asset,
                "chapter_station",
                ("station_start", 0f, 120f),
                ("station_intro", 220f, 120f),
                ("station_audio", 440f, 120f),
                ("station_line", 660f, 120f),
                ("choice_take_badge", 900f, 40f),
                ("choice_refuse_badge", 900f, 220f),
                ("station_gate_audio", 1140f, 40f),
                ("jump_station_final", 1360f, 40f),
                ("jump_station_alley", 1140f, 220f),
                ("station_end", 1580f, 120f));
            return chapter;
        }

        private static StoryAuthoringChapter CreateAlleyChapter(StoryAuthoringAsset asset)
        {
            var chapter = Chapter("chapter_alley", "暗巷", "alley_start");
            AddNodes(
                chapter,
                Node("alley_start", "开始", NodeKind.Start),
                Node("alley_line", "陌生人对白", NodeKind.Dialogue, ("textKey", "门后不是出口，是另一个人的回忆。你确定要进去？"), ("speaker", "陌生人")),
                Node("choice_pick_lock", "选择：撬开铁门", NodeKind.Choice, ("textKey", "撬开铁门")),
                Node("choice_return_station", "选择：返回旧车站", NodeKind.Choice, ("textKey", "返回旧车站")),
                Node("alley_minigame", "小游戏：撬锁", NodeKind.MiniGame, ("miniGameId", "lockpick_gate")),
                Node("alley_door_audio", "播放开门声", NodeKind.PlayAudio, ("clip", DoorAudioPath)),
                Node("alley_video", "播放暗巷视频", NodeKind.PlayVideo, (StoryMediaCommandNames.VideoSourceArgument, VideoSource), ("clip", AlleyVideoPath), ("wait", "true")),
                Node("jump_alley_final", "跳转余波", NodeKind.JumpChapter, ("chapterId", "chapter_final")),
                Node("jump_alley_station", "跳转旧车站", NodeKind.JumpChapter, ("chapterId", "chapter_station")),
                Node("alley_end", "结束", NodeKind.End));
            AddEdges(
                chapter,
                Edge("edge_alley_start_line", "alley_start", "completed", "完成", TargetNode("alley_line")),
                Edge("edge_alley_line_pick", "alley_line", "completed", "完成", TargetNode("choice_pick_lock")),
                Edge("edge_alley_line_return", "alley_line", "completed", "完成", TargetNode("choice_return_station")),
                Edge("edge_choice_pick_minigame", "choice_pick_lock", "selected", "选择后", TargetNode("alley_minigame")),
                Edge("edge_choice_return_station", "choice_return_station", "selected", "选择后", TargetNode("jump_alley_station")),
                Edge("edge_minigame_success_audio", "alley_minigame", "success", "成功", TargetNode("alley_door_audio")),
                Edge("edge_door_audio_video", "alley_door_audio", "completed", "完成", TargetNode("alley_video")),
                Edge("edge_alley_video_final", "alley_video", "completed", "完成", TargetNode("jump_alley_final")),
                Edge("edge_minigame_fail_station", "alley_minigame", "fail", "失败", TargetNode("jump_alley_station")),
                Edge("edge_minigame_cancel_station", "alley_minigame", "cancel", "取消", TargetNode("jump_alley_station")),
                Edge("edge_jump_alley_final_chapter", "jump_alley_final", "completed", "完成", TargetChapter("chapter_final")),
                Edge("edge_jump_alley_station_chapter", "jump_alley_station", "completed", "完成", TargetChapter("chapter_station")));
            AddLayout(
                asset,
                "chapter_alley",
                ("alley_start", 0f, 140f),
                ("alley_line", 220f, 140f),
                ("choice_pick_lock", 460f, 60f),
                ("choice_return_station", 460f, 240f),
                ("alley_minigame", 700f, 60f),
                ("alley_door_audio", 940f, 40f),
                ("alley_video", 1180f, 40f),
                ("jump_alley_final", 1400f, 40f),
                ("jump_alley_station", 1180f, 240f),
                ("alley_end", 1620f, 140f));
            return chapter;
        }

        private static StoryAuthoringChapter CreateFinalChapter(StoryAuthoringAsset asset)
        {
            var chapter = Chapter("chapter_final", "余波", "final_start");
            AddNodes(
                chapter,
                Node("final_start", "开始", NodeKind.Start),
                Node("final_intro", "旁白：雨停之后", NodeKind.Narration, ("textKey", "雨停后，站台上的影子终于恢复成普通人的形状。")),
                Node("final_line", "主角对白", NodeKind.Dialogue, ("textKey", "我记住这条路了。下一次，我会提前到。"), ("speaker", "主角")),
                Node("final_emit_event", "发送事件：剧情结束", NodeKind.EmitEvent, ("eventId", "story.sample.completed")),
                Node("final_wait", "等待收束", NodeKind.Wait, ("duration", "0.5")),
                Node("final_end", "结束", NodeKind.End));
            AddEdges(
                chapter,
                Edge("edge_final_start_intro", "final_start", "completed", "完成", TargetNode("final_intro")),
                Edge("edge_final_intro_line", "final_intro", "completed", "完成", TargetNode("final_line")),
                Edge("edge_final_line_event", "final_line", "completed", "完成", TargetNode("final_emit_event")),
                Edge("edge_final_event_wait", "final_emit_event", "completed", "完成", TargetNode("final_wait")),
                Edge("edge_final_wait_end", "final_wait", "completed", "完成", TargetNode("final_end")));
            AddLayout(
                asset,
                "chapter_final",
                ("final_start", 0f, 120f),
                ("final_intro", 220f, 120f),
                ("final_line", 440f, 120f),
                ("final_emit_event", 660f, 120f),
                ("final_wait", 880f, 120f),
                ("final_end", 1100f, 120f));
            return chapter;
        }

        private static StoryAuthoringChapter Chapter(string chapterId, string title, string entryNodeId)
        {
            return new StoryAuthoringChapter
            {
                ChapterId = chapterId,
                Title = title,
                EntryNodeId = entryNodeId
            };
        }

        private static StoryAuthoringNode Node(string nodeId, string title, NodeKind kind, params (string key, string value)[] parameters)
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

        private static void AddNodes(StoryAuthoringChapter chapter, params StoryAuthoringNode[] nodes)
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                chapter.Nodes.Add(nodes[i]);
            }
        }

        private static void AddEdges(StoryAuthoringChapter chapter, params StoryAuthoringEdge[] edges)
        {
            for (var i = 0; i < edges.Length; i++)
            {
                chapter.Edges.Add(edges[i]);
            }
        }

        private static StoryAuthoringEdge Edge(
            string edgeId,
            string fromNodeId,
            string fromPortId,
            string fromPortLabel,
            (TransitionTargetKind kind, string chapterId, string nodeId) target,
            params StoryAuthoringCondition[] conditions)
        {
            var edge = new StoryAuthoringEdge
            {
                EdgeId = edgeId,
                FromNodeId = fromNodeId,
                FromPortId = fromPortId,
                FromPortLabel = fromPortLabel,
                TargetKind = target.kind,
                TargetChapterId = target.chapterId,
                TargetNodeId = target.nodeId
            };
            for (var i = 0; i < conditions.Length; i++)
            {
                edge.Conditions.Add(conditions[i]);
            }

            return edge;
        }

        private static (TransitionTargetKind kind, string chapterId, string nodeId) TargetNode(string nodeId)
        {
            return (TransitionTargetKind.Node, null, nodeId);
        }

        private static (TransitionTargetKind kind, string chapterId, string nodeId) TargetChapter(string chapterId)
        {
            return (TransitionTargetKind.Chapter, chapterId, null);
        }

        private static void AddLayout(StoryAuthoringAsset asset, string chapterId, params (string nodeId, float x, float y)[] nodes)
        {
            var graphId = chapterId;
            for (var i = 0; i < nodes.Length; i++)
            {
                asset.Layout.Nodes.Add(new StoryNodeLayout
                {
                    GraphId = graphId,
                    NodeId = nodes[i].nodeId,
                    Position = new Vector2(nodes[i].x, nodes[i].y)
                });
            }
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
            if (string.IsNullOrWhiteSpace(parent) is false && string.IsNullOrWhiteSpace(name) is false && AssetDatabase.IsValidFolder(folder) is false)
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}

using System;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Event;

namespace GameDeveloperKit.StoryEditor.Model
{
    public static partial class SampleGraphFixture
    {
        private const string InteractiveSeekVideoId = "interactive_seek_video";
        private const string InteractivePlaybackVideoId = "interactive_playback_video";
        private const string InteractiveQteVideoId = "interactive_qte_video";
        private const string InteractiveUnlockVideoId = "interactive_unlock_video";

        private static AuthoringEpisode CreateInteractiveVideoEpisode(AuthoringAsset asset)
        {
            var episode = Episode(InteractiveVideoEpisodeId, "交互视频演示", "interactive_start");
            AddNodes(
                episode,
                Node("interactive_start", "开始", NodeKind.Start),
                Node(
                    InteractiveSeekVideoId,
                    "可拖动过渡视频",
                    NodeKind.PlayVideo,
                    (MediaCommandNames.VideoSourceArgument, VideoSource),
                    (MediaCommandNames.ClipArgument, InteractiveVideoPath),
                    ("wait", "true"),
                    ("allowSeek", "true")),
                Node(
                    InteractivePlaybackVideoId,
                    "互动背景视频",
                    NodeKind.PlayVideo,
                    (MediaCommandNames.VideoSourceArgument, VideoSource),
                    (MediaCommandNames.ClipArgument, InteractiveVideoPath),
                    ("wait", "true"),
                    ("allowSeek", "false")),
                Node("interactive_qte_parallel", "并行：视频中途 QTE", NodeKind.Parallel),
                Node(
                    InteractiveQteVideoId,
                    "QTE 背景视频",
                    NodeKind.PlayVideo,
                    (MediaCommandNames.VideoSourceArgument, VideoSource),
                    (MediaCommandNames.ClipArgument, InteractiveVideoPath),
                    ("wait", "true"),
                    ("allowSeek", "false")),
                Node("interactive_qte_wait", "等待 QTE 出现", NodeKind.Wait, ("duration", "1.0")),
                Node(
                    "interactive_qte",
                    "QTE：挣脱黑影",
                    NodeKind.Event,
                    (EventCommandCodec.EventIdParameter, "sample.qte"),
                    (EventCommandCodec.ModeParameter, EventCommandCodec.RequestMode),
                    ("inputActionId", "interact"),
                    ("durationSeconds", "5"),
                    ("requiredCount", "2"),
                    ("promptTextKey", "连续点击以挣脱黑影")),
                Node("interactive_unlock_parallel", "并行：视频中途解锁", NodeKind.Parallel),
                Node(
                    InteractiveUnlockVideoId,
                    "解锁背景视频",
                    NodeKind.PlayVideo,
                    (MediaCommandNames.VideoSourceArgument, VideoSource),
                    (MediaCommandNames.ClipArgument, InteractiveVideoPath),
                    ("wait", "true"),
                    ("allowSeek", "false")),
                Node("interactive_unlock_wait", "等待解锁出现", NodeKind.Wait, ("duration", "1.0")),
                Node(
                    "interactive_unlock",
                    "开启检票门事件",
                    NodeKind.Event,
                    (EventCommandCodec.EventIdParameter, "sample.unlock"),
                    (EventCommandCodec.ModeParameter, EventCommandCodec.RequestMode),
                    ("unlockId", "sample.interactive.gate"),
                    ("puzzleType", "node_unlock"),
                    ("promptTextKey", "连接节点以开启检票门")),
                Node("interactive_qte_success", "QTE 成功", NodeKind.Narration, ("textKey", "你在黑影合拢前挣脱了束缚。")),
                Node("interactive_qte_fail", "QTE 失败", NodeKind.Narration, ("textKey", "黑影迫使你退回了站台边缘。")),
                Node("interactive_unlock_success", "解锁成功", NodeKind.Narration, ("textKey", "检票门亮起绿灯，隐藏通道已经开启。")),
                Node("interactive_unlock_fail", "解锁失败", NodeKind.Narration, ("textKey", "节点连接中断，检票门重新锁死。")),
                Node("interactive_qte_merge", "等待 QTE 视频与互动完成", NodeKind.Merge),
                Node("interactive_unlock_merge", "等待解锁视频与互动完成", NodeKind.Merge),
                Node("interactive_end", "结束", NodeKind.End));
            AddEdges(
                episode,
                Edge("edge_interactive_start_seek", "interactive_start", "completed", "完成", TargetNode(InteractiveSeekVideoId)),
                Edge("edge_interactive_seek_playback", InteractiveSeekVideoId, "completed", "完成", TargetNode(InteractivePlaybackVideoId)),
                Edge("edge_interactive_playback_qte", InteractivePlaybackVideoId, "completed", "完成", TargetNode("interactive_qte_parallel")),
                Edge("edge_interactive_qte_parallel_video", "interactive_qte_parallel", "branch_video", "视频轨", TargetNode(InteractiveQteVideoId)),
                Edge("edge_interactive_qte_parallel_wait", "interactive_qte_parallel", "branch_interaction", "交互轨", TargetNode("interactive_qte_wait")),
                Edge("edge_interactive_qte_video_merge", InteractiveQteVideoId, "completed", "完成", TargetNode("interactive_qte_merge")),
                Edge("edge_interactive_qte_wait_command", "interactive_qte_wait", "completed", "完成", TargetNode("interactive_qte")),
                Edge("edge_interactive_qte_success", "interactive_qte", "success", "成功", TargetNode("interactive_qte_success")),
                Edge("edge_interactive_qte_fail", "interactive_qte", "fail", "失败", TargetNode("interactive_qte_fail")),
                Edge("edge_interactive_unlock_parallel_video", "interactive_unlock_parallel", "branch_video", "视频轨", TargetNode(InteractiveUnlockVideoId)),
                Edge("edge_interactive_unlock_parallel_wait", "interactive_unlock_parallel", "branch_interaction", "交互轨", TargetNode("interactive_unlock_wait")),
                Edge("edge_interactive_unlock_video_merge", InteractiveUnlockVideoId, "completed", "完成", TargetNode("interactive_unlock_merge")),
                Edge("edge_interactive_unlock_wait_command", "interactive_unlock_wait", "completed", "完成", TargetNode("interactive_unlock")),
                Edge("edge_interactive_unlock_success", "interactive_unlock", "success", "成功", TargetNode("interactive_unlock_success")),
                Edge("edge_interactive_unlock_fail", "interactive_unlock", "fail", "失败", TargetNode("interactive_unlock_fail")),
                Edge("edge_interactive_qte_success_merge", "interactive_qte_success", "completed", "完成", TargetNode("interactive_qte_merge")),
                Edge("edge_interactive_qte_fail_merge", "interactive_qte_fail", "completed", "完成", TargetNode("interactive_qte_merge")),
                Edge("edge_interactive_unlock_success_merge", "interactive_unlock_success", "completed", "完成", TargetNode("interactive_unlock_merge")),
                Edge("edge_interactive_unlock_fail_merge", "interactive_unlock_fail", "completed", "完成", TargetNode("interactive_unlock_merge")),
                Edge("edge_interactive_qte_merge_unlock", "interactive_qte_merge", "completed", "完成", TargetNode("interactive_unlock_parallel")),
                Edge("edge_interactive_unlock_merge_end", "interactive_unlock_merge", "completed", "完成", TargetNode("interactive_end")));
            AddLayout(
                asset,
                InteractiveVideoEpisodeId,
                ("interactive_start", 0f, 160f),
                (InteractiveSeekVideoId, 220f, 160f),
                (InteractivePlaybackVideoId, 480f, 160f),
                ("interactive_qte_parallel", 740f, 160f),
                (InteractiveQteVideoId, 1000f, 20f),
                ("interactive_qte_wait", 1000f, 200f),
                ("interactive_qte", 1240f, 200f),
                ("interactive_qte_success", 1480f, 120f),
                ("interactive_qte_fail", 1480f, 240f),
                ("interactive_qte_merge", 1720f, 180f),
                ("interactive_unlock_parallel", 1960f, 180f),
                (InteractiveUnlockVideoId, 2200f, 40f),
                ("interactive_unlock_wait", 2200f, 260f),
                ("interactive_unlock", 2440f, 260f),
                ("interactive_unlock_success", 2680f, 180f),
                ("interactive_unlock_fail", 2680f, 300f),
                ("interactive_unlock_merge", 2920f, 220f),
                ("interactive_end", 3160f, 220f));
            return episode;
        }

        private static bool HasInteractiveVideoSample(AuthoringAsset asset)
        {
            var episode = FindEpisode(asset, InteractiveVideoEpisodeId);
            var seekVideo = FindNode(episode, InteractiveSeekVideoId);
            var playbackVideo = FindNode(episode, InteractivePlaybackVideoId);
            var qteVideo = FindNode(episode, InteractiveQteVideoId);
            var unlockVideo = FindNode(episode, InteractiveUnlockVideoId);
            return episode != null &&
                   FindNode(episode, "interactive_qte")?.NodeKind == NodeKind.Event &&
                   FindNode(episode, "interactive_unlock")?.NodeKind == NodeKind.Event &&
                   string.Equals(GetParameter(seekVideo, "allowSeek"), "true", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetParameter(playbackVideo, "allowSeek"), "false", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetParameter(qteVideo, "allowSeek"), "false", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetParameter(unlockVideo, "allowSeek"), "false", StringComparison.OrdinalIgnoreCase);
        }
    }
}

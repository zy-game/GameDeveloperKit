using System;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Protocol;

namespace GameDeveloperKit.StoryEditor.Model
{
    public static partial class SampleGraphFixture
    {
        private const string InteractiveSeekVideoId = "interactive_seek_video";
        private const string InteractivePlaybackVideoId = "interactive_playback_video";
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
                Node("interactive_transition", "过渡到余波", NodeKind.Transition));
            AddEdges(
                episode,
                Edge("edge_interactive_start_seek", "interactive_start", "completed", "完成", TargetNode(InteractiveSeekVideoId)),
                Edge("edge_interactive_seek_playback", InteractiveSeekVideoId, "completed", "完成", TargetNode(InteractivePlaybackVideoId)),
                Edge("edge_interactive_playback_transition", InteractivePlaybackVideoId, "completed", "完成", TargetNode("interactive_transition")));
            AddLayout(
                episode,
                ("interactive_start", 0f, 160f),
                (InteractiveSeekVideoId, 220f, 160f),
                (InteractivePlaybackVideoId, 480f, 160f),
                ("interactive_transition", 740f, 160f));
            return episode;
        }

        private static bool HasInteractiveVideoSample(AuthoringAsset asset)
        {
            var episode = FindEpisode(asset, InteractiveVideoEpisodeId);
            var seekVideo = FindNode(episode, InteractiveSeekVideoId);
            var playbackVideo = FindNode(episode, InteractivePlaybackVideoId);
            return episode != null &&
                   FindNode(episode, "interactive_transition")?.NodeKind == NodeKind.Transition &&
                   string.Equals(GetParameter(seekVideo, "allowSeek"), "true", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetParameter(playbackVideo, "allowSeek"), "false", StringComparison.OrdinalIgnoreCase);
        }
    }
}

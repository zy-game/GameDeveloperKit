using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Event;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Settlement;
using GameDeveloperKit.Story.Publishing;

namespace GameDeveloperKit.StoryEditor.Model
{
    /// <summary>
    /// Story Editor 标准示例剧情图。
    /// </summary>
    public static partial class SampleGraphFixture
    {
        public const string StoryId = "sample_story_graph";
        public const string Version = "1.2.0";
        public const string PrimaryVolumeId = "volume_black_rain";
        public const string SecondaryVolumeId = "volume_after_rain";
        public const string RootEpisodeId = "episode_arrival";
        public const string SecondaryRootEpisodeId = "episode_after_rain";
        public const string InteractiveVideoEpisodeId = "episode_interactive_video";
        public const string AssetPath = "Assets/Bundles/Story/SampleStoryGraph.asset";
        public const string VideoSource = MediaCommandNames.VideoSourceStreamingAssets;
        public const string IntroVideoPath = "Assets/StreamingAssets/videos/0.mp4";
        public const string AlleyVideoPath = "Assets/StreamingAssets/videos/4.mp4";
        public const string InteractiveVideoPath = "Assets/StreamingAssets/videos/6.mp4";
        public const string MapImagePath = "Assets/Bundles/Story/UI/test.jpg";
        public const string StationAudioPath = "Assets/Bundles/Story/Sounds/bgm.mp3";
        public const string DoorAudioPath = "Assets/Bundles/Story/Sounds/opendoor.mp3";

        public static readonly string[] EpisodeIds =
        {
            "episode_arrival",
            "episode_station",
            "episode_alley",
            "episode_final",
            InteractiveVideoEpisodeId,
            SecondaryRootEpisodeId
        };

        public static AuthoringAsset Create()
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            asset.StoryId = StoryId;
            asset.Version = Version;
            asset.Volumes.Clear();
            var primaryVolume = new AuthoringVolume
            {
                VolumeId = PrimaryVolumeId,
                Title = "第一卷：乡村少年",
                Description = "雨夜抵达旧车站后展开的分支路线。",
                Route = new AuthoringRoute()
            };
            var secondaryVolume = new AuthoringVolume
            {
                VolumeId = SecondaryVolumeId,
                Title = "第二卷：雨后余声",
                Description = "雨停后的独立卷路线，用于展示多卷内容组织。",
                Route = new AuthoringRoute()
            };
            asset.Volumes.Add(primaryVolume);
            asset.Volumes.Add(secondaryVolume);

            var episodes = new[]
            {
                CreateArrivalEpisode(asset),
                CreateStationEpisode(asset),
                CreateAlleyEpisode(asset),
                CreateFinalEpisode(asset),
                CreateInteractiveVideoEpisode(asset)
            };

            for (var i = 0; i < episodes.Length; i++)
            {
                primaryVolume.Episodes.Add(episodes[i]);
            }

            secondaryVolume.Episodes.Add(CreateAfterRainEpisode());
            BuildRouteAndLayouts(primaryVolume);
            BuildSecondaryRouteAndLayouts(secondaryVolume);
            return asset;
        }

        public static AuthoringAsset LoadOrCreateAsset()
        {
            EnsureSampleMediaAssets();

            var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(AssetPath);
            if (asset != null)
            {
                asset.EnsureDefaults();
                if (ShouldRefreshSample(asset))
                {
                    var refreshed = Create();
                    EditorUtility.CopySerialized(refreshed, asset);
                    UnityEngine.Object.DestroyImmediate(refreshed);
                    AuthoringAssetStore.Save(asset);
                }

                return asset;
            }

            EnsureFolder(Path.GetDirectoryName(AssetPath)?.Replace('\\', '/'));
            asset = Create();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AuthoringAssetStore.Save(asset);
            return asset;
        }

        private static void EnsureSampleMediaAssets()
        {
            CopySampleAsset("Simples/UI/test.jpg", MapImagePath);
            CopySampleAsset("Simples/Sounds/bgm.mp3", StationAudioPath);
            CopySampleAsset("Simples/Sounds/opendoor.mp3", DoorAudioPath);
        }

        private static void CopySampleAsset(string packageRelativePath, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath) ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath) != null)
            {
                return;
            }

            var sourcePath = GameDeveloperKitEditorPaths.PackageAssetPath(packageRelativePath);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sourcePath) == null)
            {
                return;
            }

            EnsureFolder(Path.GetDirectoryName(targetPath)?.Replace('\\', '/'));
            if (AssetDatabase.CopyAsset(sourcePath, targetPath) is false)
            {
                Debug.LogWarning($"Failed to copy Story sample asset: {sourcePath} -> {targetPath}");
            }
        }

        private static bool ShouldRefreshSample(AuthoringAsset asset)
        {
            if (asset == null || string.Equals(asset.StoryId, StoryId, StringComparison.Ordinal) is false)
            {
                return false;
            }

            if (HasInteractiveVideoSample(asset) is false)
            {
                return true;
            }

            if (asset.Volumes.Count != 2 ||
                asset.Volumes[0].Route == null ||
                asset.Volumes[0].Route.Edges.Count != 5 ||
                asset.Volumes[1].Route == null ||
                asset.Volumes[1].Route.Edges.Count != 1 ||
                FindEpisode(asset, SecondaryRootEpisodeId) == null)
            {
                return true;
            }

            var arrival = FindEpisode(asset, "episode_arrival");
            var parallel = FindNode(arrival, "arrival_parallel");
            var merge = FindNode(arrival, "arrival_merge");
            var video = FindNode(arrival, "arrival_video");
            var audio = FindNode(arrival, "arrival_audio");
            var intro = FindNode(arrival, "arrival_intro");
            if (parallel == null || merge == null || video == null || audio == null || intro == null)
            {
                return true;
            }

            var source = GetParameter(video, MediaCommandNames.VideoSourceArgument);
            var clip = GetParameter(video, "clip");
            var audioClip = GetParameter(audio, "clip");
            var text = GetParameter(intro, "textKey");
            return string.Equals(source, VideoSource, StringComparison.Ordinal) is false ||
                   string.Equals(clip, IntroVideoPath, StringComparison.Ordinal) is false ||
                   string.Equals(audioClip, StationAudioPath, StringComparison.Ordinal) is false ||
                   string.IsNullOrWhiteSpace(text) ||
                   text.StartsWith("story.", StringComparison.Ordinal);
        }

        private static string GetParameter(AuthoringNode node, string key)
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

        public static AuthoringEpisode FindEpisode(AuthoringAsset asset, string episodeId)
        {
            if (asset == null)
            {
                return null;
            }

            for (var i = 0; i < asset.Episodes.Count; i++)
            {
                var episode = asset.Episodes[i];
                if (episode != null && string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))
                {
                    return episode;
                }
            }

            return null;
        }

        public static AuthoringNode FindNode(AuthoringEpisode episode, string nodeId)
        {
            if (episode == null)
            {
                return null;
            }

            for (var i = 0; i < episode.Nodes.Count; i++)
            {
                var node = episode.Nodes[i];
                if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        public static AuthoringEdge FindEdge(AuthoringEpisode episode, string edgeId)
        {
            if (episode == null)
            {
                return null;
            }

            for (var i = 0; i < episode.Edges.Count; i++)
            {
                var edge = episode.Edges[i];
                if (edge != null && string.Equals(edge.EdgeId, edgeId, StringComparison.Ordinal))
                {
                    return edge;
                }
            }

            return null;
        }

        private static AuthoringEpisode CreateArrivalEpisode(AuthoringAsset asset)
        {
            var episode = Episode("episode_arrival", "雨夜抵达", "arrival_start");
            AddNodes(
                episode,
                Node("arrival_start", "开始", NodeKind.Start),
                Node("arrival_intro", "旁白：雨夜抵达", NodeKind.Narration, ("textKey", "黑雨压低了旧车站的灯光，站台尽头只剩一盏红色信号灯。")),
                Node("arrival_parallel", "并行：开场表现", NodeKind.Parallel),
                Node("arrival_video", "播放开场视频", NodeKind.PlayVideo, (MediaCommandNames.VideoSourceArgument, VideoSource), ("clip", IntroVideoPath), ("wait", "true")),
                Node("arrival_audio", "播放车站环境音", NodeKind.PlayAudio, ("clip", StationAudioPath)),
                Node("arrival_guard_line", "守卫对白", NodeKind.Dialogue, ("textKey", "站住。这里今晚不该有人来。"), ("speaker", "守卫")),
                Node("choice_enter_alley", "选择：进入暗巷", NodeKind.Choice, ("textKey", "绕开守卫进入暗巷")),
                Node("choice_help_guard", "选择：帮助守卫", NodeKind.Choice, ("textKey", "询问守卫发生了什么")),
                Node("arrival_merge", "等待全部完成：开场表现", NodeKind.Merge));
            AddEdges(
                episode,
                Edge("edge_arrival_start_intro", "arrival_start", "completed", "完成", TargetNode("arrival_intro")),
                Edge("edge_arrival_intro_parallel", "arrival_intro", "completed", "完成", TargetNode("arrival_parallel")),
                Edge("edge_arrival_parallel_video", "arrival_parallel", "branch_video", "视频轨", TargetNode("arrival_video")),
                Edge("edge_arrival_parallel_audio", "arrival_parallel", "branch_audio", "音频轨", TargetNode("arrival_audio")),
                Edge("edge_arrival_parallel_dialogue", "arrival_parallel", "branch_dialogue", "对白轨", TargetNode("arrival_guard_line")),
                Edge("edge_arrival_video_merge", "arrival_video", "completed", "完成", TargetNode("arrival_merge")),
                Edge("edge_arrival_audio_merge", "arrival_audio", "completed", "完成", TargetNode("arrival_merge")),
                Edge("edge_arrival_guard_merge", "arrival_guard_line", "completed", "完成", TargetNode("arrival_merge")),
                Edge("edge_arrival_merge_alley_choice", "arrival_merge", "completed", "进入选择", TargetNode("choice_enter_alley")),
                Edge("edge_arrival_merge_help_choice", "arrival_merge", "completed", "进入选择", TargetNode("choice_help_guard")));
            AddLayout(
                episode,
                ("arrival_start", 0f, 120f),
                ("arrival_intro", 220f, 120f),
                ("arrival_parallel", 440f, 120f),
                ("arrival_video", 700f, 0f),
                ("arrival_audio", 700f, 140f),
                ("arrival_guard_line", 700f, 280f),
                ("arrival_merge", 980f, 140f),
                ("choice_enter_alley", 1240f, 60f),
                ("choice_help_guard", 1240f, 220f));
            return episode;
        }

        private static AuthoringEpisode CreateStationEpisode(AuthoringAsset asset)
        {
            var episode = Episode("episode_station", "旧车站", "station_start");
            AddNodes(
                episode,
                Node("station_start", "开始", NodeKind.Start),
                Node("station_intro", "旁白：旧车站", NodeKind.Narration, ("textKey", "候车大厅空无一人，广播却还在重复播放一段旧通知。")),
                Node("station_audio", "播放车站环境音", NodeKind.PlayAudio, ("clip", StationAudioPath)),
                Node("station_line", "列车员对白", NodeKind.Dialogue, ("textKey", "拿着这枚徽章，别让检票口认出你。"), ("speaker", "列车员")),
                Node("choice_take_badge", "选择：收下徽章", NodeKind.Choice, ("textKey", "收下站台徽章")),
                Node("choice_refuse_badge", "选择：拒绝徽章", NodeKind.Choice, ("textKey", "拒绝并查看检票口")));
            AddEdges(
                episode,
                Edge("edge_station_start_intro", "station_start", "completed", "完成", TargetNode("station_intro")),
                Edge("edge_station_intro_audio", "station_intro", "completed", "完成", TargetNode("station_audio")),
                Edge("edge_station_audio_line", "station_audio", "completed", "完成", TargetNode("station_line")),
                Edge("edge_station_line_take", "station_line", "completed", "完成", TargetNode("choice_take_badge")),
                Edge("edge_station_line_refuse", "station_line", "completed", "完成", TargetNode("choice_refuse_badge")));
            AddLayout(
                episode,
                ("station_start", 0f, 120f),
                ("station_intro", 220f, 120f),
                ("station_audio", 440f, 120f),
                ("station_line", 660f, 120f),
                ("choice_take_badge", 900f, 40f),
                ("choice_refuse_badge", 900f, 220f));
            return episode;
        }

        private static AuthoringEpisode CreateAlleyEpisode(AuthoringAsset asset)
        {
            var episode = Episode("episode_alley", "暗巷", "alley_start");
            AddNodes(
                episode,
                Node("alley_start", "开始", NodeKind.Start),
                Node("alley_line", "陌生人对白", NodeKind.Dialogue, ("textKey", "门后不是出口，是另一个人的回忆。你确定要进去？"), ("speaker", "陌生人")),
                Node(
                    "alley_minigame",
                    "小游戏：撬锁",
                    NodeKind.Event,
                    (EventCommandCodec.EventIdParameter, "sample.minigame.lockpick"),
                    (EventCommandCodec.ModeParameter, EventCommandCodec.RequestMode)),
                Node("alley_door_audio", "播放开门声", NodeKind.PlayAudio, ("clip", DoorAudioPath)),
                Node("alley_video", "播放暗巷视频", NodeKind.PlayVideo, (MediaCommandNames.VideoSourceArgument, VideoSource), ("clip", AlleyVideoPath), ("wait", "true")),
                Node("alley_end", "结束", NodeKind.End));
            AddEdges(
                episode,
                Edge("edge_alley_start_line", "alley_start", "completed", "完成", TargetNode("alley_line")),
                Edge("edge_alley_line_minigame", "alley_line", "completed", "完成", TargetNode("alley_minigame")),
                Edge("edge_minigame_success_audio", "alley_minigame", "success", "成功", TargetNode("alley_door_audio")),
                Edge("edge_door_audio_video", "alley_door_audio", "completed", "完成", TargetNode("alley_video")),
                Edge("edge_alley_video_end", "alley_video", "completed", "完成", TargetNode("alley_end")),
                Edge("edge_minigame_fail_end", "alley_minigame", "fail", "失败", TargetNode("alley_end")),
                Edge("edge_minigame_cancel_end", "alley_minigame", "cancel", "取消", TargetNode("alley_end")));
            AddLayout(
                episode,
                ("alley_start", 0f, 140f),
                ("alley_line", 220f, 140f),
                ("alley_minigame", 460f, 140f),
                ("alley_door_audio", 700f, 80f),
                ("alley_video", 940f, 80f),
                ("alley_end", 1180f, 140f));
            return episode;
        }

        private static AuthoringEpisode CreateFinalEpisode(AuthoringAsset asset)
        {
            var settlement = SettlementPlanCodec.Serialize(new SettlementPlan(
                "sample.final",
                SettlementPlan.CurrentVersion,
                new[] { new SettlementOperation("complete", "sample.operation", new ArgumentBag()) }));
            var episode = Episode("episode_final", "余波", "final_start");
            AddNodes(
                episode,
                Node("final_start", "开始", NodeKind.Start),
                Node("final_intro", "旁白：雨停之后", NodeKind.Narration, ("textKey", "雨停后，站台上的影子终于恢复成普通人的形状。")),
                Node("final_line", "主角对白", NodeKind.Dialogue, ("textKey", "我记住这条路了。下一次，我会提前到。"), ("speaker", "主角")),
                Node(
                    "final_emit_event",
                    "发送事件：剧情结束",
                    NodeKind.Event,
                    (EventCommandCodec.EventIdParameter, "sample.story.completed"),
                    (EventCommandCodec.ModeParameter, EventCommandCodec.NotifyMode)),
                Node("final_wait", "等待收束", NodeKind.Wait, ("duration", "0.5")),
                Node("final_settlement", "剧情段结算", NodeKind.SettleEpisode, (SettlementCommandNames.PlanArgument, settlement)),
                Node("final_settlement_failed", "结算失败", NodeKind.Narration, ("textKey", "结算未完成，请稍后重试。")),
                Node("final_end", "结束", NodeKind.End));
            AddEdges(
                episode,
                Edge("edge_final_start_intro", "final_start", "completed", "完成", TargetNode("final_intro")),
                Edge("edge_final_intro_line", "final_intro", "completed", "完成", TargetNode("final_line")),
                Edge("edge_final_line_event", "final_line", "completed", "完成", TargetNode("final_emit_event")),
                Edge("edge_final_event_wait", "final_emit_event", "completed", "完成", TargetNode("final_wait")),
                Edge("edge_final_wait_settlement", "final_wait", "completed", "完成", TargetNode("final_settlement")),
                Edge("edge_final_settlement_end", "final_settlement", SettlementCommandNames.CompletedOutcome, "完成", TargetNode("final_end")),
                Edge("edge_final_settlement_failed", "final_settlement", SettlementCommandNames.FailedOutcome, "失败", TargetNode("final_settlement_failed")));
            AddLayout(
                episode,
                ("final_start", 0f, 120f),
                ("final_intro", 220f, 120f),
                ("final_line", 440f, 120f),
                ("final_emit_event", 660f, 120f),
                ("final_wait", 880f, 120f),
                ("final_settlement", 1100f, 120f),
                ("final_settlement_failed", 1320f, 240f),
                ("final_end", 1320f, 80f));
            return episode;
        }

        private static AuthoringEpisode CreateAfterRainEpisode()
        {
            var episode = Episode(SecondaryRootEpisodeId, "雨后余声", "after_rain_start");
            AddNodes(
                episode,
                Node("after_rain_start", "开始", NodeKind.Start),
                Node("after_rain_intro", "旁白：雨后", NodeKind.Narration, ("textKey", "雨水退进铁轨缝隙，清晨终于照亮远处的村庄。")),
                Node("after_rain_line", "主角对白", NodeKind.Dialogue, ("textKey", "这次的路已经结束，下一次从这里重新出发。"), ("speaker", "主角")),
                Node(
                    "after_rain_event",
                    "发送事件：卷结束",
                    NodeKind.Event,
                    (EventCommandCodec.EventIdParameter, "sample.story.completed"),
                    (EventCommandCodec.ModeParameter, EventCommandCodec.NotifyMode)),
                Node("after_rain_wait", "等待收束", NodeKind.Wait, ("duration", "0.25")),
                Node("after_rain_end", "结束", NodeKind.End));
            AddEdges(
                episode,
                Edge("edge_after_rain_start_intro", "after_rain_start", "completed", "完成", TargetNode("after_rain_intro")),
                Edge("edge_after_rain_intro_line", "after_rain_intro", "completed", "完成", TargetNode("after_rain_line")),
                Edge("edge_after_rain_line_event", "after_rain_line", "completed", "完成", TargetNode("after_rain_event")),
                Edge("edge_after_rain_event_wait", "after_rain_event", "completed", "完成", TargetNode("after_rain_wait")),
                Edge("edge_after_rain_wait_end", "after_rain_wait", "completed", "完成", TargetNode("after_rain_end")));
            AddLayout(
                episode,
                ("after_rain_start", 0f, 120f),
                ("after_rain_intro", 220f, 120f),
                ("after_rain_line", 440f, 120f),
                ("after_rain_event", 660f, 120f),
                ("after_rain_wait", 880f, 120f),
                ("after_rain_end", 1100f, 120f));
            return episode;
        }

        private static void BuildRouteAndLayouts(AuthoringVolume volume)
        {
            volume.Route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = IdentityId.RootEdge(RootEpisodeId),
                SourceKind = RouteEdgeSourceKind.Root,
                ToEpisodeId = RootEpisodeId
            });
            AddRouteEdge(volume, "episode_arrival", "choice_enter_alley", "episode_alley");
            AddRouteEdge(volume, "episode_arrival", "choice_help_guard", "episode_station");
            AddRouteEdge(volume, "episode_station", "choice_take_badge", "episode_final");
            AddRouteEdge(volume, "episode_station", "choice_refuse_badge", InteractiveVideoEpisodeId);

            volume.Layouts.Add(RouteLayout(
                "landscape",
                LayoutOrientation.Landscape,
                1920,
                1080,
                new Vector2(120f, 540f),
                ("episode_arrival", 430f, 540f),
                ("episode_alley", 900f, 250f),
                ("episode_station", 900f, 720f),
                ("episode_final", 1450f, 560f),
                (InteractiveVideoEpisodeId, 1450f, 850f)));
            volume.Layouts.Add(RouteLayout(
                "portrait",
                LayoutOrientation.Portrait,
                1080,
                1920,
                new Vector2(540f, 120f),
                ("episode_arrival", 540f, 420f),
                ("episode_alley", 280f, 820f),
                ("episode_station", 800f, 820f),
                ("episode_final", 650f, 1320f),
                (InteractiveVideoEpisodeId, 900f, 1600f)));

            for (var layoutIndex = 0; layoutIndex < volume.Layouts.Count; layoutIndex++)
            {
                var layout = volume.Layouts[layoutIndex];
                for (var edgeIndex = 0; edgeIndex < volume.Route.Edges.Count; edgeIndex++)
                {
                    var placement = new AuthoringRouteEdgePlacement { EdgeId = volume.Route.Edges[edgeIndex].EdgeId };
                    if (edgeIndex == 0)
                    {
                        placement.StyleKey = "main";
                        if (layout.Orientation == LayoutOrientation.Portrait)
                        {
                            placement.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(540f, 220f) });
                            placement.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(540f, 320f) });
                        }
                        else
                        {
                            placement.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(220f, 540f) });
                            placement.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(320f, 540f) });
                        }
                    }

                    layout.Edges.Add(placement);
                }
            }
        }

        private static void BuildSecondaryRouteAndLayouts(AuthoringVolume volume)
        {
            var edgeId = IdentityId.RootEdge(SecondaryRootEpisodeId);
            volume.Route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = edgeId,
                SourceKind = RouteEdgeSourceKind.Root,
                ToEpisodeId = SecondaryRootEpisodeId
            });
            var layout = RouteLayout(
                "landscape",
                LayoutOrientation.Landscape,
                1920,
                1080,
                new Vector2(220f, 540f),
                (SecondaryRootEpisodeId, 760f, 540f));
            var edge = new AuthoringRouteEdgePlacement { EdgeId = edgeId, StyleKey = "main" };
            edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(400f, 540f) });
            edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(580f, 540f) });
            layout.Edges.Add(edge);
            volume.Layouts.Add(layout);
        }

        private static void AddRouteEdge(
            AuthoringVolume volume,
            string fromEpisodeId,
            string fromExitId,
            string toEpisodeId)
        {
            volume.Route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = IdentityId.ExitEdge(fromEpisodeId, fromExitId),
                SourceKind = RouteEdgeSourceKind.EpisodeExit,
                FromEpisodeId = fromEpisodeId,
                FromExitId = fromExitId,
                ToEpisodeId = toEpisodeId
            });
        }

        private static AuthoringRouteLayout RouteLayout(
            string layoutId,
            LayoutOrientation orientation,
            int width,
            int height,
            Vector2 root,
            params (string episodeId, float x, float y)[] episodes)
        {
            var layout = new AuthoringRouteLayout
            {
                LayoutId = layoutId,
                Orientation = orientation,
                ReferenceWidth = width,
                ReferenceHeight = height,
                RootPlacement = new AuthoringPlacement { Position = root }
            };
            for (var i = 0; i < episodes.Length; i++)
            {
                layout.Episodes.Add(new AuthoringEpisodePlacement
                {
                    EpisodeId = episodes[i].episodeId,
                    Position = new AuthoringPlacement { Position = new Vector2(episodes[i].x, episodes[i].y) }
                });
            }

            return layout;
        }

        private static AuthoringEpisode Episode(string episodeId, string title, string entryNodeId)
        {
            return new AuthoringEpisode
            {
                EpisodeId = episodeId,
                Title = title,
                EntryNodeId = entryNodeId
            };
        }

        private static AuthoringNode Node(string nodeId, string title, NodeKind kind, params (string key, string value)[] parameters)
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

        private static void AddNodes(AuthoringEpisode episode, params AuthoringNode[] nodes)
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                episode.Nodes.Add(nodes[i]);
            }
        }

        private static void AddEdges(AuthoringEpisode episode, params AuthoringEdge[] edges)
        {
            for (var i = 0; i < edges.Length; i++)
            {
                episode.Edges.Add(edges[i]);
            }
        }

        private static AuthoringEdge Edge(
            string edgeId,
            string fromNodeId,
            string fromPortId,
            string fromPortLabel,
            (TransitionTargetKind kind, string nodeId) target,
            params AuthoringCondition[] conditions)
        {
            var edge = new AuthoringEdge
            {
                EdgeId = edgeId,
                FromNodeId = fromNodeId,
                FromPortId = fromPortId,
                FromPortLabel = fromPortLabel,
                TargetKind = target.kind,
                TargetNodeId = target.nodeId
            };
            for (var i = 0; i < conditions.Length; i++)
            {
                edge.Conditions.Add(conditions[i]);
            }

            return edge;
        }

        private static (TransitionTargetKind kind, string nodeId) TargetNode(string nodeId)
        {
            return (TransitionTargetKind.Node, nodeId);
        }

        private static void AddLayout(AuthoringEpisode episode, params (string nodeId, float x, float y)[] nodes)
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                episode.DetailLayout.Nodes.Add(new EpisodeNodePlacement
                {
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

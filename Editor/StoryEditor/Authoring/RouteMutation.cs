using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.Authoring
{
    public sealed partial class RouteMutation
    {
        public const string UnknownVolume = "unknown_volume";
        public const string UnknownEpisode = "unknown_episode";
        public const string UnknownExit = "unknown_exit";
        public const string ExitAlreadyBound = "exit_already_bound";
        public const string EpisodeHasChildren = "episode_has_children";
        public const string MultipleIncoming = "multiple_incoming";
        public const string RouteCycle = "route_cycle";
        public const string RootImmutable = "root_immutable";
        public const string PublishedIdentityRemoval = "published_identity_removal";
        public const string InvalidLayout = "invalid_layout";

        private readonly AuthoringAsset m_Asset;

        public RouteMutation(AuthoringAsset asset)
        {
            m_Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        public RouteMutationResult AddRootEpisode(string volumeId, EpisodeMetadata metadata)
        {
            var volume = FindVolume(volumeId);
            if (volume == null)
            {
                return Fail(UnknownVolume, $"卷不存在：{volumeId}");
            }

            if (TryCreateRouteSnapshot(volume, out var route, out var failure) is false)
            {
                return failure;
            }

            var episode = CreateEpisode(metadata);
            var edgeId = IdentityId.RootEdge(episode.EpisodeId);
            route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = edgeId,
                SourceKind = RouteEdgeSourceKind.Root,
                ToEpisodeId = episode.EpisodeId
            });

            var episodes = CopyEpisodes(volume, episode);
            failure = ValidateCandidate(volume, episodes, route);
            if (failure.Succeeded is false)
            {
                return failure;
            }

            if (!LayoutSynchronizer.TryAdd(
                    volume,
                    episodes,
                    route,
                    episode.EpisodeId,
                    edgeId,
                    null,
                    out var layouts,
                    out var layoutError))
            {
                return Fail(InvalidLayout, layoutError);
            }

            AuthoringUndo.Mutate(m_Asset, "Add Root Episode", () =>
            {
                volume.Route = route;
                volume.Episodes.Add(episode);
                LayoutCopies.Replace(volume.Layouts, layouts);
            });
            return RouteMutationResult.Success("已添加首层剧情段。", episode.EpisodeId, edgeId);
        }

        public RouteMutationResult AddChildEpisode(
            string volumeId,
            string fromEpisodeId,
            string exitId,
            EpisodeMetadata metadata)
        {
            var volume = FindVolume(volumeId);
            if (volume == null)
            {
                return Fail(UnknownVolume, $"卷不存在：{volumeId}");
            }

            var sourceEpisode = FindEpisode(volume, fromEpisodeId);
            if (sourceEpisode == null)
            {
                return Fail(UnknownEpisode, $"来源剧情段不存在：{fromEpisodeId}");
            }

            if (DeclaresExit(sourceEpisode, exitId) is false)
            {
                return Fail(UnknownExit, $"剧情段出口不存在：{fromEpisodeId}/{exitId}");
            }

            if (TryCreateRouteSnapshot(volume, out var route, out var routeFailure) is false)
            {
                return routeFailure;
            }

            if (FindBoundExit(route, fromEpisodeId, exitId) != null)
            {
                return Fail(ExitAlreadyBound, $"剧情段出口已绑定：{fromEpisodeId}/{exitId}");
            }

            var episode = CreateEpisode(metadata);
            var edgeId = IdentityId.ExitEdge(fromEpisodeId, exitId);
            route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = edgeId,
                SourceKind = RouteEdgeSourceKind.EpisodeExit,
                FromEpisodeId = fromEpisodeId,
                FromExitId = exitId,
                ToEpisodeId = episode.EpisodeId
            });

            var episodes = CopyEpisodes(volume, episode);
            var failure = ValidateCandidate(volume, episodes, route);
            if (failure.Succeeded is false)
            {
                return failure;
            }

            if (!LayoutSynchronizer.TryAdd(
                    volume,
                    episodes,
                    route,
                    episode.EpisodeId,
                    edgeId,
                    fromEpisodeId,
                    out var layouts,
                    out var layoutError))
            {
                return Fail(InvalidLayout, layoutError);
            }

            AuthoringUndo.Mutate(m_Asset, "Add Child Episode", () =>
            {
                volume.Route = route;
                volume.Episodes.Add(episode);
                LayoutCopies.Replace(volume.Layouts, layouts);
            });
            return RouteMutationResult.Success("已添加后续剧情段。", episode.EpisodeId, edgeId);
        }

        public RouteMutationResult RemoveLeafEpisode(
            string volumeId,
            string episodeId,
            bool confirmPublishedIdentityRemoval)
        {
            var volume = FindVolume(volumeId);
            if (volume == null)
            {
                return Fail(UnknownVolume, $"卷不存在：{volumeId}");
            }

            var episode = FindEpisode(volume, episodeId);
            if (episode == null)
            {
                return Fail(UnknownEpisode, $"剧情段不存在：{episodeId}");
            }

            var replacementEntryId = FindRemainingEpisodeId(episode);
            if (replacementEntryId == null)
            {
                return Fail(RootImmutable, "剧情资产至少需要保留一个剧情段。");
            }

            if (TryCreateRouteSnapshot(volume, out var route, out var failure) is false)
            {
                return failure;
            }

            failure = ValidateCandidate(volume, volume.Episodes, route);
            if (failure.Succeeded is false)
            {
                return failure;
            }

            if (HasChildren(route, episodeId))
            {
                return Fail(EpisodeHasChildren, $"剧情段仍有后续分支，不能删除：{episodeId}");
            }

            var incoming = FindIncomingEdges(route, episodeId);
            if (incoming.Count != 1)
            {
                return Fail(MultipleIncoming, $"剧情段必须恰好有一条入边：{episodeId}");
            }

            if (confirmPublishedIdentityRemoval is false &&
                RemovesPublishedIdentity(episode, incoming[0], out var publishedMessage))
            {
                return Fail(PublishedIdentityRemoval, publishedMessage);
            }

            route.Edges.Remove(incoming[0]);
            var remainingEpisodes = new List<AuthoringEpisode>(volume.Episodes);
            remainingEpisodes.Remove(episode);
            if (!LayoutSynchronizer.TryRemove(
                    volume,
                    remainingEpisodes,
                    route,
                    episodeId,
                    incoming[0].EdgeId,
                    out var layouts,
                    out var layoutError))
            {
                return Fail(InvalidLayout, layoutError);
            }

            AuthoringUndo.Mutate(m_Asset, "Remove Leaf Episode", () =>
            {
                volume.Route = route;
                volume.Episodes.Remove(episode);
                LayoutCopies.Replace(volume.Layouts, layouts);
            });
            return RouteMutationResult.Success("已删除叶子剧情段。", episodeId, incoming[0].EdgeId);
        }

        public RouteMutationResult UpdateEpisode(
            string volumeId,
            string episodeId,
            EpisodeMetadata metadata)
        {
            var volume = FindVolume(volumeId);
            if (volume == null)
            {
                return Fail(UnknownVolume, $"卷不存在：{volumeId}");
            }

            var episode = FindEpisode(volume, episodeId);
            if (episode == null)
            {
                return Fail(UnknownEpisode, $"剧情段不存在：{episodeId}");
            }

            AuthoringUndo.Mutate(m_Asset, "Update Episode Metadata", () =>
            {
                episode.Title = metadata.Title;
                episode.Description = metadata.Description;
                episode.PreviewImage = metadata.PreviewImage;
            });
            return RouteMutationResult.Success("已更新剧情段属性。", episodeId);
        }

        public RouteMutationResult UpdateVolume(string volumeId, VolumeMetadata metadata)
        {
            var volume = FindVolume(volumeId);
            if (volume == null)
            {
                return Fail(UnknownVolume, $"卷不存在：{volumeId}");
            }

            AuthoringUndo.Mutate(m_Asset, "Update Volume Metadata", () =>
            {
                volume.Title = metadata.Title;
                volume.Description = metadata.Description;
                volume.PreviewImage = metadata.PreviewImage;
            });
            return RouteMutationResult.Success("已更新卷属性。");
        }

        private bool TryCreateRouteSnapshot(
            AuthoringVolume volume,
            out AuthoringRoute route,
            out RouteMutationResult failure)
        {
            if (volume.Route != null)
            {
                route = CopyRoute(volume.Route);
                failure = default;
                return true;
            }

            if (volume.Episodes.Count == 0)
            {
                route = new AuthoringRoute();
                failure = default;
                return true;
            }

            route = null;
            failure = Fail(InvalidLayout, "卷路线缺失，请先运行 Story 路线迁移。");
            return false;
        }

        private AuthoringVolume FindVolume(string volumeId)
        {
            for (var i = 0; i < m_Asset.Volumes.Count; i++)
            {
                var volume = m_Asset.Volumes[i];
                if (volume != null && string.Equals(volume.VolumeId, volumeId, StringComparison.Ordinal))
                {
                    return volume;
                }
            }

            return null;
        }

        private static AuthoringEpisode FindEpisode(AuthoringVolume volume, string episodeId)
        {
            for (var i = 0; i < (volume?.Episodes.Count ?? 0); i++)
            {
                var episode = volume.Episodes[i];
                if (episode != null && string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))
                {
                    return episode;
                }
            }

            return null;
        }

        private static AuthoringEpisode CreateEpisode(EpisodeMetadata metadata)
        {
            var episodeId = IdentityId.New();
            var startId = IdentityId.New();
            var endId = IdentityId.New();
            var episode = new AuthoringEpisode
            {
                EpisodeId = episodeId,
                Title = metadata.Title,
                Description = metadata.Description,
                PreviewImage = metadata.PreviewImage,
                EntryNodeId = startId
            };
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = startId,
                Title = "开始",
                NodeKind = NodeKind.Start
            });
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = endId,
                Title = "结束",
                NodeKind = NodeKind.End
            });
            episode.Edges.Add(new AuthoringEdge
            {
                EdgeId = IdentityId.New(),
                FromNodeId = startId,
                FromPortId = "completed",
                FromPortLabel = "完成",
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = endId
            });
            return episode;
        }

        private static List<AuthoringEpisode> CopyEpisodes(
            AuthoringVolume volume,
            AuthoringEpisode addedEpisode)
        {
            var episodes = new List<AuthoringEpisode>(volume.Episodes.Count + 1);
            episodes.AddRange(volume.Episodes);
            episodes.Add(addedEpisode);
            return episodes;
        }

        private string FindRemainingEpisodeId(AuthoringEpisode removedEpisode)
        {
            for (var volumeIndex = 0; volumeIndex < m_Asset.Volumes.Count; volumeIndex++)
            {
                var volume = m_Asset.Volumes[volumeIndex];
                for (var episodeIndex = 0; episodeIndex < (volume?.Episodes.Count ?? 0); episodeIndex++)
                {
                    var episode = volume.Episodes[episodeIndex];
                    if (episode != null && ReferenceEquals(episode, removedEpisode) is false)
                    {
                        return episode.EpisodeId;
                    }
                }
            }

            return null;
        }

        private static AuthoringRoute CopyRoute(AuthoringRoute source)
        {
            var copy = new AuthoringRoute();
            for (var i = 0; i < (source?.Edges.Count ?? 0); i++)
            {
                copy.Edges.Add(CopyEdge(source.Edges[i]));
            }

            return copy;
        }

        private static AuthoringRoute CopyRoute(Route source)
        {
            var copy = new AuthoringRoute();
            for (var i = 0; i < (source?.Edges.Count ?? 0); i++)
            {
                var edge = source.Edges[i];
                copy.Edges.Add(new AuthoringRouteEdge
                {
                    EdgeId = edge.EdgeId,
                    SourceKind = edge.SourceKind,
                    FromEpisodeId = edge.FromEpisodeId,
                    FromExitId = edge.FromExitId,
                    ToEpisodeId = edge.ToEpisodeId
                });
            }

            return copy;
        }

        private static AuthoringRouteEdge CopyEdge(AuthoringRouteEdge source)
        {
            return source == null
                ? null
                : new AuthoringRouteEdge
                {
                    EdgeId = source.EdgeId,
                    SourceKind = source.SourceKind,
                    FromEpisodeId = source.FromEpisodeId,
                    FromExitId = source.FromExitId,
                    ToEpisodeId = source.ToEpisodeId
                };
        }

        private static AuthoringRouteEdge FindBoundExit(
            AuthoringRoute route,
            string episodeId,
            string exitId)
        {
            for (var i = 0; i < route.Edges.Count; i++)
            {
                var edge = route.Edges[i];
                if (edge != null &&
                    edge.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                    string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal) &&
                    string.Equals(edge.FromExitId, exitId, StringComparison.Ordinal))
                {
                    return edge;
                }
            }

            return null;
        }

        private static bool HasChildren(AuthoringRoute route, string episodeId)
        {
            for (var i = 0; i < route.Edges.Count; i++)
            {
                var edge = route.Edges[i];
                if (edge != null &&
                    edge.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                    string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<AuthoringRouteEdge> FindIncomingEdges(AuthoringRoute route, string episodeId)
        {
            var edges = new List<AuthoringRouteEdge>();
            for (var i = 0; i < route.Edges.Count; i++)
            {
                var edge = route.Edges[i];
                if (edge != null && string.Equals(edge.ToEpisodeId, episodeId, StringComparison.Ordinal))
                {
                    edges.Add(edge);
                }
            }

            return edges;
        }

        private static RouteMutationResult Fail(string errorCode, string message)
        {
            return RouteMutationResult.Failure(errorCode, message);
        }
    }
}

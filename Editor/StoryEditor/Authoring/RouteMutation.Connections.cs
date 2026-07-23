using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.Authoring
{
    public sealed partial class RouteMutation
    {
        public RouteMutationResult ValidateConnection(
            string volumeId,
            string fromEpisodeId,
            string exitId,
            string toEpisodeId)
        {
            return TryPrepareConnection(
                volumeId,
                fromEpisodeId,
                exitId,
                toEpisodeId,
                out _,
                out _,
                out _,
                out var failure)
                ? RouteMutationResult.Success("可以连接路线。")
                : failure;
        }

        public RouteMutationResult Connect(
            string volumeId,
            string fromEpisodeId,
            string exitId,
            string toEpisodeId)
        {
            if (TryPrepareConnection(
                    volumeId,
                    fromEpisodeId,
                    exitId,
                    toEpisodeId,
                    out var volume,
                    out var route,
                    out var edge,
                    out var failure) is false)
            {
                return failure;
            }

            var added = FindBoundExit(volume.Route, fromEpisodeId, exitId) == null;
            List<AuthoringRouteLayout> layouts;
            string layoutError;
            if (added)
            {
                if (LayoutSynchronizer.TryAddEdge(
                        volume,
                        volume.Episodes,
                        route,
                        edge.EdgeId,
                        out layouts,
                        out layoutError) is false)
                {
                    return Fail(InvalidLayout, layoutError);
                }
            }
            else
            {
                layouts = LayoutCopies.CopyAll(volume.Layouts);
                if (LayoutSynchronizer.TryValidate(
                        volume,
                        volume.Episodes,
                        route,
                        layouts,
                        out layoutError) is false)
                {
                    return Fail(InvalidLayout, layoutError);
                }
            }

            AuthoringUndo.Mutate(UndoTarget(volume), added ? "Connect Route Edge" : "Redirect Route Edge", () =>
            {
                volume.Route = route;
                LayoutCopies.Replace(volume.Layouts, layouts);
            });
            return RouteMutationResult.Success(
                added ? "已连接剧情段路线。" : "已重定向剧情段路线。",
                toEpisodeId,
                edge.EdgeId);
        }

        public RouteMutationResult Disconnect(
            string volumeId,
            string edgeId,
            bool confirmPublishedIdentityRemoval)
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

            var edge = FindEdge(route, edgeId);
            if (edge == null)
            {
                return Fail(UnknownEdge, $"路线边不存在：{edgeId}");
            }

            if (confirmPublishedIdentityRemoval is false &&
                RemovesPublishedEdgeIdentity(edgeId, out var publishedMessage))
            {
                return Fail(PublishedIdentityRemoval, publishedMessage);
            }

            route.Edges.Remove(edge);
            failure = ValidateCandidate(volume, volume.Episodes, route);
            if (failure.Succeeded is false)
            {
                return failure;
            }

            if (LayoutSynchronizer.TryRemoveEdge(
                    volume,
                    volume.Episodes,
                    route,
                    edgeId,
                    out var layouts,
                    out var layoutError) is false)
            {
                return Fail(InvalidLayout, layoutError);
            }

            AuthoringUndo.Mutate(UndoTarget(volume), "Disconnect Route Edge", () =>
            {
                volume.Route = route;
                LayoutCopies.Replace(volume.Layouts, layouts);
            });
            return RouteMutationResult.Success("已断开剧情段路线。", edge.ToEpisodeId, edgeId);
        }

        private bool TryPrepareConnection(
            string volumeId,
            string fromEpisodeId,
            string exitId,
            string toEpisodeId,
            out AuthoringVolume volume,
            out AuthoringRoute route,
            out AuthoringRouteEdge edge,
            out RouteMutationResult failure)
        {
            volume = FindVolume(volumeId);
            route = null;
            edge = null;
            if (volume == null)
            {
                failure = Fail(UnknownVolume, $"卷不存在：{volumeId}");
                return false;
            }

            var source = FindEpisode(volume, fromEpisodeId);
            if (source == null)
            {
                failure = Fail(UnknownEpisode, $"来源剧情段不存在：{fromEpisodeId}");
                return false;
            }

            if (FindEpisode(volume, toEpisodeId) == null)
            {
                failure = Fail(UnknownEpisode, $"目标剧情段不存在：{toEpisodeId}");
                return false;
            }

            if (string.Equals(fromEpisodeId, toEpisodeId, StringComparison.Ordinal))
            {
                failure = Fail(RouteCycle, "剧情段不能连接到自身。");
                return false;
            }

            if (DeclaresExit(source, exitId) is false)
            {
                failure = Fail(UnknownExit, $"剧情段出口不存在：{fromEpisodeId}/{exitId}");
                return false;
            }

            if (TryCreateRouteSnapshot(volume, out route, out failure) is false)
            {
                return false;
            }

            edge = FindBoundExit(route, fromEpisodeId, exitId);
            if (edge != null && string.Equals(edge.ToEpisodeId, toEpisodeId, StringComparison.Ordinal))
            {
                failure = Fail(ExitAlreadyBound, $"剧情段出口已连接该目标：{fromEpisodeId}/{exitId}");
                return false;
            }

            if (edge == null)
            {
                edge = new AuthoringRouteEdge
                {
                    EdgeId = IdentityId.ExitEdge(fromEpisodeId, exitId),
                    SourceKind = RouteEdgeSourceKind.EpisodeExit,
                    FromEpisodeId = fromEpisodeId,
                    FromExitId = exitId,
                    ToEpisodeId = toEpisodeId
                };
                route.Edges.Add(edge);
            }
            else
            {
                edge.ToEpisodeId = toEpisodeId;
            }

            failure = ValidateCandidate(volume, volume.Episodes, route);
            return failure.Succeeded;
        }

        private static AuthoringRouteEdge FindEdge(AuthoringRoute route, string edgeId)
        {
            for (var i = 0; i < (route?.Edges.Count ?? 0); i++)
            {
                if (string.Equals(route.Edges[i]?.EdgeId, edgeId, StringComparison.Ordinal))
                {
                    return route.Edges[i];
                }
            }

            return null;
        }
    }
}

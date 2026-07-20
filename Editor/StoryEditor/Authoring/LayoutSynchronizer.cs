using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Authoring
{
    internal static class LayoutSynchronizer
    {
        public static bool TryAdd(
            AuthoringVolume volume,
            IReadOnlyList<AuthoringEpisode> episodes,
            AuthoringRoute route,
            string episodeId,
            string edgeId,
            string fromEpisodeId,
            out List<AuthoringRouteLayout> layouts,
            out string error)
        {
            layouts = LayoutCopies.CopyAll(volume?.Layouts);
            for (var i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                if (layout?.RootPlacement == null)
                {
                    error = $"布局缺少虚拟根位置：{layout?.LayoutId}";
                    return false;
                }

                var origin = layout.RootPlacement.Position;
                if (string.IsNullOrWhiteSpace(fromEpisodeId) is false)
                {
                    var source = FindEpisode(layout, fromEpisodeId);
                    if (source?.Position == null)
                    {
                        error = $"布局缺少来源剧情段位置：{layout.LayoutId}/{fromEpisodeId}";
                        return false;
                    }

                    origin = source.Position.Position;
                }

                const float offsetX = 0.18f;
                var offsetY = ((layout.Episodes.Count % 5) - 2) * 0.075f;
                var position = layout.Orientation == LayoutOrientation.Portrait
                    ? new Vector2(Mathf.Clamp01(origin.x + offsetY), origin.y + offsetX)
                    : layout.Orientation == LayoutOrientation.Landscape
                        ? new Vector2(origin.x + offsetX, Mathf.Clamp01(origin.y + offsetY))
                        : new Vector2(origin.x + offsetX, origin.y + offsetY);
                layout.Episodes.Add(new AuthoringEpisodePlacement
                {
                    EpisodeId = episodeId,
                    Position = new AuthoringPlacement { Position = position }
                });
                layout.Edges.Add(new AuthoringRouteEdgePlacement { EdgeId = edgeId });
            }

            return TryValidate(volume, episodes, route, layouts, out error);
        }

        public static bool TryRemove(
            AuthoringVolume volume,
            IReadOnlyList<AuthoringEpisode> episodes,
            AuthoringRoute route,
            string episodeId,
            string edgeId,
            out List<AuthoringRouteLayout> layouts,
            out string error)
        {
            layouts = LayoutCopies.CopyAll(volume?.Layouts);
            for (var i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                if (layout == null ||
                    layout.Episodes.RemoveAll(x => x != null && x.EpisodeId == episodeId) != 1 ||
                    layout.Edges.RemoveAll(x => x != null && x.EdgeId == edgeId) != 1)
                {
                    error = $"布局缺少待删除的剧情段或路线边：{layout?.LayoutId}/{episodeId}/{edgeId}";
                    return false;
                }
            }

            return TryValidate(volume, episodes, route, layouts, out error);
        }

        public static bool TryValidate(
            AuthoringVolume volume,
            IReadOnlyList<AuthoringEpisode> episodes,
            AuthoringRoute route,
            IReadOnlyList<AuthoringRouteLayout> layouts,
            out string error)
        {
            if ((layouts?.Count ?? 0) == 0)
            {
                error = null;
                return true;
            }

            var candidate = new AuthoringVolume { VolumeId = volume?.VolumeId };
            candidate.Layouts.AddRange(LayoutCopies.CopyAll(layouts));
            var runtimeEpisodes = new List<Episode>();
            for (var i = 0; i < (episodes?.Count ?? 0); i++)
            {
                var episode = episodes[i];
                if (episode != null && string.IsNullOrWhiteSpace(episode.EpisodeId) is false)
                {
                    runtimeEpisodes.Add(new Episode(
                        episode.EpisodeId,
                        episode.Title,
                        "start",
                        Array.Empty<EpisodeExit>(),
                        Array.Empty<Step>()));
                }
            }

            var runtimeEdges = new List<RouteEdge>();
            for (var i = 0; i < (route?.Edges.Count ?? 0); i++)
            {
                var edge = route.Edges[i];
                if (edge == null)
                {
                    continue;
                }

                runtimeEdges.Add(edge.SourceKind == RouteEdgeSourceKind.Root
                    ? RouteEdge.FromRoot(edge.EdgeId, edge.ToEpisodeId)
                    : RouteEdge.FromExit(edge.EdgeId, edge.FromEpisodeId, edge.FromExitId, edge.ToEpisodeId));
            }

            var report = new ValidationReport();
            LayoutCompiler.Compile(
                "layout-mutation",
                candidate,
                runtimeEpisodes,
                new Route(runtimeEdges),
                report);
            error = report.Issues.Count == 0 ? null : report.Issues[0].Message;
            return report.HasErrors is false;
        }

        private static AuthoringEpisodePlacement FindEpisode(AuthoringRouteLayout layout, string episodeId)
        {
            for (var i = 0; i < (layout?.Episodes.Count ?? 0); i++)
            {
                if (layout.Episodes[i] != null &&
                    string.Equals(layout.Episodes[i].EpisodeId, episodeId, StringComparison.Ordinal))
                {
                    return layout.Episodes[i];
                }
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;

namespace GameDeveloperKit.StoryEditor.Compiler
{
    internal static class RouteCompiler
    {
        public static Route Compile(
            AuthoringAsset asset,
            AuthoringVolume volume,
            IReadOnlyList<Episode> episodes,
            ISet<string> programEdgeIds,
            ValidationReport report)
        {
            return Compile(
                asset?.StoryId,
                volume,
                episodes,
                programEdgeIds,
                report);
        }

        internal static Route Compile(
            string storyId,
            AuthoringVolume volume,
            IReadOnlyList<Episode> episodes,
            ISet<string> programEdgeIds,
            ValidationReport report)
        {
            if (volume?.Route == null)
            {
                report.AddError(
                    $"story:{storyId}/volume:{volume?.VolumeId}/route",
                    "Volume Route is missing. Run the explicit Story route migration before compiling this asset.");
                return new Route(Array.Empty<RouteEdge>());
            }

            var route = BuildExplicit(storyId, volume, report);
            Validate(storyId, volume, episodes, route, programEdgeIds, report);
            return route;
        }

        private static Route BuildExplicit(
            string storyId,
            AuthoringVolume volume,
            ValidationReport report)
        {
            var edges = new List<RouteEdge>();
            for (var i = 0; i < volume.Route.Edges.Count; i++)
            {
                var source = volume.Route.Edges[i];
                var location = $"story:{storyId}/volume:{volume.VolumeId}/route/edge[{i}]";
                if (source == null)
                {
                    report.AddError(location, "Route edge cannot be null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(source.EdgeId) || string.IsNullOrWhiteSpace(source.ToEpisodeId))
                {
                    report.AddError(location, "Route edge id and target episode id cannot be empty.");
                    continue;
                }

                switch (source.SourceKind)
                {
                    case RouteEdgeSourceKind.Root:
                        if (string.IsNullOrWhiteSpace(source.FromEpisodeId) is false ||
                            string.IsNullOrWhiteSpace(source.FromExitId) is false)
                        {
                            report.AddError(location, "Root route edge cannot declare an Episode exit.");
                            continue;
                        }

                        edges.Add(RouteEdge.FromRoot(source.EdgeId, source.ToEpisodeId));
                        break;
                    case RouteEdgeSourceKind.EpisodeExit:
                        if (string.IsNullOrWhiteSpace(source.FromEpisodeId) ||
                            string.IsNullOrWhiteSpace(source.FromExitId))
                        {
                            report.AddError(location, "Episode route edge must declare source Episode and Exit ids.");
                            continue;
                        }

                        edges.Add(RouteEdge.FromExit(
                            source.EdgeId,
                            source.FromEpisodeId,
                            source.FromExitId,
                            source.ToEpisodeId));
                        break;
                    default:
                        report.AddError(location, $"Route edge source kind is invalid. kind:{source.SourceKind}");
                        break;
                }
            }

            return new Route(edges);
        }

        private static void Validate(
            string storyId,
            AuthoringVolume volume,
            IReadOnlyList<Episode> episodes,
            Route route,
            ISet<string> programEdgeIds,
            ValidationReport report)
        {
            var location = $"story:{storyId}/volume:{volume?.VolumeId}/route";
            if (episodes == null || episodes.Count == 0)
            {
                report.AddError(location, "Volume route requires at least one Episode.");
                return;
            }

            var episodeLookup = new Dictionary<string, Episode>(StringComparer.Ordinal);
            var exits = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (var i = 0; i < episodes.Count; i++)
            {
                var episode = episodes[i];
                if (episode == null || string.IsNullOrWhiteSpace(episode.EpisodeId))
                {
                    continue;
                }

                episodeLookup[episode.EpisodeId] = episode;
                incoming[episode.EpisodeId] = 0;
                outgoing[episode.EpisodeId] = new List<string>();
                var episodeExits = new HashSet<string>(StringComparer.Ordinal);
                for (var exitIndex = 0; exitIndex < episode.Exits.Count; exitIndex++)
                {
                    episodeExits.Add(episode.Exits[exitIndex].ExitId);
                }

                exits[episode.EpisodeId] = episodeExits;
            }

            var roots = new List<string>();
            var boundExits = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < route.Edges.Count; i++)
            {
                var edge = route.Edges[i];
                var edgeLocation = $"{location}/edge:{edge.EdgeId}";
                if (programEdgeIds.Add(edge.EdgeId) is false)
                {
                    report.AddError(edgeLocation, "Route edge id must be unique in the Program.");
                }

                if (episodeLookup.ContainsKey(edge.ToEpisodeId) is false)
                {
                    report.AddError(edgeLocation, $"Route target Episode does not exist. episode:{edge.ToEpisodeId}");
                    continue;
                }

                incoming[edge.ToEpisodeId]++;
                if (incoming[edge.ToEpisodeId] > 1)
                {
                    report.AddError(edgeLocation, $"Episode cannot have multiple incoming RouteEdges. episode:{edge.ToEpisodeId}");
                }

                if (edge.SourceKind == RouteEdgeSourceKind.Root)
                {
                    roots.Add(edge.ToEpisodeId);
                    continue;
                }

                if (episodeLookup.ContainsKey(edge.FromEpisodeId) is false)
                {
                    report.AddError(edgeLocation, $"Route source Episode does not exist. episode:{edge.FromEpisodeId}");
                    continue;
                }

                if (exits[edge.FromEpisodeId].Contains(edge.FromExitId) is false)
                {
                    report.AddError(
                        edgeLocation,
                        $"Route source Exit does not exist. episode:{edge.FromEpisodeId} exit:{edge.FromExitId}");
                    continue;
                }

                var exitKey = edge.FromEpisodeId + "\n" + edge.FromExitId;
                if (boundExits.Add(exitKey) is false)
                {
                    report.AddError(
                        edgeLocation,
                        $"Episode Exit cannot bind multiple RouteEdges. episode:{edge.FromEpisodeId} exit:{edge.FromExitId}");
                }

                outgoing[edge.FromEpisodeId].Add(edge.ToEpisodeId);
            }

            foreach (var pair in incoming)
            {
                if (pair.Value != 1)
                {
                    report.AddError(location, $"Episode requires exactly one incoming RouteEdge. episode:{pair.Key}");
                }
            }

            if (roots.Count == 0)
            {
                report.AddError(location, "Volume route requires at least one root RouteEdge.");
            }

            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var episodeId in episodeLookup.Keys)
            {
                Visit(episodeId, outgoing, states, location, report);
            }

            var reachable = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < roots.Count; i++)
            {
                MarkReachable(roots[i], outgoing, reachable);
            }

            foreach (var episodeId in episodeLookup.Keys)
            {
                if (reachable.Contains(episodeId) is false)
                {
                    report.AddError(location, $"Episode is not reachable from the virtual root. episode:{episodeId}");
                }
            }
        }

        private static void Visit(
            string episodeId,
            IReadOnlyDictionary<string, List<string>> outgoing,
            IDictionary<string, int> states,
            string location,
            ValidationReport report)
        {
            if (states.TryGetValue(episodeId, out var state))
            {
                if (state == 1)
                {
                    report.AddError(location, $"Volume route cannot contain a cycle. episode:{episodeId}");
                }

                return;
            }

            states[episodeId] = 1;
            var children = outgoing[episodeId];
            for (var i = 0; i < children.Count; i++)
            {
                Visit(children[i], outgoing, states, location, report);
            }

            states[episodeId] = 2;
        }

        private static void MarkReachable(
            string episodeId,
            IReadOnlyDictionary<string, List<string>> outgoing,
            ISet<string> reachable)
        {
            if (outgoing.ContainsKey(episodeId) is false || reachable.Add(episodeId) is false)
            {
                return;
            }

            var children = outgoing[episodeId];
            for (var i = 0; i < children.Count; i++)
            {
                MarkReachable(children[i], outgoing, reachable);
            }
        }

    }
}

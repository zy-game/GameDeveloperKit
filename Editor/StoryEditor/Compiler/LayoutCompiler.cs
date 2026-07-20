using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Compiler
{
    internal static class LayoutCompiler
    {
        public static IReadOnlyList<RouteLayout> Compile(
            string storyId,
            AuthoringVolume volume,
            IReadOnlyList<Episode> episodes,
            Route route,
            ValidationReport report)
        {
            var result = new List<RouteLayout>();
            var layoutIds = new HashSet<string>(StringComparer.Ordinal);
            var episodeIds = BuildEpisodeIds(episodes);
            var edgeIds = BuildEdgeIds(route);
            for (var i = 0; i < (volume?.Layouts.Count ?? 0); i++)
            {
                var source = volume.Layouts[i];
                var location = $"story:{storyId}/volume:{volume?.VolumeId}/layout[{i}]";
                if (source == null)
                {
                    report.AddError(location, "Route layout cannot be null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(source.LayoutId))
                {
                    report.AddError(location, "Route layout id cannot be empty.");
                    continue;
                }

                location = $"story:{storyId}/volume:{volume.VolumeId}/layout:{source.LayoutId}";
                if (!layoutIds.Add(source.LayoutId))
                {
                    report.AddError(location, "Route layout id must be unique in the Volume.");
                    continue;
                }

                if (!Enum.IsDefined(typeof(LayoutOrientation), source.Orientation))
                {
                    report.AddError(location, $"Route layout orientation is invalid. orientation:{source.Orientation}");
                }

                source.EnsureRelativeCoordinates();
                var root = CompilePlacement(source.RootPlacement, source, location + "/root", report);
                var compiledEpisodes = CompileEpisodes(source, episodeIds, location, report);
                var compiledEdges = CompileEdges(source, edgeIds, location, report);
                var backgroundPath = ResolveBackgroundPath(source.BackgroundImage, location, report);
                result.Add(new RouteLayout(
                    source.LayoutId,
                    source.Orientation,
                    backgroundPath,
                    root,
                    compiledEpisodes,
                    compiledEdges));
            }

            return result;
        }

        private static HashSet<string> BuildEpisodeIds(IReadOnlyList<Episode> episodes)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < (episodes?.Count ?? 0); i++)
            {
                if (string.IsNullOrWhiteSpace(episodes[i]?.EpisodeId) is false)
                {
                    result.Add(episodes[i].EpisodeId);
                }
            }

            return result;
        }

        private static HashSet<string> BuildEdgeIds(Route route)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < (route?.Edges.Count ?? 0); i++)
            {
                result.Add(route.Edges[i].EdgeId);
            }

            return result;
        }

        private static IReadOnlyList<EpisodePlacement> CompileEpisodes(
            AuthoringRouteLayout layout,
            ISet<string> episodeIds,
            string location,
            ValidationReport report)
        {
            var result = new List<EpisodePlacement>();
            var placed = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < layout.Episodes.Count; i++)
            {
                var source = layout.Episodes[i];
                var placementLocation = location + $"/episode[{i}]";
                if (source == null || string.IsNullOrWhiteSpace(source.EpisodeId) || !episodeIds.Contains(source.EpisodeId))
                {
                    report.AddError(placementLocation, $"Route layout references an unknown Episode. episode:{source?.EpisodeId}");
                    continue;
                }

                placementLocation = location + $"/episode:{source.EpisodeId}";
                if (!placed.Add(source.EpisodeId))
                {
                    report.AddError(placementLocation, "Episode placement must be unique in the Layout.");
                    continue;
                }

                result.Add(new EpisodePlacement(
                    source.EpisodeId,
                    CompilePlacement(source.Position, layout, placementLocation, report)));
            }

            foreach (var episodeId in episodeIds)
            {
                if (!placed.Contains(episodeId))
                {
                    report.AddError(location + $"/episode:{episodeId}", "Layout must place every Episode exactly once.");
                }
            }

            return result;
        }

        private static IReadOnlyList<RouteEdgePlacement> CompileEdges(
            AuthoringRouteLayout layout,
            ISet<string> edgeIds,
            string location,
            ValidationReport report)
        {
            var result = new List<RouteEdgePlacement>();
            var placed = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < layout.Edges.Count; i++)
            {
                var source = layout.Edges[i];
                var placementLocation = location + $"/edge[{i}]";
                if (source == null || string.IsNullOrWhiteSpace(source.EdgeId) || !edgeIds.Contains(source.EdgeId))
                {
                    report.AddError(placementLocation, $"Route layout references an unknown RouteEdge. edge:{source?.EdgeId}");
                    continue;
                }

                placementLocation = location + $"/edge:{source.EdgeId}";
                if (!placed.Add(source.EdgeId))
                {
                    report.AddError(placementLocation, "RouteEdge placement must be unique in the Layout.");
                    continue;
                }

                var points = new List<Placement>(source.ControlPoints.Count);
                for (var pointIndex = 0; pointIndex < source.ControlPoints.Count; pointIndex++)
                {
                    points.Add(CompilePlacement(
                        source.ControlPoints[pointIndex],
                        layout,
                        placementLocation + $"/point:{pointIndex}",
                        report));
                }

                result.Add(new RouteEdgePlacement(source.EdgeId, points, source.StyleKey));
            }

            foreach (var edgeId in edgeIds)
            {
                if (!placed.Contains(edgeId))
                {
                    report.AddError(location + $"/edge:{edgeId}", "Layout must place every RouteEdge exactly once.");
                }
            }

            return result;
        }

        private static Placement CompilePlacement(
            AuthoringPlacement source,
            AuthoringRouteLayout layout,
            string location,
            ValidationReport report)
        {
            if (source == null)
            {
                report.AddError(location, "Route placement cannot be null.");
                return default;
            }

            var position = source.Position;
            if (!IsFinite(position.x) || !IsFinite(position.y))
            {
                report.AddError(
                    location,
                    $"Route placement must use finite viewport-relative coordinates. position:({position.x},{position.y})");
            }

            return new Placement(position.x, position.y);
        }

        private static string ResolveBackgroundPath(
            Texture2D background,
            string location,
            ValidationReport report)
        {
            if (background == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(background);
            if (string.IsNullOrWhiteSpace(path))
            {
                report.AddError(location + "/background", "Runtime background image must be a project asset.");
                return null;
            }

            return path;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

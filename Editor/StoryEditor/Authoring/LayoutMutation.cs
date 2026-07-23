using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Authoring
{
    public sealed class LayoutMutation
    {
        public const string UnknownVolume = "unknown_volume";
        public const string UnknownLayout = "unknown_layout";
        public const string UnknownEpisode = "unknown_episode";
        public const string UnknownEdge = "unknown_edge";
        public const string InvalidLayout = "invalid_layout";

        private readonly AuthoringAsset m_Asset;

        public LayoutMutation(AuthoringAsset asset)
        {
            m_Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        public LayoutMutationResult AddLayout(string volumeId, LayoutOrientation orientation)
        {
            var volume = FindVolume(m_Asset, volumeId);
            if (volume == null)
            {
                return Fail(UnknownVolume, $"卷不存在：{volumeId}");
            }

            if (!Enum.IsDefined(typeof(LayoutOrientation), orientation))
            {
                return Fail(InvalidLayout, $"布局方向无效：{orientation}");
            }

            if (!TryCompileTopology(volumeId, out var compiled, out var failure))
            {
                return failure;
            }

            var layouts = LayoutCopies.CopyAll(volume.Layouts);
            var layout = CreateDefaultLayout(compiled, orientation);
            layouts.Add(layout);
            if (!TryValidate(volume, compiled, layouts, out failure))
            {
                return failure;
            }

            Commit(volume, layouts, "Add Route Layout");
            return LayoutMutationResult.Success("已添加路线布局。", layout.LayoutId);
        }

        public LayoutMutationResult RemoveLayout(string volumeId, string layoutId)
        {
            var volume = FindVolume(m_Asset, volumeId);
            if (volume == null)
            {
                return Fail(UnknownVolume, $"卷不存在：{volumeId}");
            }

            var layouts = LayoutCopies.CopyAll(volume.Layouts);
            var removed = layouts.RemoveAll(x => x != null && x.LayoutId == layoutId);
            if (removed != 1)
            {
                return Fail(UnknownLayout, $"布局不存在：{layoutId}");
            }

            if (!TryCompileTopology(volumeId, out var compiled, out var failure) ||
                !TryValidate(volume, compiled, layouts, out failure))
            {
                return failure;
            }

            Commit(volume, layouts, "Remove Route Layout");
            return LayoutMutationResult.Success("已删除路线布局。", layoutId);
        }

        public LayoutMutationResult UpdateLayout(string volumeId, string layoutId, LayoutMetadata metadata)
        {
            return MutateLayout(volumeId, layoutId, "Update Route Layout", InvalidLayout, layout =>
            {
                layout.Orientation = metadata.Orientation;
                layout.BackgroundImage = metadata.BackgroundImage;
                layout.EditorGuideImage = metadata.EditorGuideImage;
                return null;
            });
        }

        public LayoutMutationResult MoveNodes(
            string volumeId,
            string layoutId,
            Placement? root,
            IReadOnlyList<EpisodePlacement> episodes)
        {
            return MutateLayout(volumeId, layoutId, "Move Route Nodes", UnknownEpisode, layout =>
            {
                if (root.HasValue)
                {
                    if (layout.RootPlacement == null)
                    {
                        return "布局缺少虚拟根位置。";
                    }

                    layout.RootPlacement.Position = ToVector2(root.Value);
                }

                for (var i = 0; i < (episodes?.Count ?? 0); i++)
                {
                    var target = FindEpisode(layout, episodes[i].EpisodeId);
                    if (target?.Position == null)
                    {
                        return $"布局中的剧情段不存在：{episodes[i].EpisodeId}";
                    }

                    target.Position.Position = ToVector2(episodes[i].Position);
                }

                return null;
            });
        }

        public LayoutMutationResult UpdateEdgePath(
            string volumeId,
            string layoutId,
            string edgeId,
            IReadOnlyList<Placement> controlPoints,
            string styleKey)
        {
            return MutateLayout(volumeId, layoutId, "Update Route Edge Path", UnknownEdge, layout =>
            {
                var edge = FindEdge(layout, edgeId);
                if (edge == null)
                {
                    return $"布局中的路线边不存在：{edgeId}";
                }

                edge.ControlPoints.Clear();
                for (var i = 0; i < (controlPoints?.Count ?? 0); i++)
                {
                    edge.ControlPoints.Add(new AuthoringPlacement { Position = ToVector2(controlPoints[i]) });
                }

                edge.StyleKey = styleKey;
                return null;
            });
        }

        private LayoutMutationResult MutateLayout(
            string volumeId,
            string layoutId,
            string undoName,
            string mutationErrorCode,
            Func<AuthoringRouteLayout, string> mutate)
        {
            var volume = FindVolume(m_Asset, volumeId);
            if (volume == null)
            {
                return Fail(UnknownVolume, $"卷不存在：{volumeId}");
            }

            var layouts = LayoutCopies.CopyAll(volume.Layouts);
            var layout = FindLayout(layouts, layoutId);
            if (layout == null)
            {
                return Fail(UnknownLayout, $"布局不存在：{layoutId}");
            }

            var error = mutate(layout);
            if (string.IsNullOrWhiteSpace(error) is false)
            {
                return Fail(mutationErrorCode, error);
            }

            if (!TryCompileTopology(volumeId, out var compiled, out var failure) ||
                !TryValidate(volume, compiled, layouts, out failure))
            {
                return failure;
            }

            Commit(volume, layouts, undoName);
            return LayoutMutationResult.Success("已更新路线布局。", layoutId);
        }

        private bool TryCompileTopology(
            string volumeId,
            out Volume compiledVolume,
            out LayoutMutationResult failure)
        {
            var volume = FindVolume(m_Asset, volumeId);
            if (volume == null)
            {
                compiledVolume = null;
                failure = Fail(UnknownVolume, $"卷不存在：{volumeId}");
                return false;
            }

            var episodes = new List<Episode>();
            for (var i = 0; i < volume.Episodes.Count; i++)
            {
                var source = volume.Episodes[i];
                if (source == null || string.IsNullOrWhiteSpace(source.EpisodeId))
                {
                    continue;
                }

                var exits = new List<EpisodeExit>();
                for (var nodeIndex = 0; nodeIndex < source.Nodes.Count; nodeIndex++)
                {
                    var node = source.Nodes[nodeIndex];
                    if (node != null &&
                        (node.NodeKind == NodeKind.Choice ||
                         node.NodeKind == NodeKind.End ||
                         node.NodeKind == NodeKind.Transition) &&
                        string.IsNullOrWhiteSpace(node.NodeId) is false)
                    {
                        exits.Add(new EpisodeExit(node.NodeId, node.Title));
                    }
                }

                episodes.Add(new Episode(
                    source.EpisodeId,
                    source.Title,
                    source.EntryNodeId,
                    exits,
                    Array.Empty<Step>()));
            }

            var report = new GameDeveloperKit.StoryEditor.Validation.ValidationReport();
            var route = RouteCompiler.Compile(
                m_Asset.StoryId,
                volume,
                episodes,
                new HashSet<string>(StringComparer.Ordinal),
                report);
            if (report.HasErrors)
            {
                compiledVolume = null;
                failure = Fail(InvalidLayout,
                    report.Issues.Count == 0 ? "剧情路线当前不可用。" : report.Issues[0].Message);
                return false;
            }

            compiledVolume = new Volume(volume.VolumeId, volume.Title, episodes, route);
            failure = default;
            return true;
        }

        private bool TryValidate(
            AuthoringVolume volume,
            Volume compiled,
            IReadOnlyList<AuthoringRouteLayout> layouts,
            out LayoutMutationResult failure)
        {
            var candidate = new AuthoringVolume { VolumeId = volume.VolumeId };
            candidate.Layouts.AddRange(LayoutCopies.CopyAll(layouts));
            var report = new GameDeveloperKit.StoryEditor.Validation.ValidationReport();
            LayoutCompiler.Compile(m_Asset.StoryId, candidate, compiled.Episodes, compiled.Route, report);
            if (report.HasErrors)
            {
                failure = Fail(InvalidLayout, report.Issues[0].Message);
                return false;
            }

            failure = default;
            return true;
        }

        private void Commit(AuthoringVolume volume, IReadOnlyList<AuthoringRouteLayout> layouts, string undoName)
        {
            var target = m_Asset.FindVolumeAsset(volume?.VolumeId) ?? (UnityEngine.Object)m_Asset;
            AuthoringUndo.Mutate(target, undoName, () => LayoutCopies.Replace(volume.Layouts, layouts));
        }

        private static AuthoringRouteLayout CreateDefaultLayout(Volume volume, LayoutOrientation orientation)
        {
            var result = new AuthoringRouteLayout
            {
                LayoutId = IdentityId.New(),
                Orientation = orientation,
                UsesRelativeCoordinates = true,
                RootPlacement = new AuthoringPlacement
                {
                    Position = orientation == LayoutOrientation.Portrait
                        ? new Vector2(0.5f, 0.08f)
                        : new Vector2(0.08f, 0.5f)
                }
            };
            var depths = BuildDepths(volume.Route);
            var counts = new Dictionary<int, int>();
            for (var i = 0; i < volume.Episodes.Count; i++)
            {
                var depth = depths.TryGetValue(volume.Episodes[i].EpisodeId, out var value) ? value : 1;
                counts[depth] = counts.TryGetValue(depth, out var count) ? count + 1 : 1;
            }

            var rows = new Dictionary<int, int>();
            for (var i = 0; i < volume.Episodes.Count; i++)
            {
                var episodeId = volume.Episodes[i].EpisodeId;
                var depth = depths.TryGetValue(episodeId, out var value) ? value : 1;
                rows.TryGetValue(depth, out var row);
                float x;
                float y;
                if (orientation == LayoutOrientation.Portrait)
                {
                    x = (row + 1f) / (counts[depth] + 1f);
                    y = 0.08f + depth * 0.32f;
                }
                else if (orientation == LayoutOrientation.Landscape)
                {
                    x = 0.08f + depth * 0.32f;
                    y = (row + 1f) / (counts[depth] + 1f);
                }
                else
                {
                    x = 0.08f + depth * 0.32f;
                    y = 0.5f + (row - (counts[depth] - 1f) * 0.5f) * 0.22f;
                }

                result.Episodes.Add(new AuthoringEpisodePlacement
                {
                    EpisodeId = episodeId,
                    Position = new AuthoringPlacement { Position = new Vector2(x, y) }
                });
                rows[depth] = row + 1;
            }

            for (var i = 0; i < volume.Route.Edges.Count; i++)
            {
                result.Edges.Add(new AuthoringRouteEdgePlacement { EdgeId = volume.Route.Edges[i].EdgeId });
            }

            return result;
        }

        private static Dictionary<string, int> BuildDepths(Route route)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            var unresolved = new List<RouteEdge>(route?.Edges ?? Array.Empty<RouteEdge>());
            while (unresolved.Count > 0)
            {
                var changed = false;
                for (var i = unresolved.Count - 1; i >= 0; i--)
                {
                    var edge = unresolved[i];
                    if (edge.SourceKind == RouteEdgeSourceKind.Root)
                    {
                        result[edge.ToEpisodeId] = 1;
                    }
                    else if (result.TryGetValue(edge.FromEpisodeId, out var parentDepth))
                    {
                        result[edge.ToEpisodeId] = parentDepth + 1;
                    }
                    else
                    {
                        continue;
                    }

                    unresolved.RemoveAt(i);
                    changed = true;
                }

                if (!changed)
                {
                    break;
                }
            }

            return result;
        }

        private static AuthoringVolume FindVolume(AuthoringAsset asset, string volumeId)
        {
            for (var i = 0; i < asset.Volumes.Count; i++)
            {
                if (asset.Volumes[i] != null && asset.Volumes[i].VolumeId == volumeId)
                {
                    return asset.Volumes[i];
                }
            }

            return null;
        }

        private static AuthoringRouteLayout FindLayout(
            IReadOnlyList<AuthoringRouteLayout> layouts,
            string layoutId)
        {
            for (var i = 0; i < (layouts?.Count ?? 0); i++)
            {
                if (layouts[i] != null && layouts[i].LayoutId == layoutId)
                {
                    return layouts[i];
                }
            }

            return null;
        }

        private static AuthoringEpisodePlacement FindEpisode(AuthoringRouteLayout layout, string episodeId)
        {
            for (var i = 0; i < layout.Episodes.Count; i++)
            {
                if (layout.Episodes[i] != null && layout.Episodes[i].EpisodeId == episodeId)
                {
                    return layout.Episodes[i];
                }
            }

            return null;
        }

        private static AuthoringRouteEdgePlacement FindEdge(AuthoringRouteLayout layout, string edgeId)
        {
            for (var i = 0; i < layout.Edges.Count; i++)
            {
                if (layout.Edges[i] != null && layout.Edges[i].EdgeId == edgeId)
                {
                    return layout.Edges[i];
                }
            }

            return null;
        }

        private static Vector2 ToVector2(Placement placement)
        {
            return new Vector2(placement.X, placement.Y);
        }

        private static LayoutMutationResult Fail(string errorCode, string message)
        {
            return LayoutMutationResult.Failure(errorCode, message);
        }
    }
}

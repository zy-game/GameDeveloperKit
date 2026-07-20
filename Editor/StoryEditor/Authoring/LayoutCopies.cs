using System.Collections.Generic;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.Authoring
{
    internal static class LayoutCopies
    {
        public static List<AuthoringRouteLayout> CopyAll(IReadOnlyList<AuthoringRouteLayout> source)
        {
            var result = new List<AuthoringRouteLayout>();
            for (var i = 0; i < (source?.Count ?? 0); i++)
            {
                result.Add(Copy(source[i]));
            }

            return result;
        }

        public static AuthoringRouteLayout Copy(AuthoringRouteLayout source)
        {
            if (source == null)
            {
                return null;
            }

            var result = new AuthoringRouteLayout
            {
                LayoutId = source.LayoutId,
                Orientation = source.Orientation,
                LegacyReferenceWidth = source.LegacyReferenceWidth,
                LegacyReferenceHeight = source.LegacyReferenceHeight,
                UsesNormalizedCoordinates = source.UsesNormalizedCoordinates,
                BackgroundImage = source.BackgroundImage,
                EditorGuideImage = source.EditorGuideImage,
                RootPlacement = Copy(source.RootPlacement)
            };
            for (var i = 0; i < source.Episodes.Count; i++)
            {
                var placement = source.Episodes[i];
                result.Episodes.Add(placement == null
                    ? null
                    : new AuthoringEpisodePlacement
                    {
                        EpisodeId = placement.EpisodeId,
                        Position = Copy(placement.Position)
                    });
            }

            for (var i = 0; i < source.Edges.Count; i++)
            {
                var placement = source.Edges[i];
                if (placement == null)
                {
                    result.Edges.Add(null);
                    continue;
                }

                var edge = new AuthoringRouteEdgePlacement
                {
                    EdgeId = placement.EdgeId,
                    StyleKey = placement.StyleKey
                };
                for (var pointIndex = 0; pointIndex < placement.ControlPoints.Count; pointIndex++)
                {
                    edge.ControlPoints.Add(Copy(placement.ControlPoints[pointIndex]));
                }

                result.Edges.Add(edge);
            }

            return result;
        }

        public static AuthoringPlacement Copy(AuthoringPlacement source)
        {
            return source == null ? null : new AuthoringPlacement { Position = source.Position };
        }

        public static void Replace(
            IList<AuthoringRouteLayout> target,
            IReadOnlyList<AuthoringRouteLayout> source)
        {
            target.Clear();
            for (var i = 0; i < (source?.Count ?? 0); i++)
            {
                target.Add(Copy(source[i]));
            }
        }
    }
}

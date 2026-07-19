using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story.Model
{
    public enum LayoutOrientation
    {
        Landscape = 0,
        Portrait = 1,
        Custom = 2
    }

    public sealed class RouteLayout
    {
        public RouteLayout(
            string layoutId,
            LayoutOrientation orientation,
            int referenceWidth,
            int referenceHeight,
            string backgroundImagePath,
            Placement rootPlacement,
            IReadOnlyList<EpisodePlacement> episodes,
            IReadOnlyList<RouteEdgePlacement> edges)
        {
            if (string.IsNullOrWhiteSpace(layoutId))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(layoutId));
            }

            LayoutId = layoutId;
            Orientation = orientation;
            ReferenceWidth = referenceWidth;
            ReferenceHeight = referenceHeight;
            BackgroundImagePath = backgroundImagePath;
            RootPlacement = rootPlacement;
            Episodes = CopyList(episodes);
            Edges = CopyList(edges);
        }

        public string LayoutId { get; }

        public LayoutOrientation Orientation { get; }

        public int ReferenceWidth { get; }

        public int ReferenceHeight { get; }

        public string BackgroundImagePath { get; }

        public Placement RootPlacement { get; }

        public IReadOnlyList<EpisodePlacement> Episodes { get; }

        public IReadOnlyList<RouteEdgePlacement> Edges { get; }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> items)
        {
            return items == null || items.Count == 0
                ? Array.Empty<T>()
                : new List<T>(items);
        }
    }

    public readonly struct Placement
    {
        public Placement(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }
    }

    public readonly struct EpisodePlacement
    {
        public EpisodePlacement(string episodeId, Placement position)
        {
            EpisodeId = episodeId;
            Position = position;
        }

        public string EpisodeId { get; }

        public Placement Position { get; }
    }

    public sealed class RouteEdgePlacement
    {
        public RouteEdgePlacement(
            string edgeId,
            IReadOnlyList<Placement> controlPoints,
            string styleKey = null)
        {
            EdgeId = edgeId;
            ControlPoints = controlPoints == null || controlPoints.Count == 0
                ? Array.Empty<Placement>()
                : new List<Placement>(controlPoints);
            StyleKey = styleKey;
        }

        public string EdgeId { get; }

        public IReadOnlyList<Placement> ControlPoints { get; }

        public string StyleKey { get; }
    }
}

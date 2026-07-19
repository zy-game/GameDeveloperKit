using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 剧情卷。
    /// </summary>
    public sealed class Volume
    {
        /// <summary>
        /// 初始化剧情卷。
        /// </summary>
        /// <param name="volumeId">卷 ID。</param>
        /// <param name="title">卷标题。</param>
        /// <param name="episodes">剧情段集合。</param>
        /// <param name="route">卷路线。</param>
        /// <param name="previewImagePath">预览图资源路径。</param>
        /// <param name="description">卷简介。</param>
        public Volume(
            string volumeId,
            string title,
            IReadOnlyList<Episode> episodes,
            Route route,
            string previewImagePath = null,
            string description = null)
        {
            ValidateText(volumeId, nameof(volumeId));

            VolumeId = volumeId;
            Title = title ?? volumeId;
            Episodes = CopyList(episodes);
            Route = route ?? new Route();
            PreviewImagePath = previewImagePath;
            Description = description;
        }

        /// <summary>
        /// 卷 ID。
        /// </summary>
        public string VolumeId { get; }

        /// <summary>
        /// 卷标题。
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 剧情段集合。
        /// </summary>
        public IReadOnlyList<Episode> Episodes { get; }

        /// <summary>
        /// 卷路线。
        /// </summary>
        public Route Route { get; }

        /// <summary>
        /// 预览图资源路径。
        /// </summary>
        public string PreviewImagePath { get; }

        /// <summary>
        /// 卷简介。
        /// </summary>
        public string Description { get; }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            return new List<T>(items);
        }

        private static void ValidateText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }

    /// <summary>
    /// 可独立播放的剧情段。
    /// </summary>
    public sealed class Episode
    {
        /// <summary>
        /// 初始化剧情段。
        /// </summary>
        public Episode(
            string episodeId,
            string title,
            string entryStepId,
            IReadOnlyList<EpisodeExit> exits,
            IReadOnlyList<Step> steps,
            string previewImagePath = null,
            string description = null)
        {
            ValidateText(episodeId, nameof(episodeId));
            ValidateText(entryStepId, nameof(entryStepId));

            EpisodeId = episodeId;
            Title = title ?? episodeId;
            EntryStepId = entryStepId;
            Exits = CopyList(exits);
            Steps = CopyList(steps);
            PreviewImagePath = previewImagePath;
            Description = description;
        }

        public string EpisodeId { get; }

        public string Title { get; }

        public string EntryStepId { get; }

        public IReadOnlyList<EpisodeExit> Exits { get; }

        public IReadOnlyList<Step> Steps { get; }

        public string PreviewImagePath { get; }

        public string Description { get; }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            return new List<T>(items);
        }

        private static void ValidateText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }

    /// <summary>
    /// 剧情段出口。
    /// </summary>
    public readonly struct EpisodeExit
    {
        public EpisodeExit(string exitId, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(exitId))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(exitId));
            }

            ExitId = exitId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? exitId : displayName;
        }

        public string ExitId { get; }

        public string DisplayName { get; }
    }

    /// <summary>
    /// 路线边来源类型。
    /// </summary>
    public enum RouteEdgeSourceKind
    {
        Root = 0,
        EpisodeExit = 1
    }

    /// <summary>
    /// 卷内静态路线。
    /// </summary>
    public sealed class Route
    {
        public Route(IReadOnlyList<RouteEdge> edges = null)
        {
            Edges = CopyList(edges);
        }

        public IReadOnlyList<RouteEdge> Edges { get; }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            return new List<T>(items);
        }
    }

    /// <summary>
    /// 卷内静态路线边。
    /// </summary>
    public readonly struct RouteEdge
    {
        private RouteEdge(
            string edgeId,
            RouteEdgeSourceKind sourceKind,
            string fromEpisodeId,
            string fromExitId,
            string toEpisodeId)
        {
            ValidateText(edgeId, nameof(edgeId));
            ValidateText(toEpisodeId, nameof(toEpisodeId));

            EdgeId = edgeId;
            SourceKind = sourceKind;
            FromEpisodeId = fromEpisodeId;
            FromExitId = fromExitId;
            ToEpisodeId = toEpisodeId;
        }

        public string EdgeId { get; }

        public RouteEdgeSourceKind SourceKind { get; }

        public string FromEpisodeId { get; }

        public string FromExitId { get; }

        public string ToEpisodeId { get; }

        public static RouteEdge FromRoot(string edgeId, string toEpisodeId)
        {
            return new RouteEdge(edgeId, RouteEdgeSourceKind.Root, null, null, toEpisodeId);
        }

        public static RouteEdge FromExit(
            string edgeId,
            string fromEpisodeId,
            string fromExitId,
            string toEpisodeId)
        {
            ValidateText(fromEpisodeId, nameof(fromEpisodeId));
            ValidateText(fromExitId, nameof(fromExitId));
            return new RouteEdge(
                edgeId,
                RouteEdgeSourceKind.EpisodeExit,
                fromEpisodeId,
                fromExitId,
                toEpisodeId);
        }

        private static void ValidateText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }
}

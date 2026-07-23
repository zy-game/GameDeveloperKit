using System;

namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 剧情段完成类型。
    /// </summary>
    public enum EpisodeCompletionKind
    {
        Natural = 0,
        Choice = 1,
        Transition = 2,
        End = 3
    }

    /// <summary>
    /// 剧情段完成事实。
    /// </summary>
    public sealed class EpisodeCompletion
    {
        public EpisodeCompletion(
            string storyId,
            string volumeId,
            string episodeId,
            EpisodeCompletionKind kind,
            string exitId,
            string settlementId,
            string routeEdgeId,
            string nextEpisodeId)
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(storyId));
            }

            if (string.IsNullOrWhiteSpace(volumeId))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(volumeId));
            }

            if (string.IsNullOrWhiteSpace(episodeId))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(episodeId));
            }

            StoryId = storyId;
            VolumeId = volumeId;
            EpisodeId = episodeId;
            Kind = kind;
            ExitId = exitId;
            SettlementId = settlementId;
            RouteEdgeId = routeEdgeId;
            NextEpisodeId = nextEpisodeId;
        }

        public string StoryId { get; }

        public string VolumeId { get; }

        public string EpisodeId { get; }

        public EpisodeCompletionKind Kind { get; }

        public string ExitId { get; }

        public string SettlementId { get; }

        public string RouteEdgeId { get; }

        public string NextEpisodeId { get; }
    }
}

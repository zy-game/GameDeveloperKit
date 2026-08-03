using GameDeveloperKit.Event;

namespace GameDeveloperKit.Story.Events
{
    /// <summary>
    /// Story 解锁节点发出的事件。
    /// </summary>
    public sealed class StoryUnlockEvent : ArgsBase
    {
        public StoryUnlockEvent(
            string storyId,
            string volumeId,
            string episodeId,
            string stepId,
            string unlockId)
        {
            StoryId = storyId;
            VolumeId = volumeId;
            EpisodeId = episodeId;
            StepId = stepId;
            UnlockId = unlockId;
        }

        public string StoryId { get; }

        public string VolumeId { get; }

        public string EpisodeId { get; }

        public string StepId { get; }

        public string UnlockId { get; }
    }
}

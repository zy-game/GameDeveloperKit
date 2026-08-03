using System;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 当前剧情运行位置的只读快照。
    /// </summary>
    public readonly struct StoryPosition
    {
        public StoryPosition(
            string storyId,
            string volumeId,
            string episodeId,
            string stepId)
        {
            StoryId = RequireText(storyId, nameof(storyId));
            VolumeId = RequireText(volumeId, nameof(volumeId));
            EpisodeId = RequireText(episodeId, nameof(episodeId));
            StepId = string.IsNullOrWhiteSpace(stepId) ? string.Empty : stepId.Trim();
        }

        public string StoryId { get; }

        public string VolumeId { get; }

        public string EpisodeId { get; }

        public string StepId { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }

            return value.Trim();
        }
    }
}

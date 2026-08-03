using System;
using GameDeveloperKit.Story.Execution;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 从章节定义静态提取出的媒体提示。
    /// </summary>
    public sealed class StoryMediaCue
    {
        internal StoryMediaCue(
            string storyId,
            string volumeId,
            string episodeId,
            StoryInstruction instruction)
        {
            StoryId = storyId ?? throw new ArgumentNullException(nameof(storyId));
            VolumeId = volumeId ?? throw new ArgumentNullException(nameof(volumeId));
            EpisodeId = episodeId ?? throw new ArgumentNullException(nameof(episodeId));
            Instruction = instruction ?? throw new ArgumentNullException(nameof(instruction));
        }

        public string StoryId { get; }

        public string VolumeId { get; }

        public string EpisodeId { get; }

        public string StepId => Instruction.Step?.StepId ?? string.Empty;

        public StoryInstruction Instruction { get; }
    }
}

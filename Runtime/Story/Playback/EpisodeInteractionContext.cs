using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// 剧情段交互通道上下文。
    /// </summary>
    public sealed class EpisodeInteractionContext : InteractionContext
    {
        /// <summary>
        /// 初始化剧情段交互通道上下文。
        /// </summary>
        /// <param name="module">剧情模块。</param>
        /// <param name="presenter">剧情表现协调器。</param>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="program">剧情程序。</param>
        /// <param name="previousEpisode">上一个剧情段。</param>
        /// <param name="episode">当前剧情段。</param>
        /// <param name="frame">当前帧。</param>
        public EpisodeInteractionContext(
            StoryModule module,
            Presenter presenter,
            string storyId,
            Program program,
            Episode previousEpisode,
            Episode episode,
            Frame frame)
            : base(module, presenter, storyId, program)
        {
            PreviousEpisode = previousEpisode;
            Episode = episode;
            Frame = frame;
        }

        /// <summary>
        /// 上一个剧情段。
        /// </summary>
        public Episode PreviousEpisode { get; }

        /// <summary>
        /// 当前剧情段。
        /// </summary>
        public Episode Episode { get; }

        /// <summary>
        /// 当前帧。
        /// </summary>
        public Frame Frame { get; }
    }
}

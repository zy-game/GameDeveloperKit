using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// 剧情章节交互通道上下文。
    /// </summary>
    public sealed class ChapterInteractionContext : InteractionContext
    {
        /// <summary>
        /// 初始化剧情章节交互通道上下文。
        /// </summary>
        /// <param name="module">剧情模块。</param>
        /// <param name="presenter">剧情表现协调器。</param>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="program">剧情程序。</param>
        /// <param name="previousChapter">上一个章节。</param>
        /// <param name="chapter">当前章节。</param>
        /// <param name="frame">当前帧。</param>
        public ChapterInteractionContext(
            StoryModule module,
            Presenter presenter,
            string storyId,
            Program program,
            Chapter previousChapter,
            Chapter chapter,
            Frame frame)
            : base(module, presenter, storyId, program)
        {
            PreviousChapter = previousChapter;
            Chapter = chapter;
            Frame = frame;
        }

        /// <summary>
        /// 上一个章节。
        /// </summary>
        public Chapter PreviousChapter { get; }

        /// <summary>
        /// 当前章节。
        /// </summary>
        public Chapter Chapter { get; }

        /// <summary>
        /// 当前帧。
        /// </summary>
        public Frame Frame { get; }
    }
}

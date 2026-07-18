using System;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// 剧情交互通道上下文。
    /// </summary>
    public class InteractionContext
    {
        /// <summary>
        /// 初始化剧情交互通道上下文。
        /// </summary>
        /// <param name="module">剧情模块。</param>
        /// <param name="presenter">剧情表现协调器。</param>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="program">剧情程序。</param>
        public InteractionContext(
            StoryModule module,
            Presenter presenter,
            string storyId,
            Program program = null)
        {
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            StoryId = storyId;
            Program = program;
        }

        /// <summary>
        /// 剧情模块。
        /// </summary>
        public StoryModule Module { get; }

        /// <summary>
        /// 剧情表现协调器。
        /// </summary>
        public Presenter Presenter { get; }

        /// <summary>
        /// 剧情 ID。
        /// </summary>
        public string StoryId { get; }

        /// <summary>
        /// 剧情程序。
        /// </summary>
        public Program Program { get; }
    }
}

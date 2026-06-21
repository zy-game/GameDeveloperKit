using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情运行上下文。
    /// </summary>
    public readonly struct StoryRuntimeContext
    {
        /// <summary>
        /// 初始化运行上下文。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapter">当前章节。</param>
        /// <param name="step">当前步骤。</param>
        /// <param name="currentTime">当前时间。</param>
        /// <param name="variableStore">变量存储。</param>
        /// <param name="history">剧情历史。</param>
        public StoryRuntimeContext(
            StoryProgram program,
            StoryChapter chapter,
            StoryStep step,
            double currentTime,
            IStoryVariableStore variableStore,
            IReadOnlyList<HistoryEntry> history)
        {
            Program = program;
            Chapter = chapter;
            Step = step;
            CurrentTime = currentTime;
            VariableStore = variableStore;
            History = history;
        }

        /// <summary>
        /// 剧情程序。
        /// </summary>
        public StoryProgram Program { get; }

        /// <summary>
        /// 当前章节。
        /// </summary>
        public StoryChapter Chapter { get; }

        /// <summary>
        /// 当前步骤。
        /// </summary>
        public StoryStep Step { get; }

        /// <summary>
        /// 当前时间。
        /// </summary>
        public double CurrentTime { get; }

        /// <summary>
        /// 变量存储。
        /// </summary>
        public IStoryVariableStore VariableStore { get; }

        /// <summary>
        /// 剧情历史。
        /// </summary>
        public IReadOnlyList<HistoryEntry> History { get; }
    }
}

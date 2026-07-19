using System.Collections.Generic;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 剧情运行上下文。
    /// </summary>
    public readonly struct RuntimeContext
    {
        /// <summary>
        /// 初始化运行上下文。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="volume">当前卷。</param>
        /// <param name="episode">当前剧情段。</param>
        /// <param name="step">当前步骤。</param>
        /// <param name="currentTime">当前时间。</param>
        /// <param name="variableStore">变量存储。</param>
        /// <param name="history">剧情历史。</param>
        public RuntimeContext(
            Program program,
            Volume volume,
            Episode episode,
            Step step,
            double currentTime,
            IVariableStore variableStore,
            IReadOnlyList<HistoryEntry> history)
        {
            Program = program;
            Volume = volume;
            Episode = episode;
            Step = step;
            CurrentTime = currentTime;
            VariableStore = variableStore;
            History = history;
        }

        /// <summary>
        /// 剧情程序。
        /// </summary>
        public Program Program { get; }

        /// <summary>
        /// 当前卷。
        /// </summary>
        public Volume Volume { get; }

        /// <summary>
        /// 当前剧情段。
        /// </summary>
        public Episode Episode { get; }

        /// <summary>
        /// 当前步骤。
        /// </summary>
        public Step Step { get; }

        /// <summary>
        /// 当前时间。
        /// </summary>
        public double CurrentTime { get; }

        /// <summary>
        /// 变量存储。
        /// </summary>
        public IVariableStore VariableStore { get; }

        /// <summary>
        /// 剧情历史。
        /// </summary>
        public IReadOnlyList<HistoryEntry> History { get; }
    }
}

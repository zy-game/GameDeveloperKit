using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情步骤类型。
    /// </summary>
    public enum StoryStepKind
    {
        /// <summary>
        /// 开始。
        /// </summary>
        Start = 0,

        /// <summary>
        /// 文本行。
        /// </summary>
        Line = 1,

        /// <summary>
        /// 选项。
        /// </summary>
        Choice = 2,

        /// <summary>
        /// 命令。
        /// </summary>
        Command = 3,

        /// <summary>
        /// 条件分支。
        /// </summary>
        Branch = 4,

        /// <summary>
        /// 跳转。
        /// </summary>
        Jump = 5,

        /// <summary>
        /// 等待。
        /// </summary>
        Wait = 6,

        /// <summary>
        /// 结束。
        /// </summary>
        End = 7,

        /// <summary>
        /// 并行分叉。
        /// </summary>
        Parallel = 8,

        /// <summary>
        /// 并行合流。
        /// </summary>
        Merge = 9
    }

    /// <summary>
    /// 剧情合流策略。
    /// </summary>
    public enum StoryMergePolicy
    {
        /// <summary>
        /// 等待全部分支完成。
        /// </summary>
        All = 0
    }

    /// <summary>
    /// 剧情并行分支。
    /// </summary>
    public sealed class StoryParallelBranch
    {
        /// <summary>
        /// 初始化剧情并行分支。
        /// </summary>
        /// <param name="branchId">分支 ID。</param>
        /// <param name="label">显示标签。</param>
        /// <param name="entry">入口目标。</param>
        public StoryParallelBranch(string branchId, string label, StoryTarget entry)
        {
            if (string.IsNullOrWhiteSpace(branchId))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(branchId));
            }

            BranchId = branchId;
            Label = string.IsNullOrWhiteSpace(label) ? branchId : label;
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        /// <summary>
        /// 分支 ID。
        /// </summary>
        public string BranchId { get; }

        /// <summary>
        /// 显示标签。
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// 入口目标。
        /// </summary>
        public StoryTarget Entry { get; }
    }

    /// <summary>
    /// 剧情步骤。
    /// </summary>
    public sealed class StoryStep
    {
        private readonly IReadOnlyList<StoryChoice> m_Choices;

        /// <summary>
        /// 初始化剧情步骤。
        /// </summary>
        /// <param name="stepId">步骤 ID。</param>
        /// <param name="kind">步骤类型。</param>
        /// <param name="data">步骤数据。</param>
        public StoryStep(string stepId, StoryStepKind kind, StoryStepData data = null)
        {
            if (string.IsNullOrWhiteSpace(stepId))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(stepId));
            }

            StepId = stepId;
            Kind = kind;
            Data = data ?? new StoryStepData();
            m_Choices = Data.Choices;
        }

        /// <summary>
        /// 步骤 ID。
        /// </summary>
        public string StepId { get; }

        /// <summary>
        /// 步骤类型。
        /// </summary>
        public StoryStepKind Kind { get; }

        /// <summary>
        /// 步骤数据。
        /// </summary>
        public StoryStepData Data { get; }

        /// <summary>
        /// 选项集合。
        /// </summary>
        public IReadOnlyList<StoryChoice> Choices => m_Choices;

        /// <summary>
        /// 标签。
        /// </summary>
        public IReadOnlyList<string> Tags => Data.Tags;
    }

    /// <summary>
    /// 剧情步骤数据。
    /// </summary>
    public sealed class StoryStepData
    {
        private readonly IReadOnlyList<StoryChoice> m_Choices;
        private readonly IReadOnlyList<StoryParallelBranch> m_Branches;

        /// <summary>
        /// 初始化步骤数据。
        /// </summary>
        /// <param name="textKey">文本键。</param>
        /// <param name="speaker">说话人。</param>
        /// <param name="command">命令。</param>
        /// <param name="choices">选项集合。</param>
        /// <param name="condition">条件表达式。</param>
        /// <param name="target">跳转目标。</param>
        /// <param name="waitSeconds">等待秒数。</param>
        /// <param name="tags">标签。</param>
        /// <param name="branches">并行分支集合。</param>
        /// <param name="mergePolicy">合流策略。</param>
        /// <param name="parallelStepId">所属并行步骤 ID。</param>
        public StoryStepData(
            string textKey = null,
            string speaker = null,
            StoryCommand command = null,
            IReadOnlyList<StoryChoice> choices = null,
            StoryExpression condition = null,
            StoryTarget target = null,
            double waitSeconds = 0d,
            IReadOnlyList<string> tags = null,
            IReadOnlyList<StoryParallelBranch> branches = null,
            StoryMergePolicy mergePolicy = StoryMergePolicy.All,
            string parallelStepId = null)
        {
            TextKey = textKey;
            Speaker = speaker;
            Command = command;
            m_Choices = CopyChoices(choices);
            Condition = condition;
            Target = target;
            WaitSeconds = waitSeconds;
            Tags = CopyList(tags);
            m_Branches = CopyBranches(branches);
            MergePolicy = mergePolicy;
            ParallelStepId = parallelStepId;
        }

        /// <summary>
        /// 文本键。
        /// </summary>
        public string TextKey { get; }

        /// <summary>
        /// 说话人。
        /// </summary>
        public string Speaker { get; }

        /// <summary>
        /// 命令。
        /// </summary>
        public StoryCommand Command { get; }

        /// <summary>
        /// 选项集合。
        /// </summary>
        public IReadOnlyList<StoryChoice> Choices => m_Choices;

        /// <summary>
        /// 条件表达式。
        /// </summary>
        public StoryExpression Condition { get; }

        /// <summary>
        /// 跳转目标。
        /// </summary>
        public StoryTarget Target { get; }

        /// <summary>
        /// 等待秒数。
        /// </summary>
        public double WaitSeconds { get; }

        /// <summary>
        /// 标签。
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// 并行分支集合。
        /// </summary>
        public IReadOnlyList<StoryParallelBranch> Branches => m_Branches;

        /// <summary>
        /// 合流策略。
        /// </summary>
        public StoryMergePolicy MergePolicy { get; }

        /// <summary>
        /// 所属并行步骤 ID。
        /// </summary>
        public string ParallelStepId { get; }

        private static IReadOnlyList<StoryChoice> CopyChoices(IReadOnlyList<StoryChoice> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<StoryChoice>();
            }

            return new List<StoryChoice>(items);
        }

        private static IReadOnlyList<StoryParallelBranch> CopyBranches(IReadOnlyList<StoryParallelBranch> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<StoryParallelBranch>();
            }

            return new List<StoryParallelBranch>(items);
        }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            return new List<T>(items);
        }
    }
}

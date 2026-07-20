using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Text;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 剧情步骤类型。
    /// </summary>
    public enum StepKind
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
        Parallel = 8
    }

    /// <summary>
    /// 剧情并行分支。
    /// </summary>
    public sealed class ParallelBranch
    {
        /// <summary>
        /// 初始化剧情并行分支。
        /// </summary>
        /// <param name="branchId">分支 ID。</param>
        /// <param name="label">显示标签。</param>
        /// <param name="entry">入口目标。</param>
        public ParallelBranch(string branchId, string label, Target entry)
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
        public Target Entry { get; }
    }

    /// <summary>
    /// 剧情步骤。
    /// </summary>
    public sealed class Step
    {
        private readonly IReadOnlyList<Choice> m_Choices;

        /// <summary>
        /// 初始化剧情步骤。
        /// </summary>
        /// <param name="stepId">步骤 ID。</param>
        /// <param name="kind">步骤类型。</param>
        /// <param name="data">步骤数据。</param>
        public Step(string stepId, StepKind kind, StepData data = null)
        {
            if (string.IsNullOrWhiteSpace(stepId))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(stepId));
            }

            StepId = stepId;
            Kind = kind;
            Data = data ?? new StepData();
            m_Choices = Data.Choices;
        }

        /// <summary>
        /// 步骤 ID。
        /// </summary>
        public string StepId { get; }

        /// <summary>
        /// 步骤类型。
        /// </summary>
        public StepKind Kind { get; }

        /// <summary>
        /// 步骤数据。
        /// </summary>
        public StepData Data { get; }

        /// <summary>
        /// 选项集合。
        /// </summary>
        public IReadOnlyList<Choice> Choices => m_Choices;

        /// <summary>
        /// 标签。
        /// </summary>
        public IReadOnlyList<string> Tags => Data.Tags;
    }

    /// <summary>
    /// 剧情步骤数据。
    /// </summary>
    public sealed class StepData
    {
        private readonly IReadOnlyList<Choice> m_Choices;
        private readonly IReadOnlyList<ParallelBranch> m_Branches;

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
        /// <param name="exitId">结束步骤的出口 ID。</param>
        public StepData(
            string textKey = null,
            string speaker = null,
            Command command = null,
            IReadOnlyList<Choice> choices = null,
            Expression condition = null,
            Target target = null,
            double waitSeconds = 0d,
            IReadOnlyList<string> tags = null,
            IReadOnlyList<ParallelBranch> branches = null,
            string exitId = null)
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
            ExitId = exitId;
        }

        /// <summary>
        /// 文本键。
        /// </summary>
        public string TextKey { get; }

        public TextReference? Text => string.IsNullOrWhiteSpace(TextKey)
            ? (TextReference?)null
            : TextReferenceCodec.DeserializeOrLegacy(TextKey);

        /// <summary>
        /// 说话人。
        /// </summary>
        public string Speaker { get; }

        public TextReference? SpeakerText => string.IsNullOrWhiteSpace(Speaker)
            ? (TextReference?)null
            : TextReferenceCodec.DeserializeOrLegacy(Speaker);

        /// <summary>
        /// 命令。
        /// </summary>
        public Command Command { get; }

        /// <summary>
        /// 选项集合。
        /// </summary>
        public IReadOnlyList<Choice> Choices => m_Choices;

        /// <summary>
        /// 条件表达式。
        /// </summary>
        public Expression Condition { get; }

        /// <summary>
        /// 跳转目标。
        /// </summary>
        public Target Target { get; }

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
        public IReadOnlyList<ParallelBranch> Branches => m_Branches;

        /// <summary>
        /// 结束步骤的出口 ID。
        /// </summary>
        public string ExitId { get; }

        private static IReadOnlyList<Choice> CopyChoices(IReadOnlyList<Choice> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<Choice>();
            }

            return new List<Choice>(items);
        }

        private static IReadOnlyList<ParallelBranch> CopyBranches(IReadOnlyList<ParallelBranch> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<ParallelBranch>();
            }

            return new List<ParallelBranch>(items);
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

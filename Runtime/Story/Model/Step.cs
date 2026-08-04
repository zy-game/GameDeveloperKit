using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Media;
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
        /// 等待。
        /// </summary>
        Wait = 5,

        /// <summary>
        /// 结束。
        /// </summary>
        End = 6,

        /// <summary>
        /// 并行分叉。
        /// </summary>
        Parallel = 7,

        /// <summary>
        /// 自动过渡到下一剧情段。
        /// </summary>
        Transition = 8,

        /// <summary>
        /// 播放视频。
        /// </summary>
        PlayVideo = 9,

        /// <summary>
        /// 显示图片。
        /// </summary>
        ShowImage = 10,

        /// <summary>
        /// 播放音频。
        /// </summary>
        PlayAudio = 11,

        /// <summary>
        /// 派发解锁事件。
        /// </summary>
        Unlock = 12
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
        /// <param name="videoReference">视频引用。</param>
        /// <param name="imageLocation">图片资源位置。</param>
        /// <param name="audioReference">音频引用。</param>
        /// <param name="unlockId">解锁事件 ID。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <param name="seekable">是否允许视频 Seek。</param>
        /// <param name="volume">音频音量。</param>
        /// <param name="priority">音频优先级。</param>
        /// <param name="choices">选项集合。</param>
        /// <param name="target">跳转目标。</param>
        /// <param name="waitSeconds">等待秒数。</param>
        /// <param name="tags">标签。</param>
        /// <param name="branches">并行分支集合。</param>
        /// <param name="exitId">终端步骤的出口 ID。</param>
        /// <param name="settlementId">结束步骤的结算 ID。</param>
        public StepData(
            string textKey = null,
            string speaker = null,
            VideoReference videoReference = null,
            string imageLocation = null,
            AudioReference audioReference = null,
            string unlockId = null,
            bool loop = false,
            bool seekable = false,
            float volume = 1f,
            int priority = 0,
            IReadOnlyList<Choice> choices = null,
            Target target = null,
            double waitSeconds = 0d,
            IReadOnlyList<string> tags = null,
            IReadOnlyList<ParallelBranch> branches = null,
            string exitId = null,
            string settlementId = null)
        {
            TextKey = textKey;
            Speaker = speaker;
            VideoReference = videoReference;
            ImageLocation = imageLocation;
            AudioReference = audioReference;
            UnlockId = unlockId;
            Loop = loop;
            Seekable = seekable;
            Volume = volume;
            Priority = priority;
            m_Choices = CopyChoices(choices);
            Target = target;
            WaitSeconds = waitSeconds;
            Tags = CopyList(tags);
            m_Branches = CopyBranches(branches);
            ExitId = exitId;
            SettlementId = string.IsNullOrWhiteSpace(settlementId) ? null : settlementId.Trim();
        }

        /// <summary>
        /// 文本键。
        /// </summary>
        public string TextKey { get; }

        public TextReference? Text => string.IsNullOrWhiteSpace(TextKey)
            ? (TextReference?)null
            : TextReferenceCodec.Deserialize(TextKey);

        /// <summary>
        /// 说话人。
        /// </summary>
        public string Speaker { get; }

        public TextReference? SpeakerText => string.IsNullOrWhiteSpace(Speaker)
            ? (TextReference?)null
            : TextReferenceCodec.Deserialize(Speaker);

        /// <summary>
        /// 视频引用。
        /// </summary>
        public VideoReference VideoReference { get; }

        /// <summary>
        /// 图片资源位置。
        /// </summary>
        public string ImageLocation { get; }

        /// <summary>
        /// 音频引用。
        /// </summary>
        public AudioReference AudioReference { get; }

        /// <summary>
        /// 解锁事件 ID。
        /// </summary>
        public string UnlockId { get; }

        /// <summary>
        /// 是否循环播放。
        /// </summary>
        public bool Loop { get; }

        /// <summary>
        /// 是否允许视频 Seek。
        /// </summary>
        public bool Seekable { get; }

        /// <summary>
        /// 音频音量。
        /// </summary>
        public float Volume { get; }

        /// <summary>
        /// 音频优先级。
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 选项集合。
        /// </summary>
        public IReadOnlyList<Choice> Choices => m_Choices;

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
        /// 终端步骤的出口 ID。
        /// </summary>
        public string ExitId { get; }

        /// <summary>
        /// 结束步骤的结算 ID。
        /// </summary>
        public string SettlementId { get; }

        private static IReadOnlyList<Choice> CopyChoices(IReadOnlyList<Choice> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<Choice>();
            }

            return new List<Choice>(items).AsReadOnly();
        }

        private static IReadOnlyList<ParallelBranch> CopyBranches(IReadOnlyList<ParallelBranch> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<ParallelBranch>();
            }

            return new List<ParallelBranch>(items).AsReadOnly();
        }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            return new List<T>(items).AsReadOnly();
        }
    }
}

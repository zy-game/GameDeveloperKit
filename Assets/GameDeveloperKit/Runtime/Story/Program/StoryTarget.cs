using System;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情跳转目标类型。
    /// </summary>
    public enum StoryTargetKind
    {
        /// <summary>
        /// 跳转到章节内步骤。
        /// </summary>
        Step = 0,

        /// <summary>
        /// 跳转到章节入口。
        /// </summary>
        Chapter = 1,

        /// <summary>
        /// 跳转到剧情结束。
        /// </summary>
        StoryEnd = 2
    }

    /// <summary>
    /// 剧情跳转目标。
    /// </summary>
    public sealed class StoryTarget
    {
        private StoryTarget(StoryTargetKind targetKind, string chapterId, string stepId)
        {
            TargetKind = targetKind;
            ChapterId = chapterId;
            StepId = stepId;
        }

        /// <summary>
        /// 创建步骤目标。
        /// </summary>
        /// <param name="chapterId">章节 ID。</param>
        /// <param name="stepId">步骤 ID。</param>
        /// <returns>剧情目标。</returns>
        public static StoryTarget Step(string chapterId, string stepId)
        {
            ValidateText(chapterId, nameof(chapterId));
            ValidateText(stepId, nameof(stepId));
            return new StoryTarget(StoryTargetKind.Step, chapterId, stepId);
        }

        /// <summary>
        /// 创建章节目标。
        /// </summary>
        /// <param name="chapterId">章节 ID。</param>
        /// <returns>剧情目标。</returns>
        public static StoryTarget Chapter(string chapterId)
        {
            ValidateText(chapterId, nameof(chapterId));
            return new StoryTarget(StoryTargetKind.Chapter, chapterId, null);
        }

        /// <summary>
        /// 创建剧情结束目标。
        /// </summary>
        /// <returns>剧情目标。</returns>
        public static StoryTarget StoryEnd()
        {
            return new StoryTarget(StoryTargetKind.StoryEnd, null, null);
        }

        /// <summary>
        /// 目标类型。
        /// </summary>
        public StoryTargetKind TargetKind { get; }

        /// <summary>
        /// 章节 ID。
        /// </summary>
        public string ChapterId { get; }

        /// <summary>
        /// 步骤 ID。
        /// </summary>
        public string StepId { get; }

        /// <summary>
        /// 转为字符串。
        /// </summary>
        /// <returns>字符串表示。</returns>
        public override string ToString()
        {
            switch (TargetKind)
            {
                case StoryTargetKind.Step:
                    return $"Step:{ChapterId}:{StepId}";
                case StoryTargetKind.Chapter:
                    return $"Chapter:{ChapterId}";
                default:
                    return "StoryEnd";
            }
        }

        private static void ValidateText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }
}

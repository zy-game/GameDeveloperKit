using System;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 剧情跳转目标类型。
    /// </summary>
    public enum TargetKind
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
    public sealed class Target
    {
        private Target(TargetKind targetKind, string chapterId, string stepId)
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
        public static Target Step(string chapterId, string stepId)
        {
            ValidateText(chapterId, nameof(chapterId));
            ValidateText(stepId, nameof(stepId));
            return new Target(TargetKind.Step, chapterId, stepId);
        }

        /// <summary>
        /// 创建章节目标。
        /// </summary>
        /// <param name="chapterId">章节 ID。</param>
        /// <returns>剧情目标。</returns>
        public static Target Chapter(string chapterId)
        {
            ValidateText(chapterId, nameof(chapterId));
            return new Target(TargetKind.Chapter, chapterId, null);
        }

        /// <summary>
        /// 创建剧情结束目标。
        /// </summary>
        /// <returns>剧情目标。</returns>
        public static Target StoryEnd()
        {
            return new Target(TargetKind.StoryEnd, null, null);
        }

        /// <summary>
        /// 目标类型。
        /// </summary>
        public TargetKind TargetKind { get; }

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
                case TargetKind.Step:
                    return $"Step:{ChapterId}:{StepId}";
                case TargetKind.Chapter:
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

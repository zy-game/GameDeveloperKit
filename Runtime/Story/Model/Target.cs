using System;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 剧情跳转目标类型。
    /// </summary>
    public enum TargetKind
    {
        /// <summary>
        /// 跳转到剧情段内步骤。
        /// </summary>
        Step = 0,

        /// <summary>
        /// 完成当前剧情段。
        /// </summary>
        EpisodeEnd = 1
    }

    /// <summary>
    /// 剧情跳转目标。
    /// </summary>
    public sealed class Target
    {
        private Target(TargetKind targetKind, string stepId)
        {
            TargetKind = targetKind;
            StepId = stepId;
        }

        /// <summary>
        /// 创建步骤目标。
        /// </summary>
        /// <param name="stepId">步骤 ID。</param>
        /// <returns>剧情目标。</returns>
        public static Target Step(string stepId)
        {
            ValidateText(stepId, nameof(stepId));
            return new Target(TargetKind.Step, stepId);
        }

        /// <summary>
        /// 创建剧情段完成目标。
        /// </summary>
        /// <returns>剧情目标。</returns>
        public static Target EpisodeEnd()
        {
            return new Target(TargetKind.EpisodeEnd, null);
        }

        /// <summary>
        /// 目标类型。
        /// </summary>
        public TargetKind TargetKind { get; }

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
                    return $"Step:{StepId}";
                default:
                    return "EpisodeEnd";
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

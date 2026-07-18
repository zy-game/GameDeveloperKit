using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 剧情选项。
    /// </summary>
    public sealed class Choice
    {
        /// <summary>
        /// 初始化剧情选项。
        /// </summary>
        /// <param name="choiceId">选项 ID。</param>
        /// <param name="textKey">选项文本键。</param>
        /// <param name="condition">选项条件。</param>
        /// <param name="target">选项目标。</param>
        /// <param name="tags">选项标签。</param>
        /// <param name="branchId">并行分支 ID。</param>
        public Choice(
            string choiceId,
            string textKey,
            Expression condition,
            Target target,
            IReadOnlyList<string> tags = null,
            string branchId = null)
        {
            ValidateText(choiceId, nameof(choiceId));
            ValidateText(textKey, nameof(textKey));
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            ChoiceId = choiceId;
            TextKey = textKey;
            Condition = condition;
            Target = target;
            Tags = CopyList(tags);
            BranchId = branchId;
        }

        /// <summary>
        /// 选项 ID。
        /// </summary>
        public string ChoiceId { get; }

        /// <summary>
        /// 选项文本键。
        /// </summary>
        public string TextKey { get; }

        /// <summary>
        /// 选项条件。
        /// </summary>
        public Expression Condition { get; }

        /// <summary>
        /// 选项目标。
        /// </summary>
        public Target Target { get; }

        /// <summary>
        /// 选项标签。
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// 所属并行分支 ID。
        /// </summary>
        public string BranchId { get; }

        /// <summary>
        /// 创建带分支来源的选项副本。
        /// </summary>
        /// <param name="branchId">并行分支 ID。</param>
        /// <returns>选项。</returns>
        public Choice WithBranch(string branchId)
        {
            return new Choice(ChoiceId, TextKey, Condition, Target, Tags, branchId);
        }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            return new List<T>(items);
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

using System;
using GameDeveloperKit.Story.Text;

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
        /// <param name="exitId">出口 ID。</param>
        /// <param name="textKey">选项文本键。</param>
        /// <param name="condition">选项条件。</param>
        public Choice(
            string choiceId,
            string exitId,
            string textKey,
            Expression condition = null)
        {
            ValidateText(choiceId, nameof(choiceId));
            ValidateText(exitId, nameof(exitId));
            ValidateText(textKey, nameof(textKey));

            ChoiceId = choiceId;
            ExitId = exitId;
            TextKey = textKey;
            Condition = condition;
        }

        /// <summary>
        /// 选项 ID。
        /// </summary>
        public string ChoiceId { get; }

        /// <summary>
        /// 出口 ID。
        /// </summary>
        public string ExitId { get; }

        /// <summary>
        /// 选项文本键。
        /// </summary>
        public string TextKey { get; }

        public TextReference Text => TextReferenceCodec.DeserializeOrLegacy(TextKey);

        /// <summary>
        /// 选项条件。
        /// </summary>
        public Expression Condition { get; }

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

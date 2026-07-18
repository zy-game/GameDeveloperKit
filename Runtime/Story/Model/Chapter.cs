using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 剧情章节。
    /// </summary>
    public sealed class Chapter
    {
        /// <summary>
        /// 初始化剧情章节。
        /// </summary>
        /// <param name="chapterId">章节 ID。</param>
        /// <param name="title">章节标题。</param>
        /// <param name="entryStepId">入口步骤 ID。</param>
        /// <param name="steps">步骤集合。</param>
        /// <param name="previewImagePath">预览图资源路径。</param>
        /// <param name="description">章节简介。</param>
        public Chapter(
            string chapterId,
            string title,
            string entryStepId,
            IReadOnlyList<Step> steps,
            string previewImagePath = null,
            string description = null)
        {
            ValidateText(chapterId, nameof(chapterId));
            ValidateText(entryStepId, nameof(entryStepId));

            ChapterId = chapterId;
            Title = title ?? chapterId;
            EntryStepId = entryStepId;
            Steps = CopyList(steps);
            PreviewImagePath = previewImagePath;
            Description = description;
        }

        /// <summary>
        /// 章节 ID。
        /// </summary>
        public string ChapterId { get; }

        /// <summary>
        /// 章节标题。
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 入口步骤 ID。
        /// </summary>
        public string EntryStepId { get; }

        /// <summary>
        /// 步骤集合。
        /// </summary>
        public IReadOnlyList<Step> Steps { get; }

        /// <summary>
        /// 预览图资源路径。
        /// </summary>
        public string PreviewImagePath { get; }

        /// <summary>
        /// 章节简介。
        /// </summary>
        public string Description { get; }

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

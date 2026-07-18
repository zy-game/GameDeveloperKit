using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 编译后的剧情程序。
    /// </summary>
    public sealed class Program
    {
        /// <summary>
        /// 初始化剧情程序。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="version">版本。</param>
        /// <param name="entryChapterId">入口章节 ID。</param>
        /// <param name="chapters">章节集合。</param>
        /// <param name="variableSchema">变量 schema。</param>
        /// <param name="commandSchema">命令 schema。</param>
        public Program(
            string storyId,
            string version,
            string entryChapterId,
            IReadOnlyList<Chapter> chapters,
            VariableSchema variableSchema = null,
            CommandSchema commandSchema = null)
        {
            ValidateText(storyId, nameof(storyId));
            ValidateText(version, nameof(version));
            ValidateText(entryChapterId, nameof(entryChapterId));

            StoryId = storyId;
            Version = version;
            EntryChapterId = entryChapterId;
            Chapters = CopyList(chapters);
            VariableSchema = variableSchema ?? new VariableSchema();
            CommandSchema = commandSchema ?? new CommandSchema();
        }

        /// <summary>
        /// 剧情 ID。
        /// </summary>
        public string StoryId { get; }

        /// <summary>
        /// 版本。
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// 入口章节 ID。
        /// </summary>
        public string EntryChapterId { get; }

        /// <summary>
        /// 章节集合。
        /// </summary>
        public IReadOnlyList<Chapter> Chapters { get; }

        /// <summary>
        /// 变量 schema。
        /// </summary>
        public VariableSchema VariableSchema { get; }

        /// <summary>
        /// 命令 schema。
        /// </summary>
        public CommandSchema CommandSchema { get; }

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

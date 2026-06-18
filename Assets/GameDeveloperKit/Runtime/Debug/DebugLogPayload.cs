using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Debug Log Payload 结构。
    /// </summary>
    public readonly struct DebugLogPayload
    {
        /// <summary>
        /// 初始化 Debug Log Payload。
        /// </summary>
        public DebugLogPayload(
            long sequence,
            DateTimeOffset timestamp,
            int frameCount,
            long timerTick,
            string level,
            string category,
            string message,
            string exception,
            string context,
            IReadOnlyList<string> tags)
        {
            Sequence = sequence;
            Timestamp = timestamp;
            FrameCount = frameCount;
            TimerTick = timerTick;
            Level = level;
            Category = category;
            Message = message;
            Exception = exception;
            Context = context;
            Tags = CopyTags(tags);
        }

        public long Sequence { get; }

        public DateTimeOffset Timestamp { get; }

        public int FrameCount { get; }

        public long TimerTick { get; }

        public string Level { get; }

        public string Category { get; }

        public string Message { get; }

        public string Exception { get; }

        public string Context { get; }

        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// 复制 Tags。
        /// </summary>
        /// <param name="tags">tags 参数。</param>
        /// <returns>执行结果。</returns>
        private static IReadOnlyList<string> CopyTags(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[tags.Count];
            for (var i = 0; i < tags.Count; i++)
            {
                copy[i] = tags[i];
            }

            return copy;
        }
    }
}

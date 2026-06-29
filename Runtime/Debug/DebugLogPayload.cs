using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Debugger
{
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

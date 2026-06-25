using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Debugger
{
    public readonly struct DebugLogRecord
    {
        /// <summary>
        /// 初始化 Debug Log Record。
        /// </summary>
        /// <param name="frameCount">frame Count 参数。</param>
        /// <param name="timerTick">timer Tick 参数。</param>
        public DebugLogRecord(
            DateTimeOffset timestamp,
            long sequence,
            int frameCount,
            long timerTick,
            LogLevel level,
            string category,
            string message,
            Exception exception,
            object context,
            IReadOnlyList<string> tags)
        {
            Timestamp = timestamp;
            Sequence = sequence;
            FrameCount = frameCount;
            TimerTick = timerTick;
            Level = level;
            Category = category;
            Message = message;
            Exception = exception;
            Context = context;
            Tags = tags ?? Array.Empty<string>();
        }

        public DateTimeOffset Timestamp { get; }

        public long Sequence { get; }

        public int FrameCount { get; }

        public long TimerTick { get; }

        public LogLevel Level { get; }

        public string Category { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public object Context { get; }

        public IReadOnlyList<string> Tags { get; }
    }
}

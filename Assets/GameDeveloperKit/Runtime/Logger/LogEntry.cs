using System;

namespace GameDeveloperKit.Logger
{
    public readonly struct LogEntry
    {
        public LogEntry(
            DateTimeOffset timestamp,
            LogLevel level,
            string category,
            string message,
            Exception exception,
            object context)
        {
            Timestamp = timestamp;
            Level = level;
            Category = category;
            Message = message;
            Exception = exception;
            Context = context;
        }

        public DateTimeOffset Timestamp { get; }

        public LogLevel Level { get; }

        public string Category { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public object Context { get; }
    }
}

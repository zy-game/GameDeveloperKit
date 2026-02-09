using System;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 日志条目
    /// </summary>
    public struct LogEntry
    {
        public DateTime Timestamp;
        public LogLevel Level;
        public string Message;
        public string StackTrace;
        public string Tag;

        public LogEntry(LogLevel level, string message, string stackTrace = null, string tag = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
            StackTrace = stackTrace;
            Tag = tag;
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
        }
    }
}

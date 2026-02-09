using System;
using GameDeveloperKit.Network;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 远程日志消息
    /// </summary>
    public class RemoteLogMessage : INetworkMessage
    {
        public int MessageId => GetType().GetHashCode();
        public string DeviceId { get; set; }
        public string DeviceModel { get; set; }
        public string AppVersion { get; set; }
        public LogEntryData[] Logs { get; set; }

        [Serializable]
        public class LogEntryData
        {
            public long TimestampTicks;
            public int Level;
            public string Message;
            public string StackTrace;
            public string Tag;

            public static LogEntryData FromEntry(LogEntry entry)
            {
                return new LogEntryData
                {
                    TimestampTicks = entry.Timestamp.Ticks,
                    Level = (int)entry.Level,
                    Message = entry.Message,
                    StackTrace = entry.StackTrace,
                    Tag = entry.Tag
                };
            }
        }
    }
}

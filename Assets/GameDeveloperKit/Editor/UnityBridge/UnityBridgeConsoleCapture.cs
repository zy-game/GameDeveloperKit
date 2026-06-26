using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    public static class UnityBridgeConsoleCapture
    {
        private const int DefaultCapacity = 500;

        private static readonly List<LogEntry> s_Entries = new List<LogEntry>(DefaultCapacity);
        private static int s_Head;
        private static int s_Count;
        private static bool s_Initialized;

        public static bool IsCapturing { get; private set; }

        public static void StartCapture()
        {
            if (s_Initialized)
            {
                return;
            }

            s_Initialized = true;
            IsCapturing = true;
            Application.logMessageReceived += OnLogMessageReceived;
        }

        public static void StopCapture()
        {
            if (!s_Initialized)
            {
                return;
            }

            Application.logMessageReceived -= OnLogMessageReceived;
            IsCapturing = false;
        }

        private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            var entry = new LogEntry
            {
                Message = logString,
                StackTrace = stackTrace,
                Type = type,
                Timestamp = DateTime.Now
            };

            if (s_Entries.Count < DefaultCapacity)
            {
                s_Entries.Add(entry);
            }
            else
            {
                s_Entries[s_Head] = entry;
            }

            s_Head = (s_Head + 1) % DefaultCapacity;
            if (s_Count < DefaultCapacity)
            {
                s_Count++;
            }
        }

        public static List<LogEntry> GetLogs(LogType? levelFilter = null, int count = 50)
        {
            var result = new List<LogEntry>(Math.Min(count, s_Count));

            int startIndex;
            if (s_Entries.Count < DefaultCapacity)
            {
                startIndex = s_Count - 1;
            }
            else
            {
                startIndex = (s_Head - 1 + DefaultCapacity) % DefaultCapacity;
            }

            int collected = 0;
            int index = startIndex;

            while (collected < s_Count && result.Count < count)
            {
                var entry = s_Entries[index];
                if (levelFilter == null || entry.Type == levelFilter.Value)
                {
                    result.Add(entry);
                }

                collected++;
                index = (index - 1 + DefaultCapacity) % DefaultCapacity;
            }

            return result;
        }

        public static void Clear()
        {
            s_Entries.Clear();
            s_Head = 0;
            s_Count = 0;
        }

        [Serializable]
        public struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public string TypeName;
            public string TimestampText;

            [NonSerialized] public LogType Type;
            [NonSerialized] public DateTime Timestamp;
        }
    }
}

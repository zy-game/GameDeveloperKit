using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Logger
{
    public sealed class DebugLogBuffer
    {
        private const int DefaultCapacity = 256;
        private readonly List<LogEntry> m_Entries = new List<LogEntry>();

        public DebugLogBuffer() : this(DefaultCapacity)
        {
        }

        public DebugLogBuffer(int capacity)
        {
            SetCapacity(capacity);
        }

        public int Capacity { get; private set; }

        public IReadOnlyList<LogEntry> Snapshot(DebugLogQuery? query = null)
        {
            var entries = new List<LogEntry>();
            foreach (var entry in m_Entries)
            {
                if (!query.HasValue || query.Value.Matches(entry))
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        public void Clear()
        {
            m_Entries.Clear();
        }

        internal void Append(LogEntry entry)
        {
            m_Entries.Add(entry);
            while (m_Entries.Count > Capacity)
            {
                m_Entries.RemoveAt(0);
            }
        }

        internal void SetCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentException("Log buffer capacity must be greater than zero.", nameof(capacity));
            }

            Capacity = capacity;
            while (m_Entries.Count > Capacity)
            {
                m_Entries.RemoveAt(0);
            }
        }
    }
}

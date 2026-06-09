using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Logger
{
    public sealed class DebugLogBuffer
    {
        private const int DefaultCapacity = 256;
        private DebugLogRecord[] m_Entries = Array.Empty<DebugLogRecord>();
        private int m_Start;
        private int m_Count;

        public DebugLogBuffer() : this(DefaultCapacity)
        {
        }

        public DebugLogBuffer(int capacity)
        {
            SetCapacity(capacity);
        }

        public int Capacity { get; private set; }

        public IReadOnlyList<DebugLogRecord> Snapshot(DebugLogQuery? query = null)
        {
            var entries = new List<DebugLogRecord>();
            for (var i = 0; i < m_Count; i++)
            {
                var entry = m_Entries[(m_Start + i) % Capacity];
                if (!query.HasValue || query.Value.Matches(entry))
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        public void Clear()
        {
            Array.Clear(m_Entries, 0, m_Entries.Length);
            m_Start = 0;
            m_Count = 0;
        }

        internal void Append(DebugLogRecord entry)
        {
            if (m_Count < Capacity)
            {
                m_Entries[(m_Start + m_Count) % Capacity] = entry;
                m_Count++;
                return;
            }

            m_Entries[m_Start] = entry;
            m_Start = (m_Start + 1) % Capacity;
        }

        internal void SetCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentException("Log buffer capacity must be greater than zero.", nameof(capacity));
            }

            var snapshot = Snapshot();
            Capacity = capacity;
            m_Entries = new DebugLogRecord[Capacity];
            m_Start = 0;
            m_Count = 0;
            var skip = Math.Max(0, snapshot.Count - Capacity);
            for (var i = skip; i < snapshot.Count; i++)
            {
                Append(snapshot[i]);
            }
        }
    }
}

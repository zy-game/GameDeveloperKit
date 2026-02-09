using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 日志缓存系统（环形缓冲区）
    /// </summary>
    public class LogCache
    {
        private readonly LogEntry[] _buffer;
        private readonly int _capacity;
        private int _head;
        private int _count;
        private readonly object _lock = new();

        public event Action<LogEntry> OnLogAdded;

        public int Count
        {
            get { lock (_lock) return _count; }
        }

        public int Capacity => _capacity;

        public LogCache(int capacity = 1000)
        {
            _capacity = capacity;
            _buffer = new LogEntry[capacity];
            _head = 0;
            _count = 0;
        }

        public void Add(LogEntry entry)
        {
            lock (_lock)
            {
                _buffer[_head] = entry;
                _head = (_head + 1) % _capacity;
                if (_count < _capacity) _count++;
            }
            OnLogAdded?.Invoke(entry);
        }

        public void Add(LogLevel level, string message, string stackTrace = null, string tag = null)
        {
            Add(new LogEntry(level, message, stackTrace, tag));
        }

        public List<LogEntry> GetAll()
        {
            lock (_lock)
            {
                var result = new List<LogEntry>(_count);
                int start = _count < _capacity ? 0 : _head;
                for (int i = 0; i < _count; i++)
                {
                    result.Add(_buffer[(start + i) % _capacity]);
                }
                return result;
            }
        }

        public List<LogEntry> GetByLevel(LogLevel level)
        {
            lock (_lock)
            {
                var result = new List<LogEntry>();
                int start = _count < _capacity ? 0 : _head;
                for (int i = 0; i < _count; i++)
                {
                    var entry = _buffer[(start + i) % _capacity];
                    if (entry.Level == level) result.Add(entry);
                }
                return result;
            }
        }

        public List<LogEntry> GetByMinLevel(LogLevel minLevel)
        {
            lock (_lock)
            {
                var result = new List<LogEntry>();
                int start = _count < _capacity ? 0 : _head;
                for (int i = 0; i < _count; i++)
                {
                    var entry = _buffer[(start + i) % _capacity];
                    if (entry.Level >= minLevel) result.Add(entry);
                }
                return result;
            }
        }

        public List<LogEntry> Search(string keyword, LogLevel? level = null)
        {
            lock (_lock)
            {
                var result = new List<LogEntry>();
                int start = _count < _capacity ? 0 : _head;
                for (int i = 0; i < _count; i++)
                {
                    var entry = _buffer[(start + i) % _capacity];
                    if (level.HasValue && entry.Level != level.Value) continue;
                    if (string.IsNullOrEmpty(keyword) || 
                        entry.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(entry);
                    }
                }
                return result;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _head = 0;
                _count = 0;
            }
        }
    }
}

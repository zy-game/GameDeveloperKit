using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Logger
{
    public sealed class LoggerModule : GameModuleBase
    {
        private const string DefaultCategory = "Default";
        private readonly Dictionary<string, bool> m_CategoryStates = new Dictionary<string, bool>();
        private readonly List<ILogSink> m_Sinks = new List<ILogSink>();

        public bool Enabled { get; set; } = true;

        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        internal Exception LastSinkException { get; private set; }

        public override UniTask Startup()
        {
            Enabled = true;
            MinimumLevel = LogLevel.Info;
            m_CategoryStates.Clear();
            LastSinkException = null;
            ClearSinks();
            AddSink(new UnityConsoleLogSink());
            return UniTask.CompletedTask;
        }

        public override UniTask Shutdown()
        {
            ClearSinks();
            m_CategoryStates.Clear();
            LastSinkException = null;
            Enabled = true;
            MinimumLevel = LogLevel.Info;
            return UniTask.CompletedTask;
        }

        public void Trace(string message, string category = null, object context = null)
        {
            Log(LogLevel.Trace, message, category, context);
        }

        public void Debug(string message, string category = null, object context = null)
        {
            Log(LogLevel.Debug, message, category, context);
        }

        public void Info(string message, string category = null, object context = null)
        {
            Log(LogLevel.Info, message, category, context);
        }

        public void Warning(string message, string category = null, object context = null)
        {
            Log(LogLevel.Warning, message, category, context);
        }

        public void Error(string message, string category = null, object context = null)
        {
            Log(LogLevel.Error, message, category, context);
        }

        public void Error(Exception exception, string message = null, string category = null, object context = null)
        {
            Log(LogLevel.Error, exception, message, category, context);
        }

        public void Fatal(string message, string category = null, object context = null)
        {
            Log(LogLevel.Fatal, message, category, context);
        }

        public void Fatal(Exception exception, string message = null, string category = null, object context = null)
        {
            Log(LogLevel.Fatal, exception, message, category, context);
        }

        public void Log(LogLevel level, string message, string category = null, object context = null)
        {
            Log(level, null, message, category, context);
        }

        public void Log(LogLevel level, Exception exception, string message = null, string category = null, object context = null)
        {
            ValidateLevel(level);
            var normalizedCategory = NormalizeCategory(category);
            if (!ShouldWrite(level, normalizedCategory))
            {
                return;
            }

            var entry = new LogEntry(
                DateTimeOffset.Now,
                level,
                normalizedCategory,
                message ?? string.Empty,
                exception,
                context);

            Write(entry);
        }

        public void AddSink(ILogSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (m_Sinks.Contains(sink))
            {
                return;
            }

            m_Sinks.Add(sink);
        }

        public void RemoveSink(ILogSink sink)
        {
            if (sink == null)
            {
                return;
            }

            m_Sinks.Remove(sink);
        }

        public void ClearSinks()
        {
            m_Sinks.Clear();
        }

        public void SetCategoryEnabled(string category, bool enabled)
        {
            m_CategoryStates[NormalizeCategory(category)] = enabled;
        }

        public bool IsCategoryEnabled(string category)
        {
            var normalizedCategory = NormalizeCategory(category);
            return !m_CategoryStates.TryGetValue(normalizedCategory, out var enabled) || enabled;
        }

        private bool ShouldWrite(LogLevel level, string category)
        {
            return Enabled &&
                   MinimumLevel != LogLevel.Off &&
                   level >= MinimumLevel &&
                   IsCategoryEnabled(category) &&
                   m_Sinks.Count > 0;
        }

        private void Write(LogEntry entry)
        {
            var sinks = m_Sinks.ToArray();
            foreach (var sink in sinks)
            {
                try
                {
                    sink.Write(entry);
                }
                catch (Exception exception)
                {
                    LastSinkException = exception;
                }
            }
        }

        private static void ValidateLevel(LogLevel level)
        {
            if (level < LogLevel.Trace || level > LogLevel.Fatal)
            {
                throw new ArgumentException("Log level must be Trace, Debug, Info, Warning, Error or Fatal.", nameof(level));
            }
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? DefaultCategory : category;
        }
    }
}

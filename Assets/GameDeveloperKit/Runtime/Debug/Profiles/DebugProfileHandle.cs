using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Logger
{
    public sealed class DebugProfileHandle : ProfileHandle
    {
        private const string DefaultCategory = "Default";

        private readonly DebugSettings m_Settings;
        private readonly Func<bool> m_IsModuleEnabled;
        private readonly Func<long> m_GetTimerTick;
        private readonly Dictionary<string, bool> m_CategoryStates = new Dictionary<string, bool>();

        private long m_LogSequence;

        public DebugProfileHandle() : this(new DebugSettings(), null, null)
        {
        }

        internal DebugProfileHandle(DebugSettings settings, Func<bool> isModuleEnabled, Func<long> getTimerTick)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_IsModuleEnabled = isModuleEnabled ?? (() => true);
            m_GetTimerTick = getTimerTick ?? (() => 0L);
            Logs = new DebugLogBuffer(m_Settings.LogCapacity);
        }

        public override string Name => "Debug";

        public DebugLogBuffer Logs { get; }

        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        public bool Enabled { get; set; } = true;

        public void Reset()
        {
            MinimumLevel = LogLevel.Info;
            m_LogSequence = 0L;
            m_CategoryStates.Clear();
            Logs.SetCapacity(m_Settings.LogCapacity);
            Logs.Clear();
            Enabled = true;
        }

        public void Shutdown()
        {
            m_CategoryStates.Clear();
            Logs.Clear();
            MinimumLevel = LogLevel.Info;
            Enabled = true;
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
            WriteLog(level, exception, message, category, context, null);
        }

        internal void WriteLog(
            LogLevel level,
            Exception exception,
            string message,
            string category,
            object context,
            IReadOnlyList<string> tags)
        {
            ValidateLevel(level);
            var normalizedCategory = NormalizeCategory(category);
            if (!ShouldWrite(level, normalizedCategory))
            {
                return;
            }

            var record = new DebugLogRecord(
                DateTimeOffset.Now,
                ++m_LogSequence,
                UnityEngine.Time.frameCount,
                m_GetTimerTick(),
                level,
                RedactLogText(normalizedCategory),
                RedactLogText(message ?? string.Empty),
                RedactException(exception),
                RedactContext(context),
                RedactTags(tags));

            Logs.Append(record);
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

        protected internal override void Draw()
        {
            GUILayout.Label($"Enabled: {Enabled}");
            GUILayout.Label($"Minimum Level: {MinimumLevel}");
            GUILayout.Label($"Logs: {Logs.Snapshot().Count}");
            GUILayout.Label($"Categories: {m_CategoryStates.Count}");
        }

        private bool ShouldWrite(LogLevel level, string category)
        {
            return Enabled &&
                   m_IsModuleEnabled() &&
                   MinimumLevel != LogLevel.Off &&
                   level >= MinimumLevel &&
                   IsCategoryEnabled(category);
        }

        private string RedactLogText(string value)
        {
            return m_Settings.RedactionEnabled ? DebugRedactionUtility.Redact(value) : value;
        }

        private Exception RedactException(Exception exception)
        {
            if (!m_Settings.RedactionEnabled || exception == null)
            {
                return exception;
            }

            var text = SafeToString(exception, out var failed);
            var redacted = DebugRedactionUtility.Redact(text);
            return failed || redacted != text ? new RedactedLogException(redacted) : exception;
        }

        private object RedactContext(object context)
        {
            if (!m_Settings.RedactionEnabled || context == null)
            {
                return context;
            }

            return DebugRedactionUtility.Redact(SafeToString(context, out _));
        }

        private IReadOnlyList<string> RedactTags(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return Array.Empty<string>();
            }

            if (!m_Settings.RedactionEnabled)
            {
                return tags;
            }

            var redactedTags = new string[tags.Count];
            for (var i = 0; i < tags.Count; i++)
            {
                redactedTags[i] = DebugRedactionUtility.Redact(tags[i]);
            }

            return redactedTags;
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

        private static string SafeToString(object value, out bool failed)
        {
            failed = false;
            if (value == null)
            {
                return string.Empty;
            }

            try
            {
                return value.ToString();
            }
            catch (Exception exception)
            {
                failed = true;
                return $"<{value.GetType().Name}.ToString failed: {exception.GetType().Name}>";
            }
        }

        private sealed class RedactedLogException : Exception
        {
            private readonly string m_Text;

            public RedactedLogException(string text) : base(text)
            {
                m_Text = text;
            }

            public override string ToString()
            {
                return m_Text;
            }
        }
    }
}

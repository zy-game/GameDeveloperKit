using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Debugger
{
    public sealed partial class DebugProfileHandle : ProfileHandle
    {
        private const string DefaultCategory = "Default";
        private readonly DebugSettings m_Settings;
        private readonly Func<bool> m_IsModuleEnabled;
        private readonly Func<long> m_GetTimerTick;
        private readonly Dictionary<string, bool> m_CategoryStates = new Dictionary<string, bool>();
        private long m_LogSequence;
        private bool m_SuppressUnityConsoleOutput;

        /// <summary>
        /// 初始化 Debug Profile Handle。
        /// </summary>
        public DebugProfileHandle() : this(new DebugSettings(), null, null)
        {
        }

        /// <summary>
        /// 初始化 Debug Profile Handle。
        /// </summary>
        /// <param name="isModuleEnabled">is Module Enabled 参数。</param>
        /// <param name="getTimerTick">get Timer Tick 参数。</param>
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

        internal bool IsWritingUnityConsoleOutput => m_SuppressUnityConsoleOutput;

        /// <summary>
        /// 重置 member。
        /// </summary>
        public void Reset()
        {
            MinimumLevel = LogLevel.Info;
            m_LogSequence = 0L;
            m_CategoryStates.Clear();
            Logs.SetCapacity(m_Settings.LogCapacity);
            Logs.Clear();
            Enabled = true;
        }

        /// <summary>
        /// 关闭 member。
        /// </summary>
        public void Shutdown()
        {
            m_CategoryStates.Clear();
            Logs.Clear();
            MinimumLevel = LogLevel.Info;
            Enabled = true;
        }

        /// <summary>
        /// 执行 Trace。
        /// </summary>
        public void Trace(string message, string category = null, object context = null)
        {
            Log(LogLevel.Trace, message, category, context);
        }

        /// <summary>
        /// 执行 Debug。
        /// </summary>
        public void Debug(string message, string category = null, object context = null)
        {
            Log(LogLevel.Debug, message, category, context);
        }

        /// <summary>
        /// 执行 Info。
        /// </summary>
        public void Info(string message, string category = null, object context = null)
        {
            Log(LogLevel.Info, message, category, context);
        }

        /// <summary>
        /// 执行 Warning。
        /// </summary>
        public void Warning(string message, string category = null, object context = null)
        {
            Log(LogLevel.Warning, message, category, context);
        }

        /// <summary>
        /// 执行 Error。
        /// </summary>
        public void Error(string message, string category = null, object context = null)
        {
            Log(LogLevel.Error, message, category, context);
        }

        /// <summary>
        /// 执行 Error。
        /// </summary>
        public void Error(Exception exception, string message = null, string category = null, object context = null)
        {
            Log(LogLevel.Error, exception, message, category, context);
        }

        /// <summary>
        /// 执行 Fatal。
        /// </summary>
        public void Fatal(string message, string category = null, object context = null)
        {
            Log(LogLevel.Fatal, message, category, context);
        }

        /// <summary>
        /// 执行 Fatal。
        /// </summary>
        public void Fatal(Exception exception, string message = null, string category = null, object context = null)
        {
            Log(LogLevel.Fatal, exception, message, category, context);
        }

        /// <summary>
        /// 执行 Log。
        /// </summary>
        public void Log(LogLevel level, string message, string category = null, object context = null)
        {
            Log(level, null, message, category, context);
        }

        /// <summary>
        /// 执行 Log。
        /// </summary>
        public void Log(LogLevel level, Exception exception, string message = null, string category = null, object context = null)
        {
            WriteLog(level, exception, message, category, context, null);
        }

        /// <summary>
        /// 写入 Log。
        /// </summary>
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
            WriteUnityConsole(record);
        }

        /// <summary>
        /// 设置 Category Enabled。
        /// </summary>
        public void SetCategoryEnabled(string category, bool enabled)
        {
            m_CategoryStates[NormalizeCategory(category)] = enabled;
        }

        /// <summary>
        /// 执行 Is Category Enabled。
        /// </summary>
        public bool IsCategoryEnabled(string category)
        {
            var normalizedCategory = NormalizeCategory(category);
            return !m_CategoryStates.TryGetValue(normalizedCategory, out var enabled) || enabled;
        }

        /// <summary>
        /// 绘制 member。
        /// </summary>
        protected internal override void Draw()
        {
            GUILayout.Label($"Enabled: {Enabled}");
            GUILayout.Label($"Minimum Level: {MinimumLevel}");
            GUILayout.Label($"Logs: {Logs.Snapshot().Count}");
            GUILayout.Label($"Categories: {m_CategoryStates.Count}");
        }

        /// <summary>
        /// 执行 Should Write。
        /// </summary>
        private bool ShouldWrite(LogLevel level, string category)
        {
            return Enabled &&
                   m_IsModuleEnabled() &&
                   MinimumLevel != LogLevel.Off &&
                   level >= MinimumLevel &&
                   IsCategoryEnabled(category);
        }

        private void WriteUnityConsole(DebugLogRecord record)
        {
            if (!m_Settings.UnityConsoleOutputEnabled || m_SuppressUnityConsoleOutput)
            {
                return;
            }

            var message = FormatUnityConsoleMessage(record);
            try
            {
                m_SuppressUnityConsoleOutput = true;
                switch (record.Level)
                {
                    case LogLevel.Warning:
                        UnityEngine.Debug.LogWarning(message);
                        break;
                    case LogLevel.Error:
                    case LogLevel.Fatal:
                        UnityEngine.Debug.LogError(message);
                        break;
                    default:
                        UnityEngine.Debug.Log(message);
                        break;
                }
            }
            finally
            {
                m_SuppressUnityConsoleOutput = false;
            }
        }

        private static string FormatUnityConsoleMessage(DebugLogRecord record)
        {
            var message = $"[{record.Level}][{record.Category}] {record.Message}";
            if (record.Context != null)
            {
                message += $" Context: {record.Context}";
            }

            return message;
        }

        /// <summary>
        /// 执行 Redact Log Text。
        /// </summary>
        private string RedactLogText(string value)
        {
            return m_Settings.RedactionEnabled ? DebugRedactionUtility.Redact(value) : value;
        }

        /// <summary>
        /// 执行 Redact Exception。
        /// </summary>
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

        /// <summary>
        /// 执行 Redact Context。
        /// </summary>
        private object RedactContext(object context)
        {
            if (!m_Settings.RedactionEnabled || context == null)
            {
                return context;
            }

            return DebugRedactionUtility.Redact(SafeToString(context, out _));
        }

        /// <summary>
        /// 执行 Redact Tags。
        /// </summary>
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

        /// <summary>
        /// 校验 Level。
        /// </summary>
        private static void ValidateLevel(LogLevel level)
        {
            if (level < LogLevel.Trace || level > LogLevel.Fatal)
            {
                throw new ArgumentException("Log level must be Trace, Debug, Info, Warning, Error or Fatal.", nameof(level));
            }
        }

        /// <summary>
        /// 执行 Normalize Category。
        /// </summary>
        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? DefaultCategory : category;
        }

        /// <summary>
        /// 执行 Safe To String。
        /// </summary>
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

    }
}

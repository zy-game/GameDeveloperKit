using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Debug Profile Handle 类型。
    /// </summary>
    public sealed partial class DebugProfileHandle : ProfileHandle
    {
        /// <summary>
        /// 定义 Default Category 常量。
        /// </summary>
        private const string DefaultCategory = "Default";

        /// <summary>
        /// 存储 Settings。
        /// </summary>
        private readonly DebugSettings m_Settings;
        /// <summary>
        /// 记录 Is Module Enabled 状态。
        /// </summary>
        private readonly Func<bool> m_IsModuleEnabled;
        /// <summary>
        /// 存储 Get Timer Tick。
        /// </summary>
        private readonly Func<long> m_GetTimerTick;
        /// <summary>
        /// 记录 Category States 状态。
        /// </summary>
        private readonly Dictionary<string, bool> m_CategoryStates = new Dictionary<string, bool>();

        /// <summary>
        /// 存储 Log Sequence。
        /// </summary>
        private long m_LogSequence;

        /// <summary>
        /// 初始化 Debug Profile Handle。
        /// </summary>
        public DebugProfileHandle() : this(new DebugSettings(), null, null)
        {
        }

        /// <summary>
        /// 初始化 Debug Profile Handle。
        /// </summary>
        /// <param name="settings">settings 参数。</param>
        /// <param name="isModuleEnabled">is Module Enabled 参数。</param>
        /// <param name="getTimerTick">get Timer Tick 参数。</param>
        internal DebugProfileHandle(DebugSettings settings, Func<bool> isModuleEnabled, Func<long> getTimerTick)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_IsModuleEnabled = isModuleEnabled ?? (() => true);
            m_GetTimerTick = getTimerTick ?? (() => 0L);
            Logs = new DebugLogBuffer(m_Settings.LogCapacity);
        }

        /// <summary>
        /// 存储 Name。
        /// </summary>
        public override string Name => "Debug";

        public DebugLogBuffer Logs { get; }

        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        public bool Enabled { get; set; } = true;

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
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Trace(string message, string category = null, object context = null)
        {
            Log(LogLevel.Trace, message, category, context);
        }

        /// <summary>
        /// 执行 Debug。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Debug(string message, string category = null, object context = null)
        {
            Log(LogLevel.Debug, message, category, context);
        }

        /// <summary>
        /// 执行 Info。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Info(string message, string category = null, object context = null)
        {
            Log(LogLevel.Info, message, category, context);
        }

        /// <summary>
        /// 执行 Warning。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Warning(string message, string category = null, object context = null)
        {
            Log(LogLevel.Warning, message, category, context);
        }

        /// <summary>
        /// 执行 Error。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Error(string message, string category = null, object context = null)
        {
            Log(LogLevel.Error, message, category, context);
        }

        /// <summary>
        /// 执行 Error。
        /// </summary>
        /// <param name="exception">exception 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Error(Exception exception, string message = null, string category = null, object context = null)
        {
            Log(LogLevel.Error, exception, message, category, context);
        }

        /// <summary>
        /// 执行 Fatal。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Fatal(string message, string category = null, object context = null)
        {
            Log(LogLevel.Fatal, message, category, context);
        }

        /// <summary>
        /// 执行 Fatal。
        /// </summary>
        /// <param name="exception">exception 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Fatal(Exception exception, string message = null, string category = null, object context = null)
        {
            Log(LogLevel.Fatal, exception, message, category, context);
        }

        /// <summary>
        /// 执行 Log。
        /// </summary>
        /// <param name="level">level 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Log(LogLevel level, string message, string category = null, object context = null)
        {
            Log(level, null, message, category, context);
        }

        /// <summary>
        /// 执行 Log。
        /// </summary>
        /// <param name="level">level 参数。</param>
        /// <param name="exception">exception 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Log(LogLevel level, Exception exception, string message = null, string category = null, object context = null)
        {
            WriteLog(level, exception, message, category, context, null);
        }

        /// <summary>
        /// 写入 Log。
        /// </summary>
        /// <param name="level">level 参数。</param>
        /// <param name="exception">exception 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        /// <param name="tags">tags 参数。</param>
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

        /// <summary>
        /// 设置 Category Enabled。
        /// </summary>
        /// <param name="category">category 参数。</param>
        /// <param name="enabled">enabled 参数。</param>
        public void SetCategoryEnabled(string category, bool enabled)
        {
            m_CategoryStates[NormalizeCategory(category)] = enabled;
        }

        /// <summary>
        /// 执行 Is Category Enabled。
        /// </summary>
        /// <param name="category">category 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool IsCategoryEnabled(string category)
        {
            var normalizedCategory = NormalizeCategory(category);
            return !m_CategoryStates.TryGetValue(normalizedCategory, out var enabled) || enabled;
        }

        /// <summary>
        /// 绘制 member。
        /// </summary>
        /// <returns>执行结果。</returns>
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
        /// <param name="level">level 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        private bool ShouldWrite(LogLevel level, string category)
        {
            return Enabled &&
                   m_IsModuleEnabled() &&
                   MinimumLevel != LogLevel.Off &&
                   level >= MinimumLevel &&
                   IsCategoryEnabled(category);
        }

        /// <summary>
        /// 执行 Redact Log Text。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <returns>执行结果。</returns>
        private string RedactLogText(string value)
        {
            return m_Settings.RedactionEnabled ? DebugRedactionUtility.Redact(value) : value;
        }

        /// <summary>
        /// 执行 Redact Exception。
        /// </summary>
        /// <param name="exception">exception 参数。</param>
        /// <returns>执行结果。</returns>
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
        /// <param name="context">context 参数。</param>
        /// <returns>执行结果。</returns>
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
        /// <param name="tags">tags 参数。</param>
        /// <returns>执行结果。</returns>
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
        /// <param name="level">level 参数。</param>
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
        /// <param name="category">category 参数。</param>
        /// <returns>执行结果。</returns>
        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? DefaultCategory : category;
        }

        /// <summary>
        /// 执行 Safe To String。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <param name="failed">failed 参数。</param>
        /// <returns>执行结果。</returns>
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

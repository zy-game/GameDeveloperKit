using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 诊断日志条目，记录单条诊断日志信息
    /// </summary>
    public sealed class DiagnosticsLogEntry
    {
        /// <summary>
        /// 初始化诊断日志条目
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">日志消息</param>
        /// <param name="context">上下文信息</param>
        /// <param name="scope">作用域</param>
        /// <param name="fields">附加字段</param>
        public DiagnosticsLogEntry(DiagnosticsLogLevel level, string message, string context, string scope, IReadOnlyDictionary<string, string> fields = null)
        {
            Level = level;
            Message = message ?? string.Empty;
            Context = context ?? string.Empty;
            Scope = scope ?? string.Empty;
            Fields = fields ?? new Dictionary<string, string>(StringComparer.Ordinal);
            Timestamp = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// 获取日志级别
        /// </summary>
        public DiagnosticsLogLevel Level { get; }

        /// <summary>
        /// 获取日志消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 获取上下文信息
        /// </summary>
        public string Context { get; }

        /// <summary>
        /// 获取作用域
        /// </summary>
        public string Scope { get; }

        /// <summary>
        /// 获取附加字段集合
        /// </summary>
        public IReadOnlyDictionary<string, string> Fields { get; }

        /// <summary>
        /// 获取时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; }
    }
}

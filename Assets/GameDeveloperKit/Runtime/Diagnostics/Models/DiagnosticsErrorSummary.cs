using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 诊断错误摘要，用于聚合和统计错误信息
    /// </summary>
    public sealed class DiagnosticsErrorSummary
    {
        /// <summary>
        /// 初始化诊断错误摘要
        /// </summary>
        /// <param name="key">错误键</param>
        /// <param name="message">错误消息</param>
        /// <param name="context">上下文信息</param>
        /// <param name="scope">作用域</param>
        public DiagnosticsErrorSummary(string key, string message, string context, string scope)
        {
            Key = key;
            Message = message ?? string.Empty;
            Context = context ?? string.Empty;
            Scope = scope ?? string.Empty;
        }

        /// <summary>
        /// 获取错误键
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 获取错误消息
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// 获取上下文信息
        /// </summary>
        public string Context { get; private set; }

        /// <summary>
        /// 获取作用域
        /// </summary>
        public string Scope { get; private set; }

        /// <summary>
        /// 获取错误发生次数
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// 获取最后一次发生时间
        /// </summary>
        public DateTimeOffset LastOccurredAt { get; private set; }

        /// <summary>
        /// 记录错误发生
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="context">上下文信息</param>
        /// <param name="scope">作用域</param>
        public void Track(string message, string context, string scope)
        {
            Message = message ?? string.Empty;
            Context = context ?? string.Empty;
            Scope = scope ?? string.Empty;
            Count++;
            LastOccurredAt = DateTimeOffset.UtcNow;
        }
    }
}

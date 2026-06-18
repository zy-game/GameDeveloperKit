using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Debug Log Network Bridge 类型。
    /// </summary>
    public sealed class DebugLogNetworkBridge
    {
        /// <summary>
        /// 存储 Logs。
        /// </summary>
        private readonly DebugLogBuffer m_Logs;
        /// <summary>
        /// 存储 Sender。
        /// </summary>
        private readonly IDebugLogNetworkSender m_Sender;

        /// <summary>
        /// 初始化 Debug Log Network Bridge。
        /// </summary>
        /// <param name="logs">logs 参数。</param>
        /// <param name="sender">sender 参数。</param>
        public DebugLogNetworkBridge(DebugLogBuffer logs, IDebugLogNetworkSender sender)
        {
            m_Logs = logs ?? throw new ArgumentNullException(nameof(logs));
            m_Sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public long LastSentSequence { get; private set; }

        /// <summary>
        /// 执行 Flush Async。
        /// </summary>
        /// <returns>操作完成任务。</returns>
        public async UniTask<int> FlushAsync()
        {
            var sentCount = 0;
            var records = m_Logs.Snapshot();
            foreach (var record in records)
            {
                if (record.Sequence <= LastSentSequence)
                {
                    continue;
                }

                await m_Sender.SendDebugLogAsync(ToPayload(record));
                LastSentSequence = record.Sequence;
                sentCount++;
            }

            return sentCount;
        }

        /// <summary>
        /// 转换为 Payload。
        /// </summary>
        /// <param name="record">record 参数。</param>
        /// <returns>执行结果。</returns>
        public static DebugLogPayload ToPayload(DebugLogRecord record)
        {
            return new DebugLogPayload(
                record.Sequence,
                record.Timestamp,
                record.FrameCount,
                record.TimerTick,
                record.Level.ToString(),
                record.Category ?? string.Empty,
                record.Message ?? string.Empty,
                SafeToString(record.Exception),
                SafeToString(record.Context),
                record.Tags);
        }

        /// <summary>
        /// 安全转换字符串。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <returns>执行结果。</returns>
        private static string SafeToString(object value)
        {
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
                return $"<{value.GetType().Name}.ToString failed: {exception.GetType().Name}>";
            }
        }
    }
}

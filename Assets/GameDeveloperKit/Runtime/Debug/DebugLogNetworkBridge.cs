using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Debugger
{
    public sealed class DebugLogNetworkBridge
    {
        private readonly DebugLogBuffer m_Logs;
        private readonly IDebugLogNetworkSender m_Sender;

        /// <summary>
        /// 初始化 Debug Log Network Bridge。
        /// </summary>
        public DebugLogNetworkBridge(DebugLogBuffer logs, IDebugLogNetworkSender sender)
        {
            m_Logs = logs ?? throw new ArgumentNullException(nameof(logs));
            m_Sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public long LastSentSequence { get; private set; }

        /// <summary>
        /// 执行 Flush Async。
        /// </summary>
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

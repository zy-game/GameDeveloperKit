using System;

namespace GameDeveloperKit.Operation
{
    public partial class OperationModule
    {
        /// <summary>
        /// 运行中操作的复合键，由业务键和操作类型共同决定唯一性。
        /// </summary>
        private readonly struct OperationKey : IEquatable<OperationKey>
        {
            /// <summary>
            /// 业务操作键。
            /// </summary>
            public readonly object Key;
            private readonly Type m_OperationType;

            /// <summary>
            /// 初始化运行中操作复合键。
            /// </summary>
            /// <param name="key">业务操作键。</param>
            /// <param name="operationType">操作句柄类型。</param>
            public OperationKey(object key, Type operationType)
            {
                Key = key;
                m_OperationType = operationType;
            }

            /// <summary>
            /// 判断两个操作键是否相等。
            /// </summary>
            /// <param name="other">另一个操作键。</param>
            /// <returns>如果业务键和操作类型都相等，则返回true；否则返回false。</returns>
            public bool Equals(OperationKey other)
            {
                return Equals(Key, other.Key) && m_OperationType == other.m_OperationType;
            }

            /// <summary>
            /// 判断指定对象是否与当前操作键相等。
            /// </summary>
            /// <param name="obj">待比较对象。</param>
            /// <returns>如果对象是相等的操作键，则返回true；否则返回false。</returns>
            public override bool Equals(object obj)
            {
                return obj is OperationKey other && Equals(other);
            }

            /// <summary>
            /// 获取操作键哈希码。
            /// </summary>
            /// <returns>操作键哈希码。</returns>
            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Key != null ? Key.GetHashCode() : 0) * 397) ^ (m_OperationType != null ? m_OperationType.GetHashCode() : 0);
                }
            }
        }

        /// <summary>
        /// 一次操作执行在运行表中的稳定身份。
        /// </summary>
        private sealed class OperationEntry
        {
            public OperationKey Key { get; }
            public OperationHandle Operation { get; }
            public Cysharp.Threading.Tasks.UniTask Completion { get; }

            public OperationEntry(OperationKey key, OperationHandle operation)
            {
                Key = key;
                Operation = operation;
                Completion = operation.WaitCompletionAsync();
            }
        }
    }
}

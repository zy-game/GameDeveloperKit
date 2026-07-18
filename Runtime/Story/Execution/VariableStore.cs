using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 默认剧情变量存储。
    /// </summary>
    public sealed class VariableStore : IVariableStore
    {
        private readonly Dictionary<string, Value> m_Values =
            new Dictionary<string, Value>(StringComparer.Ordinal);

        /// <summary>
        /// 尝试读取变量。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <param name="value">变量值。</param>
        /// <returns>找到时返回 true。</returns>
        public bool TryGet(string name, out Value value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                value = default;
                return false;
            }

            return m_Values.TryGetValue(name, out value);
        }

        /// <summary>
        /// 写入变量。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <param name="value">变量值。</param>
        public void Set(string name, Value value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(name));
            }

            m_Values[name] = value;
        }

        /// <summary>
        /// 清空所有变量。
        /// </summary>
        public void Clear()
        {
            m_Values.Clear();
        }

        /// <summary>
        /// 获取变量快照。
        /// </summary>
        /// <returns>变量快照。</returns>
        public IReadOnlyDictionary<string, Value> Snapshot()
        {
            return new Dictionary<string, Value>(m_Values, StringComparer.Ordinal);
        }
    }
}

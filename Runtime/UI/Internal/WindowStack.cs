using System.Collections.Generic;

namespace GameDeveloperKit.UI.Internal
{
    internal sealed class WindowStack
    {
        private readonly List<WindowRecord> m_Records = new List<WindowRecord>();
        public int Count => m_Records.Count;
        public WindowRecord Top => m_Records.Count == 0 ? null : m_Records[m_Records.Count - 1];
        public IReadOnlyList<WindowRecord> Records => m_Records;

        /// <summary>
        /// 执行 Push。
        /// </summary>
        public void Push(WindowRecord record)
        {
            if (record == null)
            {
                return;
            }

            Remove(record);
            m_Records.Add(record);
        }

        /// <summary>
        /// 移除 member。
        /// </summary>
        public bool Remove(WindowRecord record)
        {
            return record != null && m_Records.Remove(record);
        }

        /// <summary>
        /// 清理 member。
        /// </summary>
        public void Clear()
        {
            m_Records.Clear();
        }
    }
}

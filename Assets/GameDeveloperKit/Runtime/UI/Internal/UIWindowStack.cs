using System.Collections.Generic;

namespace GameDeveloperKit.UI.Internal
{
    internal sealed class UIWindowStack
    {
        private readonly List<UIWindowRecord> m_Records = new List<UIWindowRecord>();
        public int Count => m_Records.Count;
        public UIWindowRecord Top => m_Records.Count == 0 ? null : m_Records[m_Records.Count - 1];

        /// <summary>
        /// 执行 Push。
        /// </summary>
        public void Push(UIWindowRecord record)
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
        public bool Remove(UIWindowRecord record)
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

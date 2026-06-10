using System.Collections.Generic;

namespace GameDeveloperKit.UI.Internal
{
    /// <summary>
    /// 定义 UI Window Stack 类型。
    /// </summary>
    internal sealed class UIWindowStack
    {
        /// <summary>
        /// 存储 Records。
        /// </summary>
        private readonly List<UIWindowRecord> m_Records = new List<UIWindowRecord>();

        /// <summary>
        /// 存储 Count。
        /// </summary>
        public int Count => m_Records.Count;

        /// <summary>
        /// 存储 Top。
        /// </summary>
        public UIWindowRecord Top => m_Records.Count == 0 ? null : m_Records[m_Records.Count - 1];

        /// <summary>
        /// 执行 Push。
        /// </summary>
        /// <param name="record">record 参数。</param>
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
        /// <param name="record">record 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
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

using System.Collections.Generic;

namespace GameDeveloperKit.UI.Internal
{
    internal sealed class UIWindowStack
    {
        private readonly List<UIWindowRecord> m_Records = new List<UIWindowRecord>();

        public int Count => m_Records.Count;

        public UIWindowRecord Top => m_Records.Count == 0 ? null : m_Records[m_Records.Count - 1];

        public void Push(UIWindowRecord record)
        {
            if (record == null)
            {
                return;
            }

            Remove(record);
            m_Records.Add(record);
        }

        public bool Remove(UIWindowRecord record)
        {
            return record != null && m_Records.Remove(record);
        }

        public void Clear()
        {
            m_Records.Clear();
        }
    }
}

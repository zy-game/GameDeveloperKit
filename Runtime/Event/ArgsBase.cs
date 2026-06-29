namespace GameDeveloperKit.Event
{
    /// <summary>
    /// 事件参数基类，用于在事件派发过程中传递事件数据，并记录事件是否已经被消费。
    /// </summary>
    public abstract class ArgsBase : IReference
    {
        private bool m_HasUse;

        /// <summary>
        /// 标记事件参数已经被使用，后续监听器将不再继续处理该事件。
        /// </summary>
        public void Use()
        {
            m_HasUse = true;
        }

        /// <summary>
        /// 获取事件参数是否已经被使用。
        /// </summary>
        /// <returns>如果事件已经被使用，则返回true；否则返回false。</returns>
        public bool HasUse()
        {
            return m_HasUse;
        }

        /// <summary>
        /// 释放事件参数，默认会将事件标记为已使用。
        /// </summary>
        public void Release()
        {
            Use();
        }
    }
}

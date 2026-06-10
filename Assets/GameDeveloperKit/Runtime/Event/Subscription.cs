using System;

namespace GameDeveloperKit.Event
{
    /// <summary>
    /// 事件订阅句柄，用于取消事件订阅并管理订阅生命周期。
    /// </summary>
    public sealed class Subscription : IReference
    {
        /// <summary>
        /// 存储 Module。
        /// </summary>
        private EventModule m_Module;
        /// <summary>
        /// 存储 Listener。
        /// </summary>
        private Listener m_Listener;

        /// <summary>
        /// 初始化事件订阅句柄。
        /// </summary>
        /// <param name="module">事件模块。</param>
        /// <param name="listener">监听器记录。</param>
        /// <exception cref="ArgumentNullException">事件模块或监听器记录为空时抛出。</exception>
        internal Subscription(EventModule module, Listener listener)
        {
            m_Module = module ?? throw new ArgumentNullException(nameof(module));
            m_Listener = listener ?? throw new ArgumentNullException(nameof(listener));
        }

        /// <summary>
        /// 订阅是否仍处于活动状态。
        /// </summary>
        public bool IsActive => m_Listener != null && m_Listener.IsActive;

        /// <summary>
        /// 取消当前事件订阅。
        /// </summary>
        public void Cancel()
        {
            if (m_Module == null || m_Listener == null)
            {
                return;
            }

            m_Module.Unsubscribe(m_Listener);
            m_Module = null;
            m_Listener = null;
        }

        /// <summary>
        /// 释放订阅句柄，并取消当前事件订阅。
        /// </summary>
        public void Release()
        {
            Cancel();
        }
    }
}

using System;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络消息订阅句柄。
    /// </summary>
    public sealed class MessageSubscription : IReference
    {
        private NetworkChannel m_Channel;
        private MessageListener m_Listener;

        /// <summary>
        /// 初始化 Message Subscription。
        /// </summary>
        internal MessageSubscription(NetworkChannel channel, MessageListener listener)
        {
            m_Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            m_Listener = listener ?? throw new ArgumentNullException(nameof(listener));
        }
        public bool IsActive => m_Listener != null && m_Listener.IsActive;

        /// <summary>
        /// 执行 Cancel。
        /// </summary>
        public void Cancel()
        {
            if (m_Channel == null || m_Listener == null)
            {
                return;
            }

            m_Channel.Unsubscribe(m_Listener);
            m_Channel = null;
            m_Listener = null;
        }

        /// <summary>
        /// 执行 Release。
        /// </summary>
        public void Release()
        {
            Cancel();
        }
    }
}

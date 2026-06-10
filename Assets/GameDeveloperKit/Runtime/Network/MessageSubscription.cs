using System;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络消息订阅句柄。
    /// </summary>
    public sealed class MessageSubscription : IReference
    {
        /// <summary>
        /// 存储 Channel。
        /// </summary>
        private NetworkChannel m_Channel;
        /// <summary>
        /// 存储 Listener。
        /// </summary>
        private MessageListener m_Listener;

        /// <summary>
        /// 初始化 Message Subscription。
        /// </summary>
        /// <param name="channel">channel 参数。</param>
        /// <param name="listener">listener 参数。</param>
        internal MessageSubscription(NetworkChannel channel, MessageListener listener)
        {
            m_Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            m_Listener = listener ?? throw new ArgumentNullException(nameof(listener));
        }

        /// <summary>
        /// 记录 Is Active 状态。
        /// </summary>
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

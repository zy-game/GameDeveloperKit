namespace GameDeveloperKit.Network
{
    internal sealed partial class NetworkChannel
    {
        /// <summary>
        /// 把强类型消息处理器适配为基础消息处理器。
        /// </summary>
        /// <typeparam name="TMessage">消息类型。</typeparam>
        private sealed class HandleAdapter<TMessage> : MessageHandle<Message> where TMessage : Message
        {
            /// <summary>
            /// 存储 Handle。
            /// </summary>
            private readonly MessageHandle<TMessage> m_Handle;

            /// <summary>
            /// 初始化 Handle Adapter。
            /// </summary>
            /// <param name="handle">handle 参数。</param>
            public HandleAdapter(MessageHandle<TMessage> handle)
            {
                m_Handle = handle;
            }

            /// <summary>
            /// 处理 member。
            /// </summary>
            /// <param name="channel">channel 参数。</param>
            /// <param name="message">message 参数。</param>
            public override void Handle(IChannel channel, Message message)
            {
                m_Handle.Handle(channel, (TMessage)message);
            }
        }
    }
}

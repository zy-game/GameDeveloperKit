using System;

namespace GameDeveloperKit.Network
{
    internal sealed class MessageListener
    {
        private readonly Action<IChannel, Message> m_Invoker;

        /// <summary>
        /// 初始化 Message Listener。
        /// </summary>
        /// <param name="messageType">message Type 参数。</param>
        /// <param name="handleKey">handle Key 参数。</param>
        public MessageListener(Type messageType, MessageHandle<Message> handle, object handleKey = null)
        {
            MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            HandleKey = handleKey ?? handle;
            m_Invoker = (channel, message) => handle.Handle(channel, message);
            IsActive = true;
        }

        /// <summary>
        /// 执行 callback。
        /// </summary>
        /// <param name="messageType">message Type 参数。</param>
        public MessageListener(Type messageType, Delegate callback, Action<IChannel, Message> invoker)
        {
            MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
            Callback = callback ?? throw new ArgumentNullException(nameof(callback));
            m_Invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
            IsActive = true;
        }

        public Type MessageType { get; }

        public object Handle { get; }

        public object HandleKey { get; }

        public Delegate Callback { get; }

        public bool IsActive { get; private set; }

        /// <summary>
        /// 执行 Deactivate。
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>
        /// 执行 Matches Handle。
        /// </summary>
        public bool MatchesHandle(object handle)
        {
            return HandleKey != null && ReferenceEquals(HandleKey, handle);
        }

        /// <summary>
        /// 执行 callback。
        /// </summary>
        public bool MatchesCallback(Delegate callback)
        {
            return Callback != null && Equals(Callback, callback);
        }

        /// <summary>
        /// 执行 Invoke。
        /// </summary>
        public void Invoke(IChannel channel, Message message)
        {
            m_Invoker(channel, message);
        }
    }
}

using System;

namespace GameDeveloperKit.Network
{
    internal sealed class MessageListener
    {
        private readonly Action<IChannel, Message> m_Invoker;

        public MessageListener(Type messageType, MessageHandle<Message> handle, object handleKey = null)
        {
            MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            HandleKey = handleKey ?? handle;
            m_Invoker = (channel, message) => handle.Handle(channel, message);
            IsActive = true;
        }

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

        public void Deactivate()
        {
            IsActive = false;
        }

        public bool MatchesHandle(object handle)
        {
            return HandleKey != null && ReferenceEquals(HandleKey, handle);
        }

        public bool MatchesCallback(Delegate callback)
        {
            return Callback != null && Equals(Callback, callback);
        }

        public void Invoke(IChannel channel, Message message)
        {
            m_Invoker(channel, message);
        }
    }
}

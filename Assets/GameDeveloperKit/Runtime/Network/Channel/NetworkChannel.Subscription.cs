using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Network
{
    internal sealed partial class NetworkChannel
    {
        /// <summary>
        /// 注册 member。
        /// </summary>
        /// <typeparam name="TMessage">泛型类型参数。</typeparam>
        public MessageSubscription Register<TMessage>(MessageHandle<TMessage> handle) where TMessage : Message
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            var messageType = typeof(TMessage);
            var listeners = GetListeners(messageType);
            foreach (var listener in listeners)
            {
                if (listener.IsActive && listener.MatchesHandle(handle))
                {
                    return new MessageSubscription(this, listener);
                }
            }

            var newListener = new MessageListener(
                messageType,
                new HandleAdapter<TMessage>(handle),
                handle);
            listeners.Add(newListener);
            return new MessageSubscription(this, newListener);
        }

        /// <summary>
        /// 执行 Subscribe。
        /// </summary>
        /// <typeparam name="TMessage">泛型类型参数。</typeparam>
        public MessageSubscription Subscribe<TMessage>(Action<TMessage> callback) where TMessage : Message
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var messageType = typeof(TMessage);
            var listeners = GetListeners(messageType);
            foreach (var listener in listeners)
            {
                if (listener.IsActive && listener.MatchesCallback(callback))
                {
                    return new MessageSubscription(this, listener);
                }
            }

            var newListener = new MessageListener(
                messageType,
                callback,
                (_, message) => callback((TMessage)message));
            listeners.Add(newListener);
            return new MessageSubscription(this, newListener);
        }

        /// <summary>
        /// 执行 Subscribe。
        /// </summary>
        public MessageSubscription Subscribe(Action<Message> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            foreach (var listener in m_GlobalListeners)
            {
                if (listener.IsActive && listener.MatchesCallback(callback))
                {
                    return new MessageSubscription(this, listener);
                }
            }

            var newListener = new MessageListener(
                typeof(Message),
                callback,
                (_, message) => callback(message));
            m_GlobalListeners.Add(newListener);
            return new MessageSubscription(this, newListener);
        }

        /// <summary>
        /// 执行 Unsubscribe。
        /// </summary>
        internal void Unsubscribe(MessageListener listener)
        {
            if (listener == null)
            {
                return;
            }

            listener.Deactivate();
            if (listener.MessageType == typeof(Message))
            {
                m_GlobalListeners.Remove(listener);
                return;
            }

            if (!m_Listeners.TryGetValue(listener.MessageType, out var listeners))
            {
                return;
            }

            listeners.Remove(listener);
            if (listeners.Count == 0)
            {
                m_Listeners.Remove(listener.MessageType);
            }
        }

        /// <summary>
        /// 获取 Listeners。
        /// </summary>
        /// <param name="messageType">message Type 参数。</param>
        private List<MessageListener> GetListeners(Type messageType)
        {
            if (!m_Listeners.TryGetValue(messageType, out var listeners))
            {
                listeners = new List<MessageListener>();
                m_Listeners.Add(messageType, listeners);
            }

            return listeners;
        }

        /// <summary>
        /// 执行 Dispatch。
        /// </summary>
        private void Dispatch(Message message)
        {
            if (m_Listeners.TryGetValue(message.GetType(), out var listeners))
            {
                DispatchListeners(listeners, message);
            }

            DispatchListeners(m_GlobalListeners, message);
        }

        /// <summary>
        /// 执行 Dispatch Listeners。
        /// </summary>
        private void DispatchListeners(List<MessageListener> listeners, Message message)
        {
            if (listeners.Count == 0)
            {
                return;
            }

            m_DispatchCache.Clear();
            m_DispatchCache.AddRange(listeners);
            foreach (var listener in m_DispatchCache)
            {
                if (!listener.IsActive)
                {
                    continue;
                }

                try
                {
                    listener.Invoke(this, message);
                }
                catch (Exception exception)
                {
                    LastException = exception;
                }
            }

            m_DispatchCache.Clear();
        }

        /// <summary>
        /// 清理 Subscriptions。
        /// </summary>
        private void ClearSubscriptions()
        {
            foreach (var pair in m_Listeners)
            {
                foreach (var listener in pair.Value)
                {
                    listener.Deactivate();
                }
            }

            foreach (var listener in m_GlobalListeners)
            {
                listener.Deactivate();
            }

            m_Listeners.Clear();
            m_GlobalListeners.Clear();
            m_DispatchCache.Clear();
        }
    }
}

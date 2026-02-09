using System;
using System.Collections.Generic;
using GameDeveloperKit.Events;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 消息分发器
    /// </summary>
    public class MessageDispatcher
    {
        private readonly Dictionary<int, List<Action<INetworkMessage>>> _handlers = new();

        /// <summary>
        /// 订阅消息
        /// </summary>
        public IDisposable Subscribe<T>(Action<T> handler) where T : INetworkMessage
        {
            var messageId = GetMessageId<T>();
            
            if (!_handlers.TryGetValue(messageId, out var list))
            {
                list = new List<Action<INetworkMessage>>();
                _handlers[messageId] = list;
            }

            Action<INetworkMessage> wrapper = msg => handler((T)msg);
            list.Add(wrapper);

            return new EventSubscription(() =>
            {
                if (_handlers.TryGetValue(messageId, out var handlers))
                    handlers.Remove(wrapper);
            });
        }

        /// <summary>
        /// 分发消息
        /// </summary>
        public void Dispatch(INetworkMessage message)
        {
            if (!_handlers.TryGetValue(message.MessageId, out var handlers))
                return;

            var copy = new List<Action<INetworkMessage>>(handlers);
            foreach (var handler in copy)
            {
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    Game.Debug.Error($"Message handler error: {ex}");
                }
            }
        }

        /// <summary>
        /// 清理所有订阅
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
        }

        private static int GetMessageId<T>() where T : INetworkMessage
        {
            return typeof(T).GetHashCode();
        }
    }
}

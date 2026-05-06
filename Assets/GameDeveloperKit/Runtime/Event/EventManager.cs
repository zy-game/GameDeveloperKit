using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit
{
    public class EventManager : IGameFrameworkModule
    {
        private readonly Dictionary<string, HandlerList> m_Handlers = new Dictionary<string, HandlerList>();

        public int HandlerCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in m_Handlers)
                {
                    count += kvp.Value.Count;
                }
                return count;
            }
        }

        public void Subscribe<T>(string eventKey, Action<object, T> handler) where T : EventArgs
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!m_Handlers.TryGetValue(eventKey, out var handlerList))
            {
                handlerList = new HandlerList();
                m_Handlers[eventKey] = handlerList;
            }

            handlerList.Add(handler);
        }

        public void Unsubscribe<T>(string eventKey, Action<object, T> handler) where T : EventArgs
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (m_Handlers.TryGetValue(eventKey, out var handlerList))
            {
                handlerList.Remove(handler);
                if (handlerList.Count == 0)
                {
                    m_Handlers.Remove(eventKey);
                }
            }
        }

        public void Fire<T>(string eventKey, object sender, T args) where T : EventArgs
        {
            if (m_Handlers.TryGetValue(eventKey, out var handlerList))
            {
                handlerList.Invoke(sender, args);
            }
        }

        public void Fire(string eventKey, object sender)
        {
            Fire(eventKey, sender, EventArgs.Empty);
        }

        public void Fire(string eventKey)
        {
            Fire(eventKey, null, EventArgs.Empty);
        }

        public bool HasSubscribers(string eventKey)
        {
            return m_Handlers.ContainsKey(eventKey) && m_Handlers[eventKey].Count > 0;
        }

        public void ClearEvent(string eventKey)
        {
            m_Handlers.Remove(eventKey);
        }

        public UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Shutdown()
        {
            m_Handlers.Clear();
            return UniTask.CompletedTask;
        }

        public void Release()
        {
            m_Handlers.Clear();
        }

        private sealed class HandlerList
        {
            private readonly List<Delegate> m_Delegates = new List<Delegate>();

            public int Count => m_Delegates.Count;

            public void Add(Delegate handler)
            {
                m_Delegates.Add(handler);
            }

            public void Remove(Delegate handler)
            {
                m_Delegates.Remove(handler);
            }

            public void Invoke<T>(object sender, T args) where T : EventArgs
            {
                for (int i = m_Delegates.Count - 1; i >= 0; i--)
                {
                    (m_Delegates[i] as Action<object, T>)?.Invoke(sender, args);
                }
            }
        }
    }
}

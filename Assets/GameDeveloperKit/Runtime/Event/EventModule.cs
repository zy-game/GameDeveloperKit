using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Event
{
    public class EventModule : GameModuleBase
    {
        private readonly Dictionary<Type, List<Listener>> m_Listeners = new Dictionary<Type, List<Listener>>();
        private readonly List<Listener> m_DispatchCache = new List<Listener>();

        public override UniTask Startup()
        {
            BindingGenerated.RegisterAll(this);
            return UniTask.CompletedTask;
        }

        public override UniTask Shutdown()
        {
            Clear();
            return UniTask.CompletedTask;
        }

        public Subscription Subscribe<THandle>(THandle handle) where THandle : IEventHandle
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            var eventType = GetEventType(handle.GetType());
            if (!m_Listeners.TryGetValue(eventType, out var listeners))
            {
                listeners = new List<Listener>();
                m_Listeners.Add(eventType, listeners);
            }

            foreach (var listener in listeners)
            {
                if (listener.IsActive && ReferenceEquals(listener.Handle, handle))
                {
                    return new Subscription(this, listener);
                }
            }

            var newListener = new Listener(eventType, handle);
            listeners.Add(newListener);
            return new Subscription(this, newListener);
        }

        public Subscription Subscribe<TEvent>(Action<TEvent> handle) where TEvent : IEventArgs
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            var eventType = typeof(TEvent);
            if (!m_Listeners.TryGetValue(eventType, out var listeners))
            {
                listeners = new List<Listener>();
                m_Listeners.Add(eventType, listeners);
            }

            foreach (var listener in listeners)
            {
                if (listener.IsActive && Equals(listener.Action, handle))
                {
                    return new Subscription(this, listener);
                }
            }

            var newListener = new Listener(eventType, handle);
            listeners.Add(newListener);
            return new Subscription(this, newListener);
        }

        public void Unsubscribe<THandle>(THandle handle) where THandle : IEventHandle
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            var eventType = GetEventType(handle.GetType());
            if (!m_Listeners.TryGetValue(eventType, out var listeners))
            {
                return;
            }

            for (var i = listeners.Count - 1; i >= 0; i--)
            {
                var listener = listeners[i];
                if (ReferenceEquals(listener.Handle, handle))
                {
                    listener.Deactivate();
                    listeners.RemoveAt(i);
                }
            }

            if (listeners.Count == 0)
            {
                m_Listeners.Remove(eventType);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handle) where TEvent : IEventArgs
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            var eventType = typeof(TEvent);
            if (!m_Listeners.TryGetValue(eventType, out var listeners))
            {
                return;
            }

            for (var i = listeners.Count - 1; i >= 0; i--)
            {
                var listener = listeners[i];
                if (Equals(listener.Action, handle))
                {
                    listener.Deactivate();
                    listeners.RemoveAt(i);
                }
            }

            if (listeners.Count == 0)
            {
                m_Listeners.Remove(eventType);
            }
        }

        public void Fire<TEvent>(TEvent eventData, object sender = null) where TEvent : IEventArgs
        {
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            var eventType = typeof(TEvent);
            if (!m_Listeners.TryGetValue(eventType, out var listeners) || listeners.Count == 0)
            {
                return;
            }

            m_DispatchCache.Clear();
            m_DispatchCache.AddRange(listeners);
            foreach (var listener in m_DispatchCache)
            {
                if (eventData.HasUse())
                {
                    break;
                }

                if (!listener.IsActive)
                {
                    continue;
                }

                if (listener.Handle != null)
                {
                    listener.Handle.Handle(sender, eventData);
                }
                else
                {
                    ((Action<TEvent>)listener.Action)(eventData);
                }
            }

            m_DispatchCache.Clear();
        }

        public void Clear()
        {
            foreach (var pair in m_Listeners)
            {
                foreach (var listener in pair.Value)
                {
                    listener.Deactivate();
                }
            }

            m_Listeners.Clear();
            m_DispatchCache.Clear();
        }

        internal void Unsubscribe(Listener listener)
        {
            if (listener == null || !m_Listeners.TryGetValue(listener.EventType, out var listeners))
            {
                return;
            }

            listener.Deactivate();
            listeners.Remove(listener);
            if (listeners.Count == 0)
            {
                m_Listeners.Remove(listener.EventType);
            }
        }

        private static Type GetEventType(Type handleType)
        {
            foreach (var interfaceType in handleType.GetInterfaces())
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEventHandle<>))
                {
                    return interfaceType.GetGenericArguments()[0];
                }
            }

            throw new GameException($"Event handle '{handleType.FullName}' must implement IEventHandle<TEvent>.");
        }
    }
}
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Timer;

namespace GameDeveloperKit.Event
{
    /// <summary>
    /// 事件模块，负责事件监听器注册、注销和事件派发。
    /// </summary>
    public class EventModule : GameModuleBase
    {
        /// <summary>
        /// 存储 Listeners。
        /// </summary>
        private readonly Dictionary<Type, List<Listener>> m_Listeners = new Dictionary<Type, List<Listener>>();
        /// <summary>
        /// 存储 Queued Events。
        /// </summary>
        private readonly Queue<QueuedEvent> m_QueuedEvents = new Queue<QueuedEvent>();
        /// <summary>
        /// 存储 Dispatch Cache。
        /// </summary>
        private readonly List<Listener> m_DispatchCache = new List<Listener>();
        /// <summary>
        /// 存储 Dispatch Handle。
        /// </summary>
        private UpdateTimerHandle m_DispatchHandle;

        /// <summary>
        /// 启动事件模块，并注册生成的事件绑定。
        /// </summary>
        /// <returns>模块启动任务。</returns>
        public override UniTask Startup()
        {
            BindingGenerated.RegisterAll(this);
            EnsureTimerUpdate();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 关闭事件模块，并清理所有事件监听器。
        /// </summary>
        /// <returns>模块关闭任务。</returns>
        public override UniTask Shutdown()
        {
            m_DispatchHandle?.Cancel();
            m_DispatchHandle = null;
            Clear();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 订阅对象形式的事件处理器。
        /// </summary>
        /// <typeparam name="THandle">事件处理器类型。</typeparam>
        /// <param name="handle">事件处理器实例。</param>
        /// <returns>事件订阅句柄。</returns>
        /// <exception cref="ArgumentNullException">事件处理器为空时抛出。</exception>
        public Subscription Subscribe<THandle>(THandle handle) where THandle : EventHandleBase
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
                if (listener.IsActive && ReferenceEquals(listener.handleBase, handle))
                {
                    return new Subscription(this, listener);
                }
            }

            var newListener = new Listener(eventType, handle);
            listeners.Add(newListener);
            return new Subscription(this, newListener);
        }

        /// <summary>
        /// 订阅委托形式的事件处理器。
        /// </summary>
        /// <typeparam name="TEvent">事件参数类型。</typeparam>
        /// <param name="handle">事件处理委托。</param>
        /// <returns>事件订阅句柄。</returns>
        /// <exception cref="ArgumentNullException">事件处理委托为空时抛出。</exception>
        public Subscription Subscribe<TEvent>(Action<TEvent> handle) where TEvent : ArgsBase
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

            var newListener = new Listener(eventType, handle, eventData => handle((TEvent)eventData));
            listeners.Add(newListener);
            return new Subscription(this, newListener);
        }

        /// <summary>
        /// 取消对象形式的事件处理器订阅。
        /// </summary>
        /// <typeparam name="THandle">事件处理器类型。</typeparam>
        /// <param name="handle">事件处理器实例。</param>
        /// <exception cref="ArgumentNullException">事件处理器为空时抛出。</exception>
        public void Unsubscribe<THandle>(THandle handle) where THandle : EventHandleBase
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
                if (ReferenceEquals(listener.handleBase, handle))
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

        /// <summary>
        /// 取消委托形式的事件处理器订阅。
        /// </summary>
        /// <typeparam name="TEvent">事件参数类型。</typeparam>
        /// <param name="handle">事件处理委托。</param>
        /// <exception cref="ArgumentNullException">事件处理委托为空时抛出。</exception>
        public void Unsubscribe<TEvent>(Action<TEvent> handle) where TEvent : ArgsBase
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

        /// <summary>
        /// 将事件加入派发队列，并在 TimerModule 的下一次 Update 中派发。
        /// </summary>
        /// <typeparam name="TEvent">事件参数类型。</typeparam>
        /// <param name="eventData">事件参数。</param>
        /// <param name="sender">事件发送者。</param>
        /// <exception cref="ArgumentNullException">事件参数为空时抛出。</exception>
        public void Fire<TEvent>(TEvent eventData, object sender = null) where TEvent : ArgsBase
        {
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            if (!EnsureTimerUpdate())
            {
                throw new GameException("TimerModule is required to dispatch queued events. Register TimerModule before Fire or use FireNow for immediate dispatch.");
            }

            m_QueuedEvents.Enqueue(new QueuedEvent(typeof(TEvent), eventData, sender));
        }

        /// <summary>
        /// 不安全地立即派发事件到当前事件类型的所有活动监听器。
        /// </summary>
        /// <typeparam name="TEvent">事件参数类型。</typeparam>
        /// <param name="eventData">事件参数。</param>
        /// <param name="sender">事件发送者。</param>
        /// <exception cref="ArgumentNullException">事件参数为空时抛出。</exception>
        public void FireNow<TEvent>(TEvent eventData, object sender = null) where TEvent : ArgsBase
        {
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            Dispatch(typeof(TEvent), eventData, sender);
        }

        /// <summary>
        /// 执行 Dispatch。
        /// </summary>
        /// <param name="eventType">event Type 参数。</param>
        /// <param name="eventData">event Data 参数。</param>
        /// <param name="sender">sender 参数。</param>
        private void Dispatch(Type eventType, ArgsBase eventData, object sender)
        {
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

                listener.Invoke(sender, eventData);
            }

            m_DispatchCache.Clear();
        }

        /// <summary>
        /// 执行 Dispatch Queued Events。
        /// </summary>
        private void DispatchQueuedEvents()
        {
            var count = m_QueuedEvents.Count;
            for (var i = 0; i < count; i++)
            {
                if (m_QueuedEvents.Count == 0)
                {
                    break;
                }

                var queuedEvent = m_QueuedEvents.Dequeue();
                Dispatch(queuedEvent.EventType, queuedEvent.EventData, queuedEvent.Sender);
            }
        }

        /// <summary>
        /// 确保 Timer Update。
        /// </summary>
        /// <returns>条件满足时返回 true。</returns>
        private bool EnsureTimerUpdate()
        {
            if (m_DispatchHandle != null &&
                !m_DispatchHandle.IsCancelled &&
                !m_DispatchHandle.IsCompleted &&
                m_DispatchHandle.Module != null)
            {
                return true;
            }

            if (!App.TryGetRegistered<TimerModule>(out var timer))
            {
                return false;
            }

            m_DispatchHandle = timer.OnUpdate(DispatchQueuedEvents, this, "EventModule.Dispatch");
            return true;
        }

        /// <summary>
        /// 清理所有事件监听器和派发缓存。
        /// </summary>
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
            m_QueuedEvents.Clear();
            m_DispatchCache.Clear();
        }

        /// <summary>
        /// 根据订阅记录取消监听器订阅。
        /// </summary>
        /// <param name="listener">监听器记录。</param>
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

        /// <summary>
        /// 获取 Event Type。
        /// </summary>
        /// <param name="handleType">handle Type 参数。</param>
        /// <returns>执行结果。</returns>
        private static Type GetEventType(Type handleType)
        {
            foreach (var interfaceType in handleType.GetInterfaces())
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEventHandleBase<>))
                {
                    return interfaceType.GetGenericArguments()[0];
                }
            }

            throw new GameException($"Event handle '{handleType.FullName}' must implement IEventHandleBase<TEvent>.");
        }

        /// <summary>
        /// 定义 Queued Event 结构。
        /// </summary>
        private readonly struct QueuedEvent
        {
            /// <summary>
            /// 初始化 Queued Event。
            /// </summary>
            /// <param name="eventType">event Type 参数。</param>
            /// <param name="eventData">event Data 参数。</param>
            /// <param name="sender">sender 参数。</param>
            public QueuedEvent(Type eventType, ArgsBase eventData, object sender)
            {
                EventType = eventType;
                EventData = eventData;
                Sender = sender;
            }

            public Type EventType { get; }

            public ArgsBase EventData { get; }

            public object Sender { get; }
        }
    }
}

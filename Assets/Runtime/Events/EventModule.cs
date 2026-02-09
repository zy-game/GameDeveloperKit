using System;
using System.Collections.Generic;
using GameDeveloperKit.Log;

namespace GameDeveloperKit.Events
{
    /// <summary>
    /// 事件处理器信息
    /// </summary>
    internal class EventHandlerInfo
    {
        public Action<GameEventArgs> Wrapper;
        public Delegate OriginalHandler;
        public Type EventType;
    }

    /// <summary>
    /// 事件模块
    /// </summary>
    public sealed class EventModule : IModule, IEventManager
    {
        private readonly Dictionary<int, List<EventHandlerInfo>> _handlers = new Dictionary<int, List<EventHandlerInfo>>();
        private readonly Queue<GameEventArgs> _events = new Queue<GameEventArgs>();

        public void OnStartup()
        {
            // 注册事件调试面板
            DebugConsole.Instance?.RegisterPanel(new EventDebugPanel());
        }

        public void OnUpdate(float elapseSeconds)
        {
            while (_events.Count > 0)
            {
                var e = _events.Dequeue();
                FireNow(e.Sender, e);
            }
        }

        public void OnClearup()
        {
            _handlers.Clear();
            _events.Clear();
        }

        /// <summary>
        /// 订阅事件（按类型自动推断ID）
        /// </summary>
        public IDisposable Subscribe<T>(Action<T> handler) where T : GameEventArgs
        {
            return Subscribe(typeof(T).GetHashCode(), handler);
        }

        /// <summary>
        /// 订阅事件（指定ID）
        /// </summary>
        public IDisposable Subscribe<T>(int id, Action<T> handler) where T : GameEventArgs
        {
            if (handler == null) return null;

            if (!_handlers.TryGetValue(id, out var list))
            {
                list = new List<EventHandlerInfo>();
                _handlers[id] = list;
            }

            var info = new EventHandlerInfo
            {
                Wrapper = e => handler((T)e),
                OriginalHandler = handler,
                EventType = typeof(T)
            };
            list.Add(info);

            return new EventSubscription(() =>
            {
                if (_handlers.TryGetValue(id, out var handlers))
                    handlers.Remove(info);
            });
        }

        /// <summary>
        /// 触发事件（下一帧）
        /// </summary>
        public void Fire(object sender, GameEventArgs e)
        {
            e.Sender = sender;
            _events.Enqueue(e);
        }

        /// <summary>
        /// 立即触发事件
        /// </summary>
        public void FireNow(object sender, GameEventArgs e)
        {
            if (!_handlers.TryGetValue(e.Id, out var handlers))
            {
                ReferencePool.Release(e);
                return;
            }

            var copy = new List<EventHandlerInfo>(handlers);
            foreach (var info in copy)
            {
                try
                {
                    info.Wrapper(e);
                }
                catch (Exception ex)
                {
                    Game.Debug.Error($"Event handler error: {ex}");
                }
            }

            ReferencePool.Release(e);
        }
    }
}
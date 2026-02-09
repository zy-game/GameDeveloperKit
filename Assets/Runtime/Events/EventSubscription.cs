using System;

namespace GameDeveloperKit.Events
{
    /// <summary>
    /// 事件订阅句柄，实现IDisposable用于取消订阅
    /// </summary>
    public sealed class EventSubscription : IDisposable
    {
        private Action _unsubscribe;
        private bool _disposed;

        internal EventSubscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public bool IsDisposed => _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}

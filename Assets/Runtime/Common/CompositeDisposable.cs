using System;
using System.Collections.Generic;

namespace GameDeveloperKit
{
    /// <summary>
    /// 组合式Disposable，用于批量管理多个订阅
    /// </summary>
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private bool _disposed;

        public int Count => _disposables.Count;
        public bool IsDisposed => _disposed;

        public void Add(IDisposable disposable)
        {
            if (disposable == null) return;
            
            if (_disposed)
            {
                disposable.Dispose();
                return;
            }
            
            _disposables.Add(disposable);
        }

        public bool Remove(IDisposable disposable)
        {
            if (_disposed) return false;
            return _disposables.Remove(disposable);
        }

        public void Clear()
        {
            if (_disposed) return;
            
            foreach (var d in _disposables)
                d?.Dispose();
            
            _disposables.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            foreach (var d in _disposables)
                d?.Dispose();
            
            _disposables.Clear();
        }
    }
}

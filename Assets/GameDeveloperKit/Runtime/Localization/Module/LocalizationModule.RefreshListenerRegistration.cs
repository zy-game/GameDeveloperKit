using System;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class LocalizationModule
    {
        /// <summary>
        /// 刷新监听器注册，用于管理本地化刷新监听器的生命周期
        /// </summary>
        private sealed class RefreshListenerRegistration : IDisposable
        {
            private readonly LocalizationModule _owner;
            private readonly Action<string> _listener;
            private bool _disposed;

            /// <summary>
            /// 初始化刷新监听器注册
            /// </summary>
            /// <param name="owner">拥有者模块</param>
            /// <param name="listener">监听器回调</param>
            public RefreshListenerRegistration(LocalizationModule owner, Action<string> listener)
            {
                _owner = owner;
                _listener = listener;
            }

            /// <summary>
            /// 释放刷新监听器注册
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _owner.UnregisterRefreshListener(_listener);
                _disposed = true;
            }
        }
    }
}

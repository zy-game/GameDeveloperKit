using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 提供网络服务的基础生命周期实现。
    /// </summary>
    public abstract class NetworkService : INetworkService
    {
        /// <summary>
        /// 获取当前所属的网络模块。
        /// </summary>
        public NetworkModule Module { get; private set; }

        /// <summary>
        /// 初始化网络服务。
        /// </summary>
        /// <param name="module">所属的网络模块。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="module"/> 为 null 时抛出。</exception>
        public void Initialize(NetworkModule module)
        {
            Module = module ?? throw new ArgumentNullException(nameof(module));
            OnInitialize();
        }

        /// <summary>
        /// 释放网络服务。
        /// </summary>
        public void Dispose()
        {
            OnDispose();
            Module = null;
        }

        /// <summary>
        /// 当服务初始化时调用。
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// 当服务释放时调用。
        /// </summary>
        protected virtual void OnDispose()
        {
        }
    }
}

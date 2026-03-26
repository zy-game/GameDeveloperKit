using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 网络服务接口，提供网络请求的基础功能。
    /// </summary>
    public interface INetworkService : IDisposable
    {
        /// <summary>
        /// 获取网络模块实例。
        /// </summary>
        NetworkModule Module { get; }

        /// <summary>
        /// 初始化网络服务。
        /// </summary>
        /// <param name="module">网络模块实例。</param>
        void Initialize(NetworkModule module);
    }
}

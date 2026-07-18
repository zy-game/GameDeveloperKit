using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络传输适配器。
    /// </summary>
    public interface INetworkTransport : IReference
    {
        /// <summary>
        /// 在任意线程报告收到的 payload；payload 所有权仅在回调期间有效。
        /// </summary>
        event Action<byte[]> Received;

        UniTask ConnectAsync(NetworkEndpoint endpoint);

        UniTask SendAsync(byte[] data);

        UniTask CloseAsync();
    }
}

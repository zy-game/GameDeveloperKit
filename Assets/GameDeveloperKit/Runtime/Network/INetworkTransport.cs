using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络传输适配器。
    /// </summary>
    public interface INetworkTransport : IReference
    {
        event Action<byte[]> Received;

        UniTask ConnectAsync(NetworkEndpoint endpoint);

        UniTask SendAsync(byte[] data);

        UniTask CloseAsync();
    }
}

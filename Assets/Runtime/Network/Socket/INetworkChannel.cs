using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络通道接口
    /// </summary>
    public interface INetworkChannel : IDisposable
    {
        NetworkProtocol Protocol { get; }
        bool IsConnected { get; }
        
        event Action OnConnected;
        event Action OnDisconnected;
        event Action<byte[]> OnDataReceived;

        UniTask ConnectAsync(string host, CancellationToken ct = default);
        void Disconnect();
        UniTask SendAsync(byte[] data);
    }
}

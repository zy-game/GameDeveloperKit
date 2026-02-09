using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络终端状态
    /// </summary>
    public enum NetworkState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    /// <summary>
    /// 网络协议类型
    /// </summary>
    public enum NetworkProtocol
    {
        TCP,
        UDP,
        WebSocket
    }

    /// <summary>
    /// 网络终端接口
    /// </summary>
    public interface INetworkTerminal : IDisposable
    {
        string Name { get; }
        NetworkState State { get; }
        NetworkProtocol Protocol { get; }
        bool IsConnected { get; }

        UniTask ConnectAsync(CancellationToken ct = default);
        void Disconnect();
        void Send<T>(T message) where T : INetworkMessage;
        UniTask<TResponse> SendAsync<TResponse>(INetworkMessage request, CancellationToken ct = default) where TResponse : INetworkMessage;
        IDisposable Subscribe<T>(Action<T> handler) where T : INetworkMessage;
        IDisposable OnStateChanged(Action<NetworkState> handler);
    }
}

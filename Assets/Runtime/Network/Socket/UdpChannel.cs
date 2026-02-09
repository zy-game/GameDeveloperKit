using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// UDP通道实现
    /// </summary>
    public class UdpChannel : INetworkChannel
    {
        private UdpSession _client;
        
        public NetworkProtocol Protocol => NetworkProtocol.UDP;
        public bool IsConnected => _client != null;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<byte[]> OnDataReceived;

        public async UniTask ConnectAsync(string host, CancellationToken ct = default)
        {
            _client = new UdpSession();
            _client.Received = (c, e) => { OnDataReceived?.Invoke(e.Memory.ToArray()); return Task.CompletedTask; };

            await _client.SetupAsync(new TouchSocketConfig().SetRemoteIPHost(host));
            await _client.StartAsync();
            OnConnected?.Invoke();
        }

        public void Disconnect()
        {
            _client?.Stop();
            OnDisconnected?.Invoke();
        }

        public async UniTask SendAsync(byte[] data)
        {
            if (_client != null)
                await _client.SendAsync(data);
        }

        public void Dispose()
        {
            _client?.Dispose();
            _client = null;
        }
    }
}

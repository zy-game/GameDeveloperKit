using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// TCP通道实现
    /// </summary>
    public class TcpChannel : INetworkChannel
    {
        private TcpClient _client;
        
        public NetworkProtocol Protocol => NetworkProtocol.TCP;
        public bool IsConnected => _client?.Online ?? false;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<byte[]> OnDataReceived;

        public async UniTask ConnectAsync(string host, CancellationToken ct = default)
        {
            _client = new TcpClient();
            _client.Received = (c, e) => { OnDataReceived?.Invoke(e.Memory.ToArray()); return Task.CompletedTask; };
            _client.Connected = (c, e) => { OnConnected?.Invoke(); return Task.CompletedTask; };
            _client.Closed = (c, e) => { OnDisconnected?.Invoke(); return Task.CompletedTask; };

            await _client.SetupAsync(new TouchSocketConfig().SetRemoteIPHost(host));
            await _client.ConnectAsync(ct);
        }

        public void Disconnect()
        {
            _client?.Close();
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

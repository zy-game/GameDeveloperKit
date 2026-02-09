using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using WebSocketSharp;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// WebSocket通道实现
    /// </summary>
    public class WebSocketChannel : INetworkChannel
    {
        private WebSocketSharp.WebSocket _client;
        
        public NetworkProtocol Protocol => NetworkProtocol.WebSocket;
        public bool IsConnected => _client?.ReadyState == WebSocketState.Open;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<byte[]> OnDataReceived;

        public UniTask ConnectAsync(string host, CancellationToken ct = default)
        {
            var tcs = new UniTaskCompletionSource();
            
            _client = new WebSocketSharp.WebSocket(host);
            _client.OnOpen += (s, e) => { OnConnected?.Invoke(); tcs.TrySetResult(); };
            _client.OnClose += (s, e) => { OnDisconnected?.Invoke(); };
            _client.OnError += (s, e) => { tcs.TrySetException(new Exception(e.Message)); };
            _client.OnMessage += (s, e) => { OnDataReceived?.Invoke(e.RawData); };
            
            ct.Register(() => tcs.TrySetCanceled());
            _client.ConnectAsync();
            
            return tcs.Task;
        }

        public void Disconnect()
        {
            _client?.Close();
        }

        public UniTask SendAsync(byte[] data)
        {
            _client?.Send(data);
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _client?.Close();
            _client = null;
        }
    }
}

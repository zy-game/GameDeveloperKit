using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Events;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络终端实现
    /// </summary>
    public class NetworkTerminal : INetworkTerminal
    {
        private readonly string _name;
        private readonly string _host;
        private readonly MessageDispatcher _dispatcher;
        private readonly ConcurrentDictionary<int, PendingRequest> _pendingRequests;
        private readonly List<Action<NetworkState>> _stateHandlers;
        private readonly IMessageSerializer _serializer;
        private readonly INetworkChannel _channel;

        private NetworkState _state = NetworkState.Disconnected;

        public string Name => _name;
        public NetworkState State => _state;
        public NetworkProtocol Protocol => _channel.Protocol;
        public bool IsConnected => _state == NetworkState.Connected;

        public NetworkTerminal(string name, string host, NetworkProtocol protocol, IMessageSerializer serializer = null)
        {
            _name = name;
            _host = host;
            _serializer = serializer ?? new JsonMessageSerializer();
            _dispatcher = new MessageDispatcher();
            _pendingRequests = new ConcurrentDictionary<int, PendingRequest>();
            _stateHandlers = new List<Action<NetworkState>>();
            
            _channel = CreateChannel(protocol);
            _channel.OnConnected += () => SetState(NetworkState.Connected);
            _channel.OnDisconnected += () => SetState(NetworkState.Disconnected);
            _channel.OnDataReceived += OnDataReceived;
        }

        public async UniTask ConnectAsync(CancellationToken ct = default)
        {
            if (_state == NetworkState.Connected) return;
            SetState(NetworkState.Connecting);

            try
            {
                await _channel.ConnectAsync(_host, ct);
            }
            catch (Exception ex)
            {
                SetState(NetworkState.Disconnected);
                Game.Debug.Error($"Connect failed: {ex.Message}");
                throw;
            }
        }

        public void Disconnect()
        {
            if (_state == NetworkState.Disconnected) return;
            _channel.Disconnect();
        }

        public void Send<T>(T message) where T : INetworkMessage
        {
            if (!IsConnected) { Game.Debug.Warning("Not connected"); return; }
            var data = _serializer.Serialize(message);
            _channel.SendAsync(data).Forget();
        }

        public async UniTask<TResponse> SendAsync<TResponse>(INetworkMessage request, CancellationToken ct = default) 
            where TResponse : INetworkMessage
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected");

            var responseId = typeof(TResponse).GetHashCode();
            var pending = new PendingRequest();
            _pendingRequests[responseId] = pending;

            try
            {
                Send(request);
                using (ct.Register(() => pending.TrySetCanceled()))
                    return (TResponse)await pending.Task;
            }
            finally { _pendingRequests.TryRemove(responseId, out _); }
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : INetworkMessage => _dispatcher.Subscribe(handler);
        
        public IDisposable OnStateChanged(Action<NetworkState> handler)
        {
            _stateHandlers.Add(handler);
            return new EventSubscription(() => _stateHandlers.Remove(handler));
        }

        public void Dispose()
        {
            _channel.Dispose();
            _dispatcher.Clear();
            _stateHandlers.Clear();
        }

        private static INetworkChannel CreateChannel(NetworkProtocol protocol) => protocol switch
        {
            NetworkProtocol.TCP => new TcpChannel(),
            NetworkProtocol.UDP => new UdpChannel(),
            NetworkProtocol.WebSocket => new WebSocketChannel(),
            _ => throw new NotSupportedException($"Protocol {protocol} not supported")
        };

        private void OnDataReceived(byte[] data)
        {
            try
            {
                var message = _serializer.Deserialize(data);
                if (message == null) return;
                if (_pendingRequests.TryGetValue(message.MessageId, out var pending))
                    pending.TrySetResult(message);
                _dispatcher.Dispatch(message);
            }
            catch (Exception ex) { Game.Debug.Error($"Receive error: {ex}"); }
        }

        private void SetState(NetworkState state)
        {
            if (_state == state) return;
            _state = state;
            foreach (var h in _stateHandlers) try { h(state); } catch { }
        }

        private class PendingRequest
        {
            private readonly UniTaskCompletionSource<INetworkMessage> _tcs = new();
            public UniTask<INetworkMessage> Task => _tcs.Task;
            public void TrySetResult(INetworkMessage msg) => _tcs.TrySetResult(msg);
            public void TrySetCanceled() => _tcs.TrySetCanceled();
        }
    }
}

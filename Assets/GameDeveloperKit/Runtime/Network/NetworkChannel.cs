using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    internal sealed class NetworkChannel : IChannel
    {
        private readonly INetworkCodec m_Codec;
        private readonly INetworkTransport m_Transport;
        private readonly Dictionary<long, PendingResponse> m_PendingResponses = new Dictionary<long, PendingResponse>();
        private readonly Dictionary<Type, List<MessageListener>> m_Listeners = new Dictionary<Type, List<MessageListener>>();
        private readonly List<MessageListener> m_GlobalListeners = new List<MessageListener>();
        private readonly List<MessageListener> m_DispatchCache = new List<MessageListener>();
        private long m_NextSequenceId;

        internal NetworkChannel(string name, NetworkEndpoint endpoint, INetworkCodec codec, INetworkTransport transport)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Network channel name cannot be empty.", nameof(name)) : name;
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            m_Codec = codec ?? throw new ArgumentNullException(nameof(codec));
            m_Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            m_Transport.Received += OnTransportReceived;
            ResponseTimeout = TimeSpan.FromSeconds(30d);
        }

        public string Name { get; }

        public NetworkEndpoint Endpoint { get; }

        public NetworkChannelStatus Status { get; private set; } = NetworkChannelStatus.Closed;

        internal Exception LastException { get; private set; }

        internal TimeSpan ResponseTimeout { get; set; }

        internal int PendingResponseCount => m_PendingResponses.Count;

        internal int ListenerCount
        {
            get
            {
                var count = m_GlobalListeners.Count;
                foreach (var pair in m_Listeners)
                {
                    count += pair.Value.Count;
                }

                return count;
            }
        }

        public async UniTask ConnectAsync()
        {
            if (Status == NetworkChannelStatus.Connected)
            {
                return;
            }

            Status = NetworkChannelStatus.Connecting;
            try
            {
                await m_Transport.ConnectAsync(Endpoint);
                Status = NetworkChannelStatus.Connected;
                LastException = null;
            }
            catch (Exception exception)
            {
                Status = NetworkChannelStatus.Failed;
                LastException = exception;
                throw new NetworkException($"Network channel '{Name}' connection failed.", NetworkFailureKind.Connection, exception);
            }
        }

        public async UniTask CloseAsync()
        {
            if (Status == NetworkChannelStatus.Closed)
            {
                return;
            }

            Status = NetworkChannelStatus.Closed;
            CancelPendingResponses(new NetworkException($"Network channel '{Name}' was closed.", NetworkFailureKind.Canceled));
            await m_Transport.CloseAsync();
        }

        public async UniTask SendAsync(Message request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (Status != NetworkChannelStatus.Connected)
            {
                throw new GameException($"Network channel '{Name}' is not connected.");
            }

            if (request.SequenceId == 0L)
            {
                request.SequenceId = ++m_NextSequenceId;
            }

            if (m_PendingResponses.ContainsKey(request.SequenceId))
            {
                throw new GameException($"Network request sequence '{request.SequenceId}' is already pending.");
            }

            var pending = new PendingResponse();
            m_PendingResponses.Add(request.SequenceId, pending);
            ExpirePendingResponseAsync(request.SequenceId, pending).Forget();

            byte[] payload;
            try
            {
                payload = m_Codec.Encode(request);
            }
            catch (Exception exception)
            {
                m_PendingResponses.Remove(request.SequenceId);
                LastException = exception;
                throw new NetworkException("Network request encode failed.", NetworkFailureKind.InvalidResponse, exception);
            }

            try
            {
                await m_Transport.SendAsync(payload);
            }
            catch (Exception exception)
            {
                m_PendingResponses.Remove(request.SequenceId);
                var networkException = new NetworkException("Network request send failed.", NetworkFailureKind.Send, exception);
                pending.SetException(networkException);
                LastException = networkException;
                throw networkException;
            }
        }

        public async UniTask<TResponse> WaitAsync<TResponse>(Message request) where TResponse : Message
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.SequenceId == 0L || !m_PendingResponses.TryGetValue(request.SequenceId, out var pending))
            {
                throw new GameException("Network request does not have a pending response slot.");
            }

            try
            {
                var response = await pending.Task;
                if (response is TResponse typedResponse)
                {
                    return typedResponse;
                }

                throw new NetworkException(
                    $"Network response type '{response.GetType().Name}' does not match '{typeof(TResponse).Name}'.",
                    NetworkFailureKind.InvalidResponse);
            }
            finally
            {
                m_PendingResponses.Remove(request.SequenceId);
            }
        }

        public MessageSubscription Register<TMessage>(MessageHandle<TMessage> handle) where TMessage : Message
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            var messageType = typeof(TMessage);
            var listeners = GetListeners(messageType);
            foreach (var listener in listeners)
            {
                if (listener.IsActive && listener.MatchesHandle(handle))
                {
                    return new MessageSubscription(this, listener);
                }
            }

            var newListener = new MessageListener(
                messageType,
                new HandleAdapter<TMessage>(handle),
                handle);
            listeners.Add(newListener);
            return new MessageSubscription(this, newListener);
        }

        public MessageSubscription Subscribe<TMessage>(Action<TMessage> callback) where TMessage : Message
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var messageType = typeof(TMessage);
            var listeners = GetListeners(messageType);
            foreach (var listener in listeners)
            {
                if (listener.IsActive && listener.MatchesCallback(callback))
                {
                    return new MessageSubscription(this, listener);
                }
            }

            var newListener = new MessageListener(
                messageType,
                callback,
                (_, message) => callback((TMessage)message));
            listeners.Add(newListener);
            return new MessageSubscription(this, newListener);
        }

        public MessageSubscription Subscribe(Action<Message> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            foreach (var listener in m_GlobalListeners)
            {
                if (listener.IsActive && listener.MatchesCallback(callback))
                {
                    return new MessageSubscription(this, listener);
                }
            }

            var newListener = new MessageListener(
                typeof(Message),
                callback,
                (_, message) => callback(message));
            m_GlobalListeners.Add(newListener);
            return new MessageSubscription(this, newListener);
        }

        public void Release()
        {
            CloseAsync().Forget();
            ClearSubscriptions();
            m_Transport.Received -= OnTransportReceived;
            m_Transport.Release();
        }

        internal void Receive(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.SequenceId != 0L && m_PendingResponses.TryGetValue(message.SequenceId, out var pending))
            {
                pending.SetResult(message);
                return;
            }

            Dispatch(message);
        }

        internal void Unsubscribe(MessageListener listener)
        {
            if (listener == null)
            {
                return;
            }

            listener.Deactivate();
            if (listener.MessageType == typeof(Message))
            {
                m_GlobalListeners.Remove(listener);
                return;
            }

            if (!m_Listeners.TryGetValue(listener.MessageType, out var listeners))
            {
                return;
            }

            listeners.Remove(listener);
            if (listeners.Count == 0)
            {
                m_Listeners.Remove(listener.MessageType);
            }
        }

        private List<MessageListener> GetListeners(Type messageType)
        {
            if (!m_Listeners.TryGetValue(messageType, out var listeners))
            {
                listeners = new List<MessageListener>();
                m_Listeners.Add(messageType, listeners);
            }

            return listeners;
        }

        private void OnTransportReceived(byte[] data)
        {
            try
            {
                Receive(m_Codec.Decode(data));
            }
            catch (Exception exception)
            {
                LastException = exception is NetworkException networkException
                    ? networkException
                    : new NetworkException("Network message decode failed.", NetworkFailureKind.Decode, exception);
            }
        }

        private void Dispatch(Message message)
        {
            if (m_Listeners.TryGetValue(message.GetType(), out var listeners))
            {
                DispatchListeners(listeners, message);
            }

            DispatchListeners(m_GlobalListeners, message);
        }

        private void DispatchListeners(List<MessageListener> listeners, Message message)
        {
            if (listeners.Count == 0)
            {
                return;
            }

            m_DispatchCache.Clear();
            m_DispatchCache.AddRange(listeners);
            foreach (var listener in m_DispatchCache)
            {
                if (!listener.IsActive)
                {
                    continue;
                }

                try
                {
                    listener.Invoke(this, message);
                }
                catch (Exception exception)
                {
                    LastException = exception;
                }
            }

            m_DispatchCache.Clear();
        }

        private void ClearSubscriptions()
        {
            foreach (var pair in m_Listeners)
            {
                foreach (var listener in pair.Value)
                {
                    listener.Deactivate();
                }
            }

            foreach (var listener in m_GlobalListeners)
            {
                listener.Deactivate();
            }

            m_Listeners.Clear();
            m_GlobalListeners.Clear();
            m_DispatchCache.Clear();
        }

        private void CancelPendingResponses(Exception exception)
        {
            var pendingResponses = new List<PendingResponse>(m_PendingResponses.Values);
            m_PendingResponses.Clear();

            foreach (var pending in pendingResponses)
            {
                pending.SetException(exception);
            }
        }

        private async UniTaskVoid ExpirePendingResponseAsync(long sequenceId, PendingResponse pending)
        {
            if (ResponseTimeout <= TimeSpan.Zero)
            {
                return;
            }

            await UniTask.Delay(ResponseTimeout);
            if (!m_PendingResponses.TryGetValue(sequenceId, out var current) || !ReferenceEquals(current, pending))
            {
                return;
            }

            if (current.IsCompleted)
            {
                m_PendingResponses.Remove(sequenceId);
                return;
            }

            current.SetException(new NetworkException("Network response timed out.", NetworkFailureKind.Timeout));
            m_PendingResponses.Remove(sequenceId);
        }

        private sealed class PendingResponse
        {
            private readonly UniTaskCompletionSource<Message> m_Source = new UniTaskCompletionSource<Message>();

            public bool IsCompleted { get; private set; }

            public UniTask<Message> Task => m_Source.Task;

            public void SetResult(Message message)
            {
                IsCompleted = true;
                m_Source.TrySetResult(message);
            }

            public void SetException(Exception exception)
            {
                IsCompleted = true;
                m_Source.TrySetException(exception);
            }
        }

        private sealed class HandleAdapter<TMessage> : MessageHandle<Message> where TMessage : Message
        {
            private readonly MessageHandle<TMessage> m_Handle;

            public HandleAdapter(MessageHandle<TMessage> handle)
            {
                m_Handle = handle;
            }

            public override void Handle(IChannel channel, Message message)
            {
                m_Handle.Handle(channel, (TMessage)message);
            }
        }
    }
}

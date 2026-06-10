using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 定义 Network Channel 类型。
    /// </summary>
    internal sealed class NetworkChannel : IChannel
    {
        /// <summary>
        /// 存储 Codec。
        /// </summary>
        private readonly INetworkCodec m_Codec;
        /// <summary>
        /// 存储 Transport。
        /// </summary>
        private readonly INetworkTransport m_Transport;
        /// <summary>
        /// 存储 Pending Responses。
        /// </summary>
        private readonly Dictionary<long, PendingResponse> m_PendingResponses = new Dictionary<long, PendingResponse>();
        /// <summary>
        /// 存储 Listeners。
        /// </summary>
        private readonly Dictionary<Type, List<MessageListener>> m_Listeners = new Dictionary<Type, List<MessageListener>>();
        /// <summary>
        /// 存储 Global Listeners。
        /// </summary>
        private readonly List<MessageListener> m_GlobalListeners = new List<MessageListener>();
        /// <summary>
        /// 存储 Dispatch Cache。
        /// </summary>
        private readonly List<MessageListener> m_DispatchCache = new List<MessageListener>();
        /// <summary>
        /// 存储 Next Sequence Id。
        /// </summary>
        private long m_NextSequenceId;

        /// <summary>
        /// 初始化 Network Channel。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <param name="endpoint">endpoint 参数。</param>
        /// <param name="codec">codec 参数。</param>
        /// <param name="transport">transport 参数。</param>
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

        /// <summary>
        /// 存储 Pending Response Count。
        /// </summary>
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

        /// <summary>
        /// 执行 Connect Async。
        /// </summary>
        /// <returns>操作完成任务。</returns>
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

        /// <summary>
        /// 执行 Close Async。
        /// </summary>
        /// <returns>操作完成任务。</returns>
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

        /// <summary>
        /// 执行 Send Async。
        /// </summary>
        /// <param name="request">request 参数。</param>
        /// <returns>操作完成任务。</returns>
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

        /// <summary>
        /// 执行 Wait Async。
        /// </summary>
        /// <typeparam name="TResponse">泛型类型参数。</typeparam>
        /// <param name="request">request 参数。</param>
        /// <returns>操作完成任务。</returns>
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

        /// <summary>
        /// 注册 member。
        /// </summary>
        /// <typeparam name="TMessage">泛型类型参数。</typeparam>
        /// <param name="handle">handle 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 Subscribe。
        /// </summary>
        /// <typeparam name="TMessage">泛型类型参数。</typeparam>
        /// <param name="callback">callback 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 Subscribe。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 Release。
        /// </summary>
        public void Release()
        {
            CloseAsync().Forget();
            ClearSubscriptions();
            m_Transport.Received -= OnTransportReceived;
            m_Transport.Release();
        }

        /// <summary>
        /// 执行 Receive。
        /// </summary>
        /// <param name="message">message 参数。</param>
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

        /// <summary>
        /// 执行 Unsubscribe。
        /// </summary>
        /// <param name="listener">listener 参数。</param>
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

        /// <summary>
        /// 获取 Listeners。
        /// </summary>
        /// <param name="messageType">message Type 参数。</param>
        /// <returns>执行结果。</returns>
        private List<MessageListener> GetListeners(Type messageType)
        {
            if (!m_Listeners.TryGetValue(messageType, out var listeners))
            {
                listeners = new List<MessageListener>();
                m_Listeners.Add(messageType, listeners);
            }

            return listeners;
        }

        /// <summary>
        /// 处理 Transport Received 回调。
        /// </summary>
        /// <param name="data">data 参数。</param>
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

        /// <summary>
        /// 执行 Dispatch。
        /// </summary>
        /// <param name="message">message 参数。</param>
        private void Dispatch(Message message)
        {
            if (m_Listeners.TryGetValue(message.GetType(), out var listeners))
            {
                DispatchListeners(listeners, message);
            }

            DispatchListeners(m_GlobalListeners, message);
        }

        /// <summary>
        /// 执行 Dispatch Listeners。
        /// </summary>
        /// <param name="listeners">listeners 参数。</param>
        /// <param name="message">message 参数。</param>
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

        /// <summary>
        /// 清理 Subscriptions。
        /// </summary>
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

        /// <summary>
        /// 执行 Cancel Pending Responses。
        /// </summary>
        /// <param name="exception">exception 参数。</param>
        private void CancelPendingResponses(Exception exception)
        {
            var pendingResponses = new List<PendingResponse>(m_PendingResponses.Values);
            m_PendingResponses.Clear();

            foreach (var pending in pendingResponses)
            {
                pending.SetException(exception);
            }
        }

        /// <summary>
        /// 执行 Expire Pending Response Async。
        /// </summary>
        /// <param name="sequenceId">sequence Id 参数。</param>
        /// <param name="pending">pending 参数。</param>
        /// <returns>操作完成任务。</returns>
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

        /// <summary>
        /// 定义 Pending Response 类型。
        /// </summary>
        private sealed class PendingResponse
        {
            /// <summary>
            /// 存储 Source。
            /// </summary>
            private readonly UniTaskCompletionSource<Message> m_Source = new UniTaskCompletionSource<Message>();

            public bool IsCompleted { get; private set; }

            /// <summary>
            /// 存储 Task。
            /// </summary>
            public UniTask<Message> Task => m_Source.Task;

            /// <summary>
            /// 设置 Result。
            /// </summary>
            /// <param name="message">message 参数。</param>
            public void SetResult(Message message)
            {
                IsCompleted = true;
                m_Source.TrySetResult(message);
            }

            /// <summary>
            /// 设置 Exception。
            /// </summary>
            /// <param name="exception">exception 参数。</param>
            public void SetException(Exception exception)
            {
                IsCompleted = true;
                m_Source.TrySetException(exception);
            }
        }

        /// <summary>
        /// 把强类型消息处理器适配为基础消息处理器。
        /// </summary>
        /// <typeparam name="TMessage">消息类型。</typeparam>
        private sealed class HandleAdapter<TMessage> : MessageHandle<Message> where TMessage : Message
        {
            /// <summary>
            /// 存储 Handle。
            /// </summary>
            private readonly MessageHandle<TMessage> m_Handle;

            /// <summary>
            /// 初始化 Handle Adapter。
            /// </summary>
            /// <param name="handle">handle 参数。</param>
            public HandleAdapter(MessageHandle<TMessage> handle)
            {
                m_Handle = handle;
            }

            /// <summary>
            /// 处理 member。
            /// </summary>
            /// <param name="channel">channel 参数。</param>
            /// <param name="message">message 参数。</param>
            public override void Handle(IChannel channel, Message message)
            {
                m_Handle.Handle(channel, (TMessage)message);
            }
        }
    }
}

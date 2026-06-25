using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    internal sealed partial class NetworkChannel : IChannel
    {
        private readonly INetworkCodec m_Codec;
        private readonly INetworkTransport m_Transport;
        private readonly Dictionary<long, PendingResponse> m_PendingResponses = new Dictionary<long, PendingResponse>();
        private readonly Dictionary<Type, List<MessageListener>> m_Listeners = new Dictionary<Type, List<MessageListener>>();
        private readonly List<MessageListener> m_GlobalListeners = new List<MessageListener>();
        private readonly List<MessageListener> m_DispatchCache = new List<MessageListener>();
        private long m_NextSequenceId;

        /// <summary>
        /// 初始化 Network Channel。
        /// </summary>
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
    }
}

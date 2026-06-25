using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 定义 Network Channel 类型。
    /// </summary>
    internal sealed partial class NetworkChannel : IChannel
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
    }
}

using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    internal sealed partial class NetworkChannel
    {
        /// <summary>
        /// 执行 Connect Async。
        /// </summary>
        public async UniTask ConnectAsync()
        {
            if (Status == NetworkChannelStatus.Connected)
            {
                return;
            }

            Status = NetworkChannelStatus.Connecting;
            SetInboundAcceptance(true);
            try
            {
                await m_Transport.ConnectAsync(Endpoint);
                Status = NetworkChannelStatus.Connected;
                LastException = null;
            }
            catch (Exception exception)
            {
                SetInboundAcceptance(false);
                Status = NetworkChannelStatus.Failed;
                LastException = exception;
                throw new NetworkException($"Network channel '{Name}' connection failed.", NetworkFailureKind.Connection, exception);
            }
        }

        /// <summary>
        /// 执行 Close Async。
        /// </summary>
        public async UniTask CloseAsync()
        {
            if (Status == NetworkChannelStatus.Closed)
            {
                return;
            }

            SetInboundAcceptance(false);
            Status = NetworkChannelStatus.Closed;
            CancelPendingResponses(new NetworkException($"Network channel '{Name}' was closed.", NetworkFailureKind.Canceled));
            await m_Transport.CloseAsync();
        }

        /// <summary>
        /// 执行 Release。
        /// </summary>
        public void Release()
        {
            SetInboundAcceptance(false);
            if (Status != NetworkChannelStatus.Closed)
            {
                Status = NetworkChannelStatus.Closed;
                CancelPendingResponses(new NetworkException($"Network channel '{Name}' was closed.", NetworkFailureKind.Canceled));
            }

            ClearSubscriptions();
            m_Transport.Received -= OnTransportReceived;
            m_Transport.Release();
        }

        private void SetInboundAcceptance(bool accept)
        {
            lock (m_InboundQueueLock)
            {
                m_AcceptInbound = accept;
                m_InboundFailure = null;
                if (!accept || m_InboundQueue.Count > 0)
                {
                    m_InboundQueue.Clear();
                    m_InboundQueueBytes = 0L;
                }
            }

            if (!accept)
            {
                m_InboundDrainCache.Clear();
            }
        }
    }
}

using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    internal sealed partial class NetworkChannel
    {
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
        /// 执行 Release。
        /// </summary>
        public void Release()
        {
            if (Status != NetworkChannelStatus.Closed)
            {
                Status = NetworkChannelStatus.Closed;
                CancelPendingResponses(new NetworkException($"Network channel '{Name}' was closed.", NetworkFailureKind.Canceled));
            }

            ClearSubscriptions();
            m_Transport.Received -= OnTransportReceived;
            m_Transport.Release();
        }
    }
}

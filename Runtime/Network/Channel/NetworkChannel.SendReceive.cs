using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    internal sealed partial class NetworkChannel
    {
        /// <summary>
        /// 执行 Send Async。
        /// </summary>
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

            await SendPayloadAsync(request);
        }

        /// <summary>
        /// 执行 Wait Async。
        /// </summary>
        /// <typeparam name="TResponse">泛型类型参数。</typeparam>
        public async UniTask<TResponse> WaitAsync<TResponse>(Message request) where TResponse : Message
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
            StartPendingTimeout(request.SequenceId, pending);

            try
            {
                await SendPayloadAsync(request);
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
                CancelPendingTimeout(pending);
            }
        }

        private void Receive(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (IsResponseMessage(message) && message.SequenceId != 0L && m_PendingResponses.TryGetValue(message.SequenceId, out var pending))
            {
                pending.SetResult(message);
                CancelPendingTimeout(pending);
                return;
            }

            Dispatch(message);
        }

        /// <summary>
        /// 判断 message 是否为响应消息。
        /// </summary>
        private bool IsResponseMessage(Message message)
        {
            return message.IsResponse;
        }

        /// <summary>
        /// 发送 Payload。
        /// </summary>
        private async UniTask SendPayloadAsync(Message request)
        {
            byte[] payload;
            try
            {
                payload = m_Codec.Encode(request);
            }
            catch (Exception exception)
            {
                LastException = exception;
                throw new NetworkException("Network request encode failed.", NetworkFailureKind.InvalidResponse, exception);
            }

            try
            {
                await m_Transport.SendAsync(payload);
            }
            catch (Exception exception)
            {
                var networkException = new NetworkException("Network request send failed.", NetworkFailureKind.Send, exception);
                LastException = networkException;
                throw networkException;
            }
        }

        /// <summary>
        /// 处理 Transport Received 回调。
        /// </summary>
        private void OnTransportReceived(byte[] data)
        {
            lock (m_InboundQueueLock)
            {
                if (!m_AcceptInbound)
                {
                    return;
                }

                if (data == null || data.Length > m_Options.MaxPacketBytes)
                {
                    RejectInboundLocked(
                        $"Network channel '{Name}' received a packet larger than the {m_Options.MaxPacketBytes}-byte limit.");
                    return;
                }

                if (m_InboundQueue.Count >= m_Options.MaxQueuedMessages ||
                    m_InboundQueueBytes > m_Options.MaxQueuedBytes - data.Length)
                {
                    RejectInboundLocked(
                        $"Network channel '{Name}' exceeded its inbound queue capacity.");
                    return;
                }

                var ownedData = (byte[])data.Clone();
                m_InboundQueue.Enqueue(ownedData);
                m_InboundQueueBytes += ownedData.Length;
            }
        }

        internal void DrainInbound()
        {
            if (Status != NetworkChannelStatus.Connected)
            {
                return;
            }

            NetworkException inboundFailure;
            lock (m_InboundQueueLock)
            {
                inboundFailure = m_InboundFailure;
                m_InboundFailure = null;
                while (inboundFailure == null &&
                       m_InboundQueue.Count > 0 &&
                       m_InboundDrainCache.Count < m_Options.MaxMessagesPerFrame)
                {
                    var data = m_InboundQueue.Dequeue();
                    m_InboundQueueBytes -= data.Length;
                    m_InboundDrainCache.Add(data);
                }
            }

            if (inboundFailure != null)
            {
                FailInbound(inboundFailure);
                return;
            }

            foreach (var data in m_InboundDrainCache)
            {
                if (Status != NetworkChannelStatus.Connected)
                {
                    break;
                }

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

            m_InboundDrainCache.Clear();
        }

        private void RejectInboundLocked(string message)
        {
            m_AcceptInbound = false;
            m_InboundQueue.Clear();
            m_InboundQueueBytes = 0L;
            m_InboundFailure = new NetworkException(message, NetworkFailureKind.Receive);
        }

        private void FailInbound(NetworkException exception)
        {
            LastException = exception;
            Status = NetworkChannelStatus.Failed;
            CancelPendingResponses(exception);
            CloseTransportAfterInboundFailureAsync(exception).Forget(UnityEngine.Debug.LogException);
        }

        private async UniTask CloseTransportAfterInboundFailureAsync(NetworkException inboundException)
        {
            try
            {
                await m_Transport.CloseAsync();
            }
            catch (Exception exception)
            {
                LastException = new AggregateException(inboundException, exception);
            }
        }

        /// <summary>
        /// 执行 Cancel Pending Responses。
        /// </summary>
        private void CancelPendingResponses(Exception exception)
        {
            var pendingResponses = new List<PendingResponse>(m_PendingResponses.Values);
            m_PendingResponses.Clear();

            foreach (var pending in pendingResponses)
            {
                pending.SetException(exception);
                CancelPendingTimeout(pending);
            }
        }

        private void StartPendingTimeout(long sequenceId, PendingResponse pending)
        {
            if (ResponseTimeout <= TimeSpan.Zero)
            {
                return;
            }

            var cancellationToken = pending.StartTimeout();
            m_TimeoutRegistrationCount++;
            ExpirePendingResponseAsync(sequenceId, pending, cancellationToken).Forget(UnityEngine.Debug.LogException);
        }

        private void CancelPendingTimeout(PendingResponse pending)
        {
            if (pending.CancelTimeout())
            {
                m_TimeoutRegistrationCount--;
            }
        }

        /// <summary>
        /// 执行 Expire Pending Response Async。
        /// </summary>
        /// <param name="sequenceId">sequence Id 参数。</param>
        private async UniTask ExpirePendingResponseAsync(
            long sequenceId,
            PendingResponse pending,
            CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(ResponseTimeout, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!m_PendingResponses.TryGetValue(sequenceId, out var current) || !ReferenceEquals(current, pending))
            {
                CancelPendingTimeout(pending);
                return;
            }

            if (current.IsCompleted)
            {
                m_PendingResponses.Remove(sequenceId);
                CancelPendingTimeout(current);
                return;
            }

            current.SetException(new NetworkException("Network response timed out.", NetworkFailureKind.Timeout));
            m_PendingResponses.Remove(sequenceId);
            CancelPendingTimeout(current);
        }
    }
}

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    internal sealed partial class NetworkChannel
    {
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

            await SendPayloadAsync(request);
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
            }
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

            if (IsResponseMessage(message) && message.SequenceId != 0L && m_PendingResponses.TryGetValue(message.SequenceId, out var pending))
            {
                pending.SetResult(message);
                return;
            }

            Dispatch(message);
        }

        /// <summary>
        /// 判断 message 是否为响应消息。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <returns>执行结果。</returns>
        private bool IsResponseMessage(Message message)
        {
            return message.IsResponse;
        }

        /// <summary>
        /// 发送 Payload。
        /// </summary>
        /// <param name="request">request 参数。</param>
        /// <returns>操作完成任务。</returns>
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
    }
}

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    internal sealed partial class NetworkChannel
    {
        private sealed class PendingResponse
        {
            private readonly UniTaskCompletionSource<Message> m_Source = new UniTaskCompletionSource<Message>();
            private CancellationTokenSource m_TimeoutCancellation;

            public bool IsCompleted { get; private set; }
            public UniTask<Message> Task => m_Source.Task;

            public CancellationToken StartTimeout()
            {
                if (m_TimeoutCancellation != null)
                {
                    throw new InvalidOperationException("Pending response timeout has already been started.");
                }

                m_TimeoutCancellation = new CancellationTokenSource();
                return m_TimeoutCancellation.Token;
            }

            public bool CancelTimeout()
            {
                var cancellation = m_TimeoutCancellation;
                if (cancellation == null)
                {
                    return false;
                }

                m_TimeoutCancellation = null;
                cancellation.Cancel();
                cancellation.Dispose();
                return true;
            }

            /// <summary>
            /// 设置 Result。
            /// </summary>
            public void SetResult(Message message)
            {
                if (m_Source.TrySetResult(message))
                {
                    IsCompleted = true;
                }
            }

            /// <summary>
            /// 设置 Exception。
            /// </summary>
            public void SetException(Exception exception)
            {
                if (m_Source.TrySetException(exception))
                {
                    IsCompleted = true;
                }
            }
        }
    }
}

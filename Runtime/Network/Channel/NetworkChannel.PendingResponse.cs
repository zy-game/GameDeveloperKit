using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    internal sealed partial class NetworkChannel
    {
        private sealed class PendingResponse
        {
            private readonly UniTaskCompletionSource<Message> m_Source = new UniTaskCompletionSource<Message>();

            public bool IsCompleted { get; private set; }
            public UniTask<Message> Task => m_Source.Task;

            /// <summary>
            /// 设置 Result。
            /// </summary>
            public void SetResult(Message message)
            {
                IsCompleted = true;
                m_Source.TrySetResult(message);
            }

            /// <summary>
            /// 设置 Exception。
            /// </summary>
            public void SetException(Exception exception)
            {
                IsCompleted = true;
                m_Source.TrySetException(exception);
            }
        }
    }
}

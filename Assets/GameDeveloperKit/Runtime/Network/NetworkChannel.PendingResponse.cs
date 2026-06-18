using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    internal sealed partial class NetworkChannel
    {
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
    }
}

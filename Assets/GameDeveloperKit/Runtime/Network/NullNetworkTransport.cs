using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 定义 Null Network Transport 类型。
    /// </summary>
    internal sealed class NullNetworkTransport : INetworkTransport
    {
        /// <summary>
        /// 存储 Received。
        /// </summary>
        private Action<byte[]> m_Received;

        /// <summary>
        /// 定义 member 事件。
        /// </summary>
        public event Action<byte[]> Received
        {
            add => m_Received += value;
            remove => m_Received -= value;
        }

        /// <summary>
        /// 执行 Connect Async。
        /// </summary>
        /// <param name="endpoint">endpoint 参数。</param>
        /// <returns>操作完成任务。</returns>
        public UniTask ConnectAsync(NetworkEndpoint endpoint)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 执行 Send Async。
        /// </summary>
        /// <param name="data">data 参数。</param>
        /// <returns>操作完成任务。</returns>
        public UniTask SendAsync(byte[] data)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 执行 Close Async。
        /// </summary>
        /// <returns>操作完成任务。</returns>
        public UniTask CloseAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 执行 Emit。
        /// </summary>
        /// <param name="data">data 参数。</param>
        internal void Emit(byte[] data)
        {
            m_Received?.Invoke(data);
        }

        /// <summary>
        /// 执行 Release。
        /// </summary>
        public void Release()
        {
            m_Received = null;
        }
    }
}

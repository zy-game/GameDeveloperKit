using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    internal sealed class NullNetworkTransport : INetworkTransport
    {
        private Action<byte[]> m_Received;
        public event Action<byte[]> Received
        {
            add => m_Received += value;
            remove => m_Received -= value;
        }

        /// <summary>
        /// 执行 Connect Async。
        /// </summary>
        public UniTask ConnectAsync(NetworkEndpoint endpoint)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 执行 Send Async。
        /// </summary>
        public UniTask SendAsync(byte[] data)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 执行 Close Async。
        /// </summary>
        public UniTask CloseAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 执行 Emit。
        /// </summary>
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

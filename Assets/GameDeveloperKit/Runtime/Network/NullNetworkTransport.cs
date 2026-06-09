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

        public UniTask ConnectAsync(NetworkEndpoint endpoint)
        {
            return UniTask.CompletedTask;
        }

        public UniTask SendAsync(byte[] data)
        {
            return UniTask.CompletedTask;
        }

        public UniTask CloseAsync()
        {
            return UniTask.CompletedTask;
        }

        internal void Emit(byte[] data)
        {
            m_Received?.Invoke(data);
        }

        public void Release()
        {
            m_Received = null;
        }
    }
}

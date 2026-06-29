using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络连接契约。
    /// </summary>
    public interface IChannel : IReference
    {
        string Name { get; }

        NetworkEndpoint Endpoint { get; }

        NetworkChannelStatus Status { get; }

        UniTask ConnectAsync();

        UniTask CloseAsync();

        UniTask SendAsync(Message request);

        UniTask<TResponse> WaitAsync<TResponse>(Message request) where TResponse : Message;

        MessageSubscription Register<TMessage>(MessageHandle<TMessage> handle) where TMessage : Message;

        MessageSubscription Subscribe<TMessage>(Action<TMessage> callback) where TMessage : Message;

        MessageSubscription Subscribe(Action<Message> callback);
    }
}

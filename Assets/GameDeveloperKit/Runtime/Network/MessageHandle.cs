namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络消息处理器。
    /// </summary>
    /// <typeparam name="TMessage">消息类型。</typeparam>
    public abstract class MessageHandle<TMessage> : IReference where TMessage : Message
    {
        public abstract void Handle(IChannel channel, TMessage message);

        public virtual void Release()
        {
        }
    }
}

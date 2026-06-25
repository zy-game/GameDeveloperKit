namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络消息处理器。
    /// </summary>
    /// <typeparam name="TMessage">消息类型。</typeparam>
    public abstract class MessageHandle<TMessage> : IReference where TMessage : Message
    {
        /// <summary>
        /// 处理 member。
        /// </summary>
        /// <param name="channel">channel 参数。</param>
        /// <param name="message">message 参数。</param>
        public abstract void Handle(IChannel channel, TMessage message);

        /// <summary>
        /// 执行 Release。
        /// </summary>
        public virtual void Release()
        {
        }
    }
}

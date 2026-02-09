namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络消息接口
    /// </summary>
    public interface INetworkMessage
    {
        /// <summary>
        /// 消息ID（用于路由）
        /// </summary>
        int MessageId { get; }
    }

    /// <summary>
    /// 网络消息基类
    /// </summary>
    public abstract class NetworkMessage : INetworkMessage
    {
        public abstract int MessageId { get; }
    }
}

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络管理器接口
    /// </summary>
    public interface INetworkManager : IModule
    {
        /// <summary>
        /// 创建网络终端
        /// </summary>
        INetworkTerminal CreateTerminal(string name, string host, NetworkProtocol protocol = NetworkProtocol.TCP, IMessageSerializer serializer = null);

        /// <summary>
        /// 获取网络终端
        /// </summary>
        INetworkTerminal GetTerminal(string name);

        /// <summary>
        /// 移除网络终端
        /// </summary>
        void RemoveTerminal(string name);
    }
}

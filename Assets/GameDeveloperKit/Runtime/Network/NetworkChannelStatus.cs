namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络连接状态。
    /// </summary>
    public enum NetworkChannelStatus : byte
    {
        Closed = 0,
        Connecting = 1,
        Connected = 2,
        Failed = 3,
    }
}

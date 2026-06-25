namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络消息编解码器。
    /// </summary>
    public interface INetworkCodec
    {
        byte[] Encode(Message message);

        Message Decode(byte[] data);
    }
}

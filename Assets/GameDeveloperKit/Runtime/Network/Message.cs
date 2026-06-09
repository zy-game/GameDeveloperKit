namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络消息基类。
    /// </summary>
    public abstract class Message
    {
        public int MessageId { get; set; }

        public long SequenceId { get; set; }

        public virtual bool IsResponse => false;
    }
}

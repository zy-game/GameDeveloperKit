using MemoryPack;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络消息基类。
    /// </summary>
    public abstract class Message
    {
        [MemoryPackIgnore]
        public int MessageId { get; set; }

        [MemoryPackIgnore]
        public long SequenceId { get; set; }
        [MemoryPackIgnore]
        public virtual bool IsResponse => false;
    }
}

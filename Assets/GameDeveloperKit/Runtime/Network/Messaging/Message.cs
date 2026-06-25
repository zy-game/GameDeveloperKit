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

        /// <summary>
        /// 记录 Is Response 状态。
        /// </summary>
        [MemoryPackIgnore]
        public virtual bool IsResponse => false;
    }
}

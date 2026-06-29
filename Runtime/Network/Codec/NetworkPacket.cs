using MemoryPack;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// MemoryPack 网络消息外壳。
    /// </summary>
    [MemoryPackable]
    public partial struct NetworkPacket
    {
        public int Opcode { get; set; }

        public long SequenceId { get; set; }

        public byte[] Payload { get; set; }
    }
}

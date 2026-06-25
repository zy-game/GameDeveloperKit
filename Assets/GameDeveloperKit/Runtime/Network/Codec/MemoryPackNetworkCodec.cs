using System;
using MemoryPack;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 基于 MemoryPack packet 的网络消息编解码器。
    /// </summary>
    public sealed class MemoryPackNetworkCodec : INetworkCodec
    {
        private readonly NetworkOpcodeRegistry m_OpcodeRegistry;

        /// <summary>
        /// 初始化 MemoryPack Network Codec。
        /// </summary>
        public MemoryPackNetworkCodec()
            : this(NetworkOpcodeRegistry.Shared)
        {
        }

        internal MemoryPackNetworkCodec(NetworkOpcodeRegistry opcodeRegistry)
        {
            m_OpcodeRegistry = opcodeRegistry ?? throw new ArgumentNullException(nameof(opcodeRegistry));
        }

        /// <summary>
        /// 执行 Encode。
        /// </summary>
        public byte[] Encode(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var messageType = message.GetType();
            var opcode = m_OpcodeRegistry.GetOpcode(messageType);
            var payload = MemoryPackSerializer.Serialize(messageType, message);
            if (payload == null || payload.Length == 0)
            {
                throw new NetworkException(
                    $"Network message payload for '{messageType.Name}' is invalid.",
                    NetworkFailureKind.InvalidResponse);
            }

            message.MessageId = opcode;
            var packet = new NetworkPacket
            {
                Opcode = opcode,
                SequenceId = message.SequenceId,
                Payload = payload
            };
            return MemoryPackSerializer.Serialize(packet);
        }

        /// <summary>
        /// 执行 Decode。
        /// </summary>
        public Message Decode(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length == 0)
            {
                throw new NetworkException("Network packet data is empty.", NetworkFailureKind.Decode);
            }

            NetworkPacket packet;
            try
            {
                packet = MemoryPackSerializer.Deserialize<NetworkPacket>(data);
            }
            catch (Exception exception)
            {
                throw new NetworkException("Network packet decode failed.", NetworkFailureKind.Decode, exception);
            }

            if (packet.Opcode <= 0)
            {
                throw new NetworkException("Network packet opcode is invalid.", NetworkFailureKind.Decode);
            }

            if (!m_OpcodeRegistry.TryGetType(packet.Opcode, out var messageType))
            {
                throw new NetworkException($"Network message opcode '{packet.Opcode}' is not registered.", NetworkFailureKind.Decode);
            }

            if (packet.Payload == null || packet.Payload.Length == 0)
            {
                throw new NetworkException("Network packet payload is invalid.", NetworkFailureKind.Decode);
            }

            Message message;
            try
            {
                message = MemoryPackSerializer.Deserialize(messageType, packet.Payload) as Message;
            }
            catch (Exception exception)
            {
                throw new NetworkException("Network message payload decode failed.", NetworkFailureKind.Decode, exception);
            }

            if (message == null)
            {
                throw new NetworkException("Network message payload is invalid.", NetworkFailureKind.Decode);
            }

            message.MessageId = packet.Opcode;
            message.SequenceId = packet.SequenceId;
            return message;
        }
    }
}

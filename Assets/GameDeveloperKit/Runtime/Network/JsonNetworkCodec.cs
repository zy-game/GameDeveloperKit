using System;
using System.Text;
using Newtonsoft.Json;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 基于 JSON envelope 的默认消息编解码器。
    /// </summary>
    public sealed class JsonNetworkCodec : INetworkCodec
    {
        public byte[] Encode(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var envelope = new MessageEnvelope
            {
                TypeName = message.GetType().AssemblyQualifiedName,
                MessageId = message.MessageId,
                SequenceId = message.SequenceId,
                Payload = JsonConvert.SerializeObject(message)
            };

            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope));
        }

        public Message Decode(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var text = Encoding.UTF8.GetString(data);
            var envelope = JsonConvert.DeserializeObject<MessageEnvelope>(text);
            if (envelope == null || string.IsNullOrEmpty(envelope.TypeName))
            {
                throw new NetworkException("Network message envelope is invalid.", NetworkFailureKind.Decode);
            }

            var messageType = Type.GetType(envelope.TypeName);
            if (messageType == null || !typeof(Message).IsAssignableFrom(messageType))
            {
                throw new NetworkException($"Network message type '{envelope.TypeName}' is invalid.", NetworkFailureKind.Decode);
            }

            var message = (Message)JsonConvert.DeserializeObject(envelope.Payload, messageType);
            if (message == null)
            {
                throw new NetworkException("Network message payload is invalid.", NetworkFailureKind.Decode);
            }

            message.MessageId = envelope.MessageId;
            message.SequenceId = envelope.SequenceId;
            return message;
        }

        private sealed class MessageEnvelope
        {
            public string TypeName;
            public int MessageId;
            public long SequenceId;
            public string Payload;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 消息序列化器接口
    /// </summary>
    public interface IMessageSerializer
    {
        byte[] Serialize(INetworkMessage message);
        INetworkMessage Deserialize(byte[] data);
    }

    /// <summary>
    /// JSON消息序列化器
    /// </summary>
    public class JsonMessageSerializer : IMessageSerializer
    {
        private static readonly Dictionary<int, Type> s_messageTypes = new();

        /// <summary>
        /// 注册消息类型
        /// </summary>
        public static void Register<T>() where T : INetworkMessage
        {
            var type = typeof(T);
            s_messageTypes[type.GetHashCode()] = type;
        }

        public byte[] Serialize(INetworkMessage message)
        {
            var wrapper = new MessageWrapper
            {
                MessageId = message.MessageId,
                TypeName = message.GetType().FullName,
                Data = JsonUtility.ToJson(message)
            };
            var json = JsonUtility.ToJson(wrapper);
            return Encoding.UTF8.GetBytes(json);
        }

        public INetworkMessage Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var wrapper = JsonUtility.FromJson<MessageWrapper>(json);
            
            if (!s_messageTypes.TryGetValue(wrapper.MessageId, out var type))
            {
                Game.Debug.Warning($"Unknown message type: {wrapper.TypeName}");
                return null;
            }

            return JsonUtility.FromJson(wrapper.Data, type) as INetworkMessage;
        }

        [Serializable]
        private class MessageWrapper
        {
            public int MessageId;
            public string TypeName;
            public string Data;
        }
    }
}

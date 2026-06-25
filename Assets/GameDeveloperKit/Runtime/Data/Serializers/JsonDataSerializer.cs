using System;
using System.Text;
using Newtonsoft.Json;

namespace GameDeveloperKit.Data.Serializers
{
    public sealed class JsonDataSerializer : IDataSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
        };
        public string Format => "json";

        /// <summary>
        /// 执行 Serialize。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public byte[] Serialize<T>(T data)
        {
            var json = JsonConvert.SerializeObject(data, Settings);
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// 执行 Deserialize。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public T Deserialize<T>(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }

        public byte[] Serialize(Type type, object data)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var json = JsonConvert.SerializeObject(data, type, Settings);
            return Encoding.UTF8.GetBytes(json);
        }

        public object Deserialize(Type type, byte[] bytes)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject(json, type, Settings);
        }
    }
}

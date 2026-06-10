using System;
using System.Text;
using Newtonsoft.Json;

namespace GameDeveloperKit.Data.Serializers
{
    /// <summary>
    /// 定义 Json Data Serializer 类型。
    /// </summary>
    public sealed class JsonDataSerializer : IDataSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
        };

        /// <summary>
        /// 存储 Format。
        /// </summary>
        public string Format => "json";

        /// <summary>
        /// 执行 Serialize。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="data">data 参数。</param>
        /// <returns>执行结果。</returns>
        public byte[] Serialize<T>(T data)
        {
            var json = JsonConvert.SerializeObject(data, Settings);
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// 执行 Deserialize。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="bytes">bytes 参数。</param>
        /// <returns>执行结果。</returns>
        public T Deserialize<T>(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }
    }
}

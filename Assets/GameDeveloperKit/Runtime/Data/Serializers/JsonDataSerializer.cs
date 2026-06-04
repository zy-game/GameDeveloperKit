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

        public byte[] Serialize<T>(T data)
        {
            var json = JsonConvert.SerializeObject(data, Settings);
            return Encoding.UTF8.GetBytes(json);
        }

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

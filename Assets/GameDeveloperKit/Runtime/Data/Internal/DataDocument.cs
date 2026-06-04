using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.Data.Internal
{
    internal sealed class DataDocument
    {
        [JsonProperty("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonProperty("serializer")]
        public string Serializer { get; set; }

        [JsonProperty("typeKey")]
        public string TypeKey { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("dataVersion")]
        public string DataVersion { get; set; }

        [JsonProperty("typeName")]
        public string TypeName { get; set; }

        [JsonProperty("savedAtUtc")]
        public DateTimeOffset SavedAtUtc { get; set; }

        [JsonProperty("payload")]
        public JToken Payload { get; set; }
    }
}

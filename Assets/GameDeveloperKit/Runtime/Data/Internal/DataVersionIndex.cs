using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameDeveloperKit.Data.Internal
{
    /// <summary>
    /// 定义 Data Version Index 类型。
    /// </summary>
    internal sealed class DataVersionIndex
    {
        [JsonProperty("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonProperty("typeKey")]
        public string TypeKey { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("currentVersion")]
        public string CurrentVersion { get; set; }

        [JsonProperty("versions")]
        public List<DataVersionInfo> Versions { get; set; } = new List<DataVersionInfo>();
    }
}

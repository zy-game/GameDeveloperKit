using System;
using Newtonsoft.Json;

namespace GameDeveloperKit.Data
{
    /// <summary>
    /// 定义 Data Version Info 结构。
    /// </summary>
    public readonly struct DataVersionInfo
    {
        /// <summary>
        /// 初始化 Data Version Info。
        /// </summary>
        /// <param name="version">version 参数。</param>
        /// <param name="savedAtUtc">saved At Utc 参数。</param>
        /// <param name="isCurrent">is Current 参数。</param>
        [JsonConstructor]
        public DataVersionInfo(string version, DateTimeOffset savedAtUtc, bool isCurrent)
        {
            Version = version;
            SavedAtUtc = savedAtUtc;
            IsCurrent = isCurrent;
        }

        [JsonProperty("version")]
        public string Version { get; }

        [JsonProperty("savedAtUtc")]
        public DateTimeOffset SavedAtUtc { get; }

        [JsonIgnore]
        public bool IsCurrent { get; }
    }
}

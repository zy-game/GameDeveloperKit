using System;
using Newtonsoft.Json;

namespace GameDeveloperKit.Data
{
    public readonly struct DataVersionInfo
    {
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

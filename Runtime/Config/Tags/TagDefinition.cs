using System;
using Newtonsoft.Json;

namespace GameDeveloperKit.Config
{
    [Serializable]
    public sealed class TagDefinition
    {
        [JsonProperty("key")]
        private string m_Key;

        [JsonProperty("displayName")]
        private string m_DisplayName;

        [JsonProperty("description")]
        private string m_Description;

        [JsonIgnore]
        public string Key
        {
            get => m_Key;
            set => m_Key = value;
        }

        [JsonIgnore]
        public string DisplayName
        {
            get => m_DisplayName;
            set => m_DisplayName = value;
        }

        [JsonIgnore]
        public string Description
        {
            get => m_Description;
            set => m_Description = value;
        }
    }
}

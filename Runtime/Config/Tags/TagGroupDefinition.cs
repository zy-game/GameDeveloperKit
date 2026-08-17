using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameDeveloperKit.Config
{
    [Serializable]
    public sealed class TagGroupDefinition
    {
        [JsonProperty("key")]
        private string m_Key;

        [JsonProperty("displayName")]
        private string m_DisplayName;

        [JsonProperty("fixed")]
        private bool m_Fixed;

        [JsonProperty("tags")]
        private List<TagDefinition> m_Tags = new List<TagDefinition>();

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
        public bool Fixed
        {
            get => m_Fixed;
            set => m_Fixed = value;
        }

        [JsonIgnore]
        public List<TagDefinition> Tags
        {
            get
            {
                m_Tags ??= new List<TagDefinition>();
                return m_Tags;
            }
        }
    }
}

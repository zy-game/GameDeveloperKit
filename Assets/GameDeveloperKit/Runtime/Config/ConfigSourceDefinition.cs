using System;

namespace GameDeveloperKit.Config
{
    [Serializable]
    public sealed class ConfigSourceDefinition
    {
        public string Name;
        public ConfigFormat Format;
        public string Location;
        public string RowTypeName;
        public string KeyField;
        public bool Preload;
    }
}

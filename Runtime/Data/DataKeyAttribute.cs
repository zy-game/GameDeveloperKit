using System;

namespace GameDeveloperKit.Data
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class DataKeyAttribute : Attribute
    {
        /// <summary>
        /// 初始化 Data Key Attribute。
        /// </summary>
        public DataKeyAttribute(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Data key cannot be empty.", nameof(key));
            }

            Key = key;
        }

        public string Key { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class DataSchemaAttribute : Attribute
    {
        public DataSchemaAttribute(int version)
        {
            if (version < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Data schema version must be greater than zero.");
            }

            Version = version;
        }

        public int Version { get; }
    }
}

using System;

namespace GameDeveloperKit.Data
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
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
}

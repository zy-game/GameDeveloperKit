using System;

namespace GameDeveloperKit.Data
{
    /// <summary>
    /// 定义 Data Key Attribute 类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class DataKeyAttribute : Attribute
    {
        /// <summary>
        /// 初始化 Data Key Attribute。
        /// </summary>
        /// <param name="key">key 参数。</param>
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

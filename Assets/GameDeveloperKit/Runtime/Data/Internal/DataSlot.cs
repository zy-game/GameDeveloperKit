using System;
using System.Reflection;

namespace GameDeveloperKit.Data.Internal
{
    /// <summary>
    /// 定义 Data Slot 结构。
    /// </summary>
    internal readonly struct DataSlot : IEquatable<DataSlot>
    {
        /// <summary>
        /// 初始化 Data Slot。
        /// </summary>
        /// <param name="type">type 参数。</param>
        /// <param name="typeKey">type Key 参数。</param>
        /// <param name="key">key 参数。</param>
        private DataSlot(Type type, string typeKey, string key)
        {
            Type = type;
            TypeKey = typeKey;
            Key = key;
        }

        public Type Type { get; }

        public string TypeKey { get; }

        public string Key { get; }

        /// <summary>
        /// 创建 member。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="key">key 参数。</param>
        /// <returns>执行结果。</returns>
        public static DataSlot Create<T>(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Data key cannot be empty.", nameof(key));
            }

            var type = typeof(T);
            var typeKey = GetTypeKey(type);
            return new DataSlot(type, typeKey, key);
        }

        /// <summary>
        /// 执行 Equals。
        /// </summary>
        /// <param name="other">other 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool Equals(DataSlot other)
        {
            return TypeKey == other.TypeKey
                && Key == other.Key;
        }

        /// <summary>
        /// 执行 Equals。
        /// </summary>
        /// <param name="obj">obj 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public override bool Equals(object obj)
        {
            return obj is DataSlot other && Equals(other);
        }

        /// <summary>
        /// 获取 Hash Code。
        /// </summary>
        /// <returns>执行结果。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = TypeKey != null ? TypeKey.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (Key != null ? Key.GetHashCode() : 0);
                return hashCode;
            }
        }

        /// <summary>
        /// 获取 Type Key。
        /// </summary>
        /// <param name="type">type 参数。</param>
        /// <returns>执行结果。</returns>
        private static string GetTypeKey(Type type)
        {
            var attribute = type.GetCustomAttribute<GameDeveloperKit.Data.DataKeyAttribute>();
            if (attribute != null)
            {
                return attribute.Key;
            }

            return string.IsNullOrEmpty(type.FullName) ? type.Name : type.FullName;
        }
    }
}

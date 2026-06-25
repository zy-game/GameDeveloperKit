using System;
using System.Reflection;

namespace GameDeveloperKit.Data.Internal
{
    internal readonly struct DataSlot : IEquatable<DataSlot>
    {
        /// <summary>
        /// 初始化 Data Slot。
        /// </summary>
        /// <param name="typeKey">type Key 参数。</param>
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
        public bool Equals(DataSlot other)
        {
            return TypeKey == other.TypeKey
                && Key == other.Key;
        }

        /// <summary>
        /// 执行 Equals。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is DataSlot other && Equals(other);
        }

        /// <summary>
        /// 获取 Hash Code。
        /// </summary>
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

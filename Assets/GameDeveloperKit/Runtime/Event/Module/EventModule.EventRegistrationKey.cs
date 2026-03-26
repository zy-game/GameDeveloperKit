using System;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class EventModule
    {
        /// <summary>
        /// 事件注册键，用于唯一标识事件订阅。
        /// </summary>
        private readonly struct EventRegistrationKey : IEquatable<EventRegistrationKey>
        {
            private EventRegistrationKey(object value, Type valueType, string name)
            {
                Value = value;
                ValueType = valueType;
                Name = name;
            }

            /// <summary>
            /// 获取事件值。
            /// </summary>
            public object Value { get; }
            /// <summary>
            /// 获取值类型。
            /// </summary>
            public Type ValueType { get; }
            /// <summary>
            /// 获取事件名称。
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// 从事件名称创建注册键。
            /// </summary>
            /// <param name="eventName">事件名称。</param>
            /// <returns>事件注册键。</returns>
            /// <exception cref="ArgumentException">当eventName为空时抛出。</exception>
            public static EventRegistrationKey From(string eventName)
            {
                if (string.IsNullOrWhiteSpace(eventName))
                {
                    throw new ArgumentException("Event name can not be empty.", nameof(eventName));
                }

                return new EventRegistrationKey(eventName, typeof(string), eventName);
            }

            /// <summary>
            /// 从事件ID创建注册键。
            /// </summary>
            /// <param name="eventId">事件ID。</param>
            /// <returns>事件注册键。</returns>
            public static EventRegistrationKey From(int eventId)
            {
                return new EventRegistrationKey(eventId, typeof(int), eventId.ToString());
            }

            /// <summary>
            /// 从枚举值创建注册键。
            /// </summary>
            /// <typeparam name="TEnum">枚举类型。</typeparam>
            /// <param name="eventKey">事件键枚举值。</param>
            /// <returns>事件注册键。</returns>
            public static EventRegistrationKey From<TEnum>(TEnum eventKey)
                where TEnum : struct, Enum
            {
                var enumType = typeof(TEnum);
                return new EventRegistrationKey(eventKey, enumType, $"{enumType.Name}.{eventKey}");
            }

            /// <summary>
            /// 指示当前对象是否等于另一个同类型对象。
            /// </summary>
            /// <param name="other">要比较的对象。</param>
            /// <returns>如果对象相等则为true，否则为false。</returns>
            public bool Equals(EventRegistrationKey other)
            {
                return ValueType == other.ValueType && Equals(Value, other.Value);
            }

            /// <summary>
            /// 指示当前对象是否等于另一个对象。
            /// </summary>
            /// <param name="obj">要比较的对象。</param>
            /// <returns>如果对象相等则为true，否则为false。</returns>
            public override bool Equals(object obj)
            {
                return obj is EventRegistrationKey other && Equals(other);
            }

            /// <summary>
            /// 返回当前对象的哈希代码。
            /// </summary>
            /// <returns>哈希代码。</returns>
            public override int GetHashCode()
            {
                return HashCode.Combine(ValueType, Value);
            }
        }
    }
}

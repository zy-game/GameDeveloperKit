using System;

namespace GameDeveloperKit.Event
{
    /// <summary>
    /// 事件绑定特性，用于声明事件处理类型需要绑定到指定的生成键。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class BindingAttribute : Attribute
    {
        /// <summary>
        /// 初始化事件绑定特性。
        /// </summary>
        /// <param name="key">事件绑定键。</param>
        /// <exception cref="ArgumentNullException">绑定键为空时抛出。</exception>
        /// <exception cref="ArgumentException">绑定键为空字符串或空白字符串时抛出。</exception>
        public BindingAttribute(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.", nameof(key));
            }

            Key = key;
        }

        /// <summary>
        /// 事件绑定键。
        /// </summary>
        public string Key { get; }
    }
}

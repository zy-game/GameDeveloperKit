using System;
using System.Collections.Generic;
using System.Threading;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 事件上下文，包含事件的发送者、事件键、参数等信息。
    /// </summary>
    public sealed class EventContext : IEventContext, IReferencePoolable
    {
        private readonly Dictionary<string, object> _items = new(StringComparer.Ordinal);
        private object[] _arguments;
        private object _argument0;
        private object _argument1;
        private object _argument2;
        private int _argumentCount;

        /// <summary>
        /// 初始化事件上下文的新实例。
        /// </summary>
        public EventContext()
        {
        }

        /// <summary>
        /// 使用无参数初始化事件上下文。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="eventKey">事件键。</param>
        /// <param name="eventName">事件名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        internal void Initialize(object sender, object eventKey, string eventName, CancellationToken cancellationToken)
        {
            InitializeCommon(sender, eventKey, eventName, cancellationToken);
            _arguments = Array.Empty<object>();
            _argumentCount = 0;
        }

        /// <summary>
        /// 使用单个参数初始化事件上下文。
        /// </summary>
        /// <typeparam name="TArg0">参数0类型。</typeparam>
        /// <param name="sender">事件发送者。</param>
        /// <param name="eventKey">事件键。</param>
        /// <param name="eventName">事件名称。</param>
        /// <param name="argument0">参数0。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        internal void Initialize<TArg0>(object sender, object eventKey, string eventName, TArg0 argument0, CancellationToken cancellationToken)
        {
            InitializeCommon(sender, eventKey, eventName, cancellationToken);
            _arguments = null;
            _argument0 = argument0;
            _argument1 = null;
            _argument2 = null;
            _argumentCount = 1;
        }

        /// <summary>
        /// 使用两个参数初始化事件上下文。
        /// </summary>
        /// <typeparam name="TArg0">参数0类型。</typeparam>
        /// <typeparam name="TArg1">参数1类型。</typeparam>
        /// <param name="sender">事件发送者。</param>
        /// <param name="eventKey">事件键。</param>
        /// <param name="eventName">事件名称。</param>
        /// <param name="argument0">参数0。</param>
        /// <param name="argument1">参数1。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        internal void Initialize<TArg0, TArg1>(object sender, object eventKey, string eventName, TArg0 argument0, TArg1 argument1, CancellationToken cancellationToken)
        {
            InitializeCommon(sender, eventKey, eventName, cancellationToken);
            _arguments = null;
            _argument0 = argument0;
            _argument1 = argument1;
            _argument2 = null;
            _argumentCount = 2;
        }

        /// <summary>
        /// 使用三个参数初始化事件上下文。
        /// </summary>
        /// <typeparam name="TArg0">参数0类型。</typeparam>
        /// <typeparam name="TArg1">参数1类型。</typeparam>
        /// <typeparam name="TArg2">参数2类型。</typeparam>
        /// <param name="sender">事件发送者。</param>
        /// <param name="eventKey">事件键。</param>
        /// <param name="eventName">事件名称。</param>
        /// <param name="argument0">参数0。</param>
        /// <param name="argument1">参数1。</param>
        /// <param name="argument2">参数2。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        internal void Initialize<TArg0, TArg1, TArg2>(object sender, object eventKey, string eventName, TArg0 argument0, TArg1 argument1, TArg2 argument2, CancellationToken cancellationToken)
        {
            InitializeCommon(sender, eventKey, eventName, cancellationToken);
            _arguments = null;
            _argument0 = argument0;
            _argument1 = argument1;
            _argument2 = argument2;
            _argumentCount = 3;
        }

        /// <summary>
        /// 使用参数数组初始化事件上下文。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="eventKey">事件键。</param>
        /// <param name="eventName">事件名称。</param>
        /// <param name="arguments">参数数组。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        internal void Initialize(object sender, object eventKey, string eventName, object[] arguments, CancellationToken cancellationToken)
        {
            InitializeCommon(sender, eventKey, eventName, cancellationToken);
            _arguments = arguments ?? Array.Empty<object>();
            _argument0 = null;
            _argument1 = null;
            _argument2 = null;
            _argumentCount = _arguments.Length;
        }

        /// <summary>
        /// 获取事件发送者。
        /// </summary>
        public object Sender { get; private set; }

        /// <summary>
        /// 获取事件键。
        /// </summary>
        public object EventKey { get; private set; }

        /// <summary>
        /// 获取事件名称。
        /// </summary>
        public string EventName { get; private set; }
        /// <summary>
        /// 获取事件参数数组。
        /// </summary>
        public object[] Arguments
        {
            get
            {
                if (_arguments != null)
                {
                    return _arguments;
                }

                _arguments = _argumentCount switch
                {
                    <= 0 => Array.Empty<object>(),
                    1 => new[] { _argument0 },
                    2 => new[] { _argument0, _argument1 },
                    _ => new[] { _argument0, _argument1, _argument2 }
                };
                return _arguments;
            }
        }

        /// <summary>
        /// 获取取消令牌。
        /// </summary>
        public CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// 获取或设置事件是否已被处理。
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        /// 获取指定类型的事件键。
        /// </summary>
        /// <typeparam name="TKey">事件键类型。</typeparam>
        /// <returns>事件键。</returns>
        /// <exception cref="InvalidCastException">当无法转换时抛出。</exception>
        public TKey GetEventKey<TKey>()
        {
            if (EventKey is TKey key)
            {
                return key;
            }

            throw new InvalidCastException($"Can not cast event key '{EventKey?.GetType().FullName ?? "null"}' to '{typeof(TKey).FullName}'.");
        }

        /// <summary>
        /// 获取指定索引位置的参数。
        /// </summary>
        /// <typeparam name="TArg">参数类型。</typeparam>
        /// <param name="index">参数索引。</param>
        /// <returns>参数值。</returns>
        /// <exception cref="InvalidOperationException">当无法获取参数时抛出。</exception>
        public TArg GetArgument<TArg>(int index)
        {
            if (!TryGetArgument<TArg>(index, out var value))
            {
                throw new InvalidOperationException($"Can not read argument at index {index} as '{typeof(TArg).FullName}'.");
            }

            return value;
        }

        /// <summary>
        /// 尝试获取指定索引位置的参数。
        /// </summary>
        /// <typeparam name="TArg">参数类型。</typeparam>
        /// <param name="index">参数索引。</param>
        /// <param name="value">输出的参数值。</param>
        /// <returns>如果获取成功返回true，否则返回false。</returns>
        public bool TryGetArgument<TArg>(int index, out TArg value)
        {
            if (index < 0 || index >= _argumentCount)
            {
                value = default!;
                return false;
            }

            object argument;
            if (_arguments != null)
            {
                argument = _arguments[index];
            }
            else
            {
                argument = index switch
                {
                    0 => _argument0,
                    1 => _argument1,
                    2 => _argument2,
                    _ => null
                };
            }

            if (argument is TArg typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>
        /// 设置上下文项的值。
        /// </summary>
        /// <typeparam name="T">值类型。</typeparam>
        /// <param name="key">项的键。</param>
        /// <param name="value">项的值。</param>
        /// <exception cref="ArgumentException">当键为空时抛出。</exception>
        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Context item key can not be empty.", nameof(key));
            }

            _items[key] = value;
        }

        /// <summary>
        /// 尝试获取上下文项的值。
        /// </summary>
        /// <typeparam name="T">值类型。</typeparam>
        /// <param name="key">项的键。</param>
        /// <param name="value">输出的值。</param>
        /// <returns>如果获取成功返回true，否则返回false。</returns>
        public bool TryGet<T>(string key, out T value)
        {
            if (!string.IsNullOrWhiteSpace(key) && _items.TryGetValue(key, out var item) && item is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>
        /// 重置上下文以供对象池重用。
        /// </summary>
        public void ResetForPool()
        {
            Sender = null;
            EventKey = null;
            EventName = string.Empty;
            _arguments = Array.Empty<object>();
            _argument0 = null;
            _argument1 = null;
            _argument2 = null;
            _argumentCount = 0;
            CancellationToken = default;
            Handled = false;
            _items.Clear();
        }

        private void InitializeCommon(object sender, object eventKey, string eventName, CancellationToken cancellationToken)
        {
            Sender = sender;
            EventKey = eventKey;
            EventName = eventName;
            CancellationToken = cancellationToken;
            Handled = false;
            _items.Clear();
        }
    }
}

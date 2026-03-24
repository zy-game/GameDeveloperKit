using System;
using System.Collections.Generic;
using System.Threading;

namespace GameDeveloperKit.Runtime
{
    public sealed class EventContext : IEventContext
    {
        private readonly Dictionary<string, object> _items = new(StringComparer.Ordinal);

        internal EventContext(object sender, object eventKey, string eventName, object[] arguments, CancellationToken cancellationToken)
        {
            Sender = sender;
            EventKey = eventKey;
            EventName = eventName;
            Arguments = arguments ?? Array.Empty<object>();
            CancellationToken = cancellationToken;
        }

        public object Sender { get; }
        public object EventKey { get; }
        public string EventName { get; }
        public object[] Arguments { get; }
        public CancellationToken CancellationToken { get; }
        public bool Handled { get; set; }

        public TKey GetEventKey<TKey>()
        {
            if (EventKey is TKey key)
            {
                return key;
            }

            throw new InvalidCastException($"Can not cast event key '{EventKey?.GetType().FullName ?? "null"}' to '{typeof(TKey).FullName}'.");
        }

        public TArg GetArgument<TArg>(int index)
        {
            if (!TryGetArgument<TArg>(index, out var value))
            {
                throw new InvalidOperationException($"Can not read argument at index {index} as '{typeof(TArg).FullName}'.");
            }

            return value;
        }

        public bool TryGetArgument<TArg>(int index, out TArg value)
        {
            if (index < 0 || index >= Arguments.Length)
            {
                value = default!;
                return false;
            }

            var argument = Arguments[index];
            if (argument is TArg typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default!;
            return false;
        }

        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Context item key can not be empty.", nameof(key));
            }

            _items[key] = value;
        }

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
    }
}

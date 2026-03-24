using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public sealed class EventModule : IGameFrameworkModule
    {
        private readonly Dictionary<EventRegistrationKey, List<IEventHandle>> _handlers = new();
        private readonly Dictionary<EventRegistrationKey, List<IAsyncEventHandle>> _asyncHandlers = new();
        private bool _bindingsInitialized;

        public EventModule()
        {
            ScanAndRegisterBindings();
        }

        public int EventCount => _handlers.Count + _asyncHandlers.Count;

        public void Register(string eventName, IEventHandle handler)
        {
            RegisterInternal(EventRegistrationKey.From(eventName), handler);
        }

        public void Register(int eventId, IEventHandle handler)
        {
            RegisterInternal(EventRegistrationKey.From(eventId), handler);
        }

        public void Register<TEnum>(TEnum eventKey, IEventHandle handler)
            where TEnum : struct, Enum
        {
            RegisterInternal(EventRegistrationKey.From(eventKey), handler);
        }

        public void Register<THandler>(string eventName)
            where THandler : class, IEventHandle, new()
        {
            RegisterByTypeInternal<THandler>(EventRegistrationKey.From(eventName));
        }

        public void Register<THandler>(int eventId)
            where THandler : class, IEventHandle, new()
        {
            RegisterByTypeInternal<THandler>(EventRegistrationKey.From(eventId));
        }

        public void Register<TEnum, THandler>(TEnum eventKey)
            where TEnum : struct, Enum
            where THandler : class, IEventHandle, new()
        {
            RegisterByTypeInternal<THandler>(EventRegistrationKey.From(eventKey));
        }

        public void RegisterAsync(string eventName, IAsyncEventHandle handler)
        {
            RegisterAsyncInternal(EventRegistrationKey.From(eventName), handler);
        }

        public void RegisterAsync(int eventId, IAsyncEventHandle handler)
        {
            RegisterAsyncInternal(EventRegistrationKey.From(eventId), handler);
        }

        public void RegisterAsync<TEnum>(TEnum eventKey, IAsyncEventHandle handler)
            where TEnum : struct, Enum
        {
            RegisterAsyncInternal(EventRegistrationKey.From(eventKey), handler);
        }

        public void RegisterAsync<THandler>(string eventName)
            where THandler : class, IAsyncEventHandle, new()
        {
            RegisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventName));
        }

        public void RegisterAsync<THandler>(int eventId)
            where THandler : class, IAsyncEventHandle, new()
        {
            RegisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventId));
        }

        public void RegisterAsync<TEnum, THandler>(TEnum eventKey)
            where TEnum : struct, Enum
            where THandler : class, IAsyncEventHandle, new()
        {
            RegisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventKey));
        }

        public bool Unregister(string eventName, IEventHandle handler)
        {
            return UnregisterInternal(EventRegistrationKey.From(eventName), handler);
        }

        public bool Unregister(int eventId, IEventHandle handler)
        {
            return UnregisterInternal(EventRegistrationKey.From(eventId), handler);
        }

        public bool Unregister<TEnum>(TEnum eventKey, IEventHandle handler)
            where TEnum : struct, Enum
        {
            return UnregisterInternal(EventRegistrationKey.From(eventKey), handler);
        }

        public bool Unregister<THandler>(string eventName)
            where THandler : class, IEventHandle
        {
            return UnregisterByTypeInternal<THandler>(EventRegistrationKey.From(eventName));
        }

        public bool Unregister<THandler>(int eventId)
            where THandler : class, IEventHandle
        {
            return UnregisterByTypeInternal<THandler>(EventRegistrationKey.From(eventId));
        }

        public bool Unregister<TEnum, THandler>(TEnum eventKey)
            where TEnum : struct, Enum
            where THandler : class, IEventHandle
        {
            return UnregisterByTypeInternal<THandler>(EventRegistrationKey.From(eventKey));
        }

        public bool UnregisterAsync(string eventName, IAsyncEventHandle handler)
        {
            return UnregisterAsyncInternal(EventRegistrationKey.From(eventName), handler);
        }

        public bool UnregisterAsync(int eventId, IAsyncEventHandle handler)
        {
            return UnregisterAsyncInternal(EventRegistrationKey.From(eventId), handler);
        }

        public bool UnregisterAsync<TEnum>(TEnum eventKey, IAsyncEventHandle handler)
            where TEnum : struct, Enum
        {
            return UnregisterAsyncInternal(EventRegistrationKey.From(eventKey), handler);
        }

        public bool UnregisterAsync<THandler>(string eventName)
            where THandler : class, IAsyncEventHandle
        {
            return UnregisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventName));
        }

        public bool UnregisterAsync<THandler>(int eventId)
            where THandler : class, IAsyncEventHandle
        {
            return UnregisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventId));
        }

        public bool UnregisterAsync<TEnum, THandler>(TEnum eventKey)
            where TEnum : struct, Enum
            where THandler : class, IAsyncEventHandle
        {
            return UnregisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventKey));
        }

        public void Raise(string eventName, object sender = null, params object[] args)
        {
            RaiseInternal(EventRegistrationKey.From(eventName), sender, CancellationToken.None, args);
        }

        public void Raise(int eventId, object sender = null, params object[] args)
        {
            RaiseInternal(EventRegistrationKey.From(eventId), sender, CancellationToken.None, args);
        }

        public void Raise<TEnum>(TEnum eventKey, object sender = null, params object[] args)
            where TEnum : struct, Enum
        {
            RaiseInternal(EventRegistrationKey.From(eventKey), sender, CancellationToken.None, args);
        }

        public UniTask RaiseAsync(string eventName, object sender = null, params object[] args)
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventName), sender, CancellationToken.None, args);
        }

        public UniTask RaiseAsync(int eventId, object sender = null, params object[] args)
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventId), sender, CancellationToken.None, args);
        }

        public UniTask RaiseAsync<TEnum>(TEnum eventKey, object sender = null, params object[] args)
            where TEnum : struct, Enum
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventKey), sender, CancellationToken.None, args);
        }

        public void ScanAndRegisterBindings()
        {
            if (_bindingsInitialized)
            {
                return;
            }

            _bindingsInitialized = true;

            var providerType = typeof(IEventBindingProvider);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                for (var j = 0; j < types.Length; j++)
                {
                    var type = types[j];
                    if (type == null || type.IsAbstract || type.IsInterface || !providerType.IsAssignableFrom(type))
                    {
                        continue;
                    }

                    if (Activator.CreateInstance(type) is IEventBindingProvider provider)
                    {
                        provider.Register(this);
                    }
                }
            }
        }

        public void RescanAndRegisterBindings()
        {
            _bindingsInitialized = false;
            ScanAndRegisterBindings();
        }

        public void Dispose()
        {
            _handlers.Clear();
            _asyncHandlers.Clear();
            _bindingsInitialized = false;
        }

        private void RegisterInternal(EventRegistrationKey key, IEventHandle handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var handlers = GetOrCreateHandlers(key);
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }

        private void RegisterByTypeInternal<THandler>(EventRegistrationKey key)
            where THandler : class, IEventHandle, new()
        {
            var handlers = GetOrCreateHandlers(key);
            if (ContainsHandlerType<THandler>(handlers))
            {
                return;
            }

            handlers.Add(new THandler());
        }

        private void RegisterAsyncInternal(EventRegistrationKey key, IAsyncEventHandle handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var handlers = GetOrCreateAsyncHandlers(key);
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }

        private void RegisterAsyncByTypeInternal<THandler>(EventRegistrationKey key)
            where THandler : class, IAsyncEventHandle, new()
        {
            var handlers = GetOrCreateAsyncHandlers(key);
            if (ContainsHandlerType<THandler>(handlers))
            {
                return;
            }

            handlers.Add(new THandler());
        }

        private bool UnregisterInternal(EventRegistrationKey key, IEventHandle handler)
        {
            return RemoveHandler(_handlers, key, handler);
        }

        private bool UnregisterByTypeInternal<THandler>(EventRegistrationKey key)
            where THandler : class, IEventHandle
        {
            return RemoveHandlerByType<THandler, IEventHandle>(_handlers, key);
        }

        private bool UnregisterAsyncInternal(EventRegistrationKey key, IAsyncEventHandle handler)
        {
            return RemoveHandler(_asyncHandlers, key, handler);
        }

        private bool UnregisterAsyncByTypeInternal<THandler>(EventRegistrationKey key)
            where THandler : class, IAsyncEventHandle
        {
            return RemoveHandlerByType<THandler, IAsyncEventHandle>(_asyncHandlers, key);
        }

        private void RaiseInternal(EventRegistrationKey key, object sender, CancellationToken cancellationToken, object[] args)
        {
            if (!_handlers.TryGetValue(key, out var handlers) || handlers.Count == 0)
            {
                return;
            }

            var context = new EventContext(sender, key.Value, key.Name, args, cancellationToken);
            var snapshot = handlers.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
            {
                snapshot[i].Handle(context);
            }
        }

        private async UniTask RaiseAsyncInternal(EventRegistrationKey key, object sender, CancellationToken cancellationToken, object[] args)
        {
            var context = new EventContext(sender, key.Value, key.Name, args, cancellationToken);

            if (_handlers.TryGetValue(key, out var handlers) && handlers.Count > 0)
            {
                var syncSnapshot = handlers.ToArray();
                for (var i = 0; i < syncSnapshot.Length; i++)
                {
                    syncSnapshot[i].Handle(context);
                }
            }

            if (_asyncHandlers.TryGetValue(key, out var asyncHandlers) && asyncHandlers.Count > 0)
            {
                var asyncSnapshot = asyncHandlers.ToArray();
                for (var i = 0; i < asyncSnapshot.Length; i++)
                {
                    await asyncSnapshot[i].HandleAsync(context);
                }
            }
        }

        private List<IEventHandle> GetOrCreateHandlers(EventRegistrationKey key)
        {
            if (!_handlers.TryGetValue(key, out var handlers))
            {
                handlers = new List<IEventHandle>();
                _handlers.Add(key, handlers);
            }

            return handlers;
        }

        private List<IAsyncEventHandle> GetOrCreateAsyncHandlers(EventRegistrationKey key)
        {
            if (!_asyncHandlers.TryGetValue(key, out var handlers))
            {
                handlers = new List<IAsyncEventHandle>();
                _asyncHandlers.Add(key, handlers);
            }

            return handlers;
        }

        private static bool ContainsHandlerType<THandler>(List<IEventHandle> handlers)
            where THandler : class, IEventHandle
        {
            for (var i = 0; i < handlers.Count; i++)
            {
                if (handlers[i] is THandler)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsHandlerType<THandler>(List<IAsyncEventHandle> handlers)
            where THandler : class, IAsyncEventHandle
        {
            for (var i = 0; i < handlers.Count; i++)
            {
                if (handlers[i] is THandler)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RemoveHandler<THandler>(Dictionary<EventRegistrationKey, List<THandler>> dictionary, EventRegistrationKey key, THandler handler)
            where THandler : class
        {
            if (handler == null)
            {
                return false;
            }

            if (!dictionary.TryGetValue(key, out var handlers))
            {
                return false;
            }

            var removed = handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                dictionary.Remove(key);
            }

            return removed;
        }

        private static bool RemoveHandlerByType<TTarget, THandler>(Dictionary<EventRegistrationKey, List<THandler>> dictionary, EventRegistrationKey key)
            where TTarget : class
            where THandler : class
        {
            if (!dictionary.TryGetValue(key, out var handlers))
            {
                return false;
            }

            for (var i = 0; i < handlers.Count; i++)
            {
                if (handlers[i] is TTarget)
                {
                    handlers.RemoveAt(i);
                    if (handlers.Count == 0)
                    {
                        dictionary.Remove(key);
                    }

                    return true;
                }
            }

            return false;
        }

        private readonly struct EventRegistrationKey : IEquatable<EventRegistrationKey>
        {
            private EventRegistrationKey(object value, Type valueType, string name)
            {
                Value = value;
                ValueType = valueType;
                Name = name;
            }

            public object Value { get; }
            public Type ValueType { get; }
            public string Name { get; }

            public static EventRegistrationKey From(string eventName)
            {
                if (string.IsNullOrWhiteSpace(eventName))
                {
                    throw new ArgumentException("Event name can not be empty.", nameof(eventName));
                }

                return new EventRegistrationKey(eventName, typeof(string), eventName);
            }

            public static EventRegistrationKey From(int eventId)
            {
                return new EventRegistrationKey(eventId, typeof(int), eventId.ToString());
            }

            public static EventRegistrationKey From<TEnum>(TEnum eventKey)
                where TEnum : struct, Enum
            {
                var enumType = typeof(TEnum);
                return new EventRegistrationKey(eventKey, enumType, $"{enumType.Name}.{eventKey}");
            }

            public bool Equals(EventRegistrationKey other)
            {
                return ValueType == other.ValueType && Equals(Value, other.Value);
            }

            public override bool Equals(object obj)
            {
                return obj is EventRegistrationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ValueType, Value);
            }
        }
    }
}

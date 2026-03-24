using System;
using System.Threading;

namespace GameDeveloperKit.Runtime
{
    public interface IEventContext
    {
        object Sender { get; }
        object EventKey { get; }
        string EventName { get; }
        object[] Arguments { get; }
        CancellationToken CancellationToken { get; }
        bool Handled { get; set; }

        TKey GetEventKey<TKey>();
        TArg GetArgument<TArg>(int index);
        bool TryGetArgument<TArg>(int index, out TArg value);

        void Set<T>(string key, T value);
        bool TryGet<T>(string key, out T value);
    }
}

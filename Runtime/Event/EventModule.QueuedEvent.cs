using System;

namespace GameDeveloperKit.Event
{
    public partial class EventModule
    {
        private readonly struct QueuedEvent
        {
            /// <summary>
            /// 初始化 Queued Event。
            /// </summary>
            /// <param name="eventType">event Type 参数。</param>
            /// <param name="eventData">event Data 参数。</param>
            public QueuedEvent(Type eventType, ArgsBase eventData, object sender)
            {
                EventType = eventType;
                EventData = eventData;
                Sender = sender;
            }

            public Type EventType { get; }

            public ArgsBase EventData { get; }

            public object Sender { get; }
        }
    }
}

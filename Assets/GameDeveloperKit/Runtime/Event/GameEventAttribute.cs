using System;

namespace GameDeveloperKit.Runtime
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class GameEventAttribute : Attribute
    {
        public GameEventAttribute(string eventName)
        {
            EventName = eventName;
        }

        public GameEventAttribute(int eventId)
        {
            EventId = eventId;
            EventName = eventId.ToString();
        }

        /// <summary>Applied to handler classes to declare which event type they bind to.</summary>
        public GameEventAttribute(Type eventType)
        {
            EventType = eventType;
        }

        public string EventName { get; }
        public int? EventId { get; }
        public Type EventType { get; }
    }
}

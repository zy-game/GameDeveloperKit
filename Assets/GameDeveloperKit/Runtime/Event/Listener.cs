using System;

namespace GameDeveloperKit.Event
{
    internal sealed class Listener
    {
        internal Listener(Type eventType, IEventHandle handle)
        {
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            IsActive = true;
        }

        internal Listener(Type eventType, Delegate action)
        {
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            IsActive = true;
        }

        public Type EventType { get; }

        public IEventHandle Handle { get; }

        public Delegate Action { get; }

        public bool IsActive { get; private set; }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}

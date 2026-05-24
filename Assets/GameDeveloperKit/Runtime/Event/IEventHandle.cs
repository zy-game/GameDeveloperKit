namespace GameDeveloperKit.Event
{
    public interface IEventHandle
    {
        void Handle(object sender, object args);
    }

    public interface IEventHandle<in TEvent> : IEventHandle where TEvent : IEventArgs
    {
        void Handle(object sender, TEvent eventData);
    }
}
